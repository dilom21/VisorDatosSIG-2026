// FE-SIG 4 & CU10: Gestión de Entidades geográficas por clic / identificación.
// Cumple con CU10 - Gestionar Entidades (RF-CON-01, RF-VIS-02, RF-VIS-06).
// Actores: Administrador, Consultor.
//
// Flujo:
// 1. Identifica las entidades bajo el punto de clic consultando GET api/capas/identificar (SQL Server STIntersects).
// 2. Filtra por las capas que el usuario tiene activas.
// 3. Resalta visualmente la entidad seleccionada en el mapa en un pane de alta prioridad (paneResaltado).
// 4. Sincroniza bidireccionalmente el mapa con las tarjetas del panel de resultados.
// 5. La selección se mantiene hasta que el usuario limpie la selección, seleccione otra entidad,
//    o desactive la capa a la que pertenece la entidad seleccionada.
//
// Seguridad del DOM: document.createElement y textContent, estrictamente sin innerHTML.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var ID_BOTON = 'btn-identificar';
    var ID_PANEL = 'panel-identificacion';
    var ID_ESTADO = 'estado-identificacion';
    var ID_RESULTADOS = 'resultados-identificacion';
    var ID_COORDENADAS = 'coordenadas-identificacion';

    var TOLERANCIA_METROS = 10;
    var RUTA_IDENTIFICAR = 'api/capas/identificar';
    var DECIMALES_COORDENADA = 6;

    var TEXTO_AYUDA = 'Haga clic sobre el mapa para identificar y gestionar elementos.';
    var TEXTO_CARGANDO = 'Identificando...';
    var TEXTO_VACIO = 'No se encontraron elementos en este punto.';
    var TEXTO_ERROR = 'No fue posible identificar elementos.';

    var CLASE_MAPA_IDENTIFICANDO = 'visor-map--identificando';

    // Orden visual de los grupos coincidentes: Códigos Fijos, Vías, Lotes, Manzanas.
    var GRUPOS = [
        {
            capa: 'CodigosFijos',
            clave: 'codigosfijos',
            etiqueta: 'Códigos Fijos',
            campos: [
                ['Código SIG', 'CodF_SIG'],
                ['Código fijo', 'CodFijo'],
                ['Nombre', 'Nombre'],
                ['Estado', 'Estado'],
                ['Lote', 'IdLote']
            ]
        },
        {
            capa: 'Vias',
            clave: 'vias',
            etiqueta: 'Vías',
            campos: [
                ['Nombre', 'Nombre'],
                ['Tipo de vía', 'TipoVia'],
                ['OSMID', 'OSMID'],
                ['OBJECTID', 'OBJECTID']
            ]
        },
        {
            capa: 'Lotes',
            clave: 'lotes',
            etiqueta: 'Lotes',
            campos: [
                ['NroLote', 'NroLote'],
                ['IdManzana', 'IdManzana'],
                ['IdOrigen', 'IdOrigen']
            ]
        },
        {
            capa: 'Manzanas',
            clave: 'manzanas',
            etiqueta: 'Manzanas',
            campos: [
                ['UV_MZA', 'UV_MZA'],
                ['UV', 'UV'],
                ['MZA', 'MZA'],
                ['IdOrigen', 'IdOrigen']
            ]
        }
    ];

    // Estado del módulo
    var activo = false;
    var secuencia = 0;
    var capaResaltado = null;
    var entidadSeleccionada = null;
    var tarjetaSeleccionada = null;
    var botonLimpiar = null;

    // --- Utilidades de DOM y de valores -------------------------------------

    function obtenerNodo(id) {
        return document.getElementById(id);
    }

    function normalizar(texto) {
        if (typeof texto !== 'string') {
            return '';
        }
        var base = texto;
        if (typeof base.normalize === 'function') {
            base = base.normalize('NFD').replace(/[\u0300-\u036f]/g, '');
        }
        return base.toLowerCase().trim();
    }

    function esArreglo(valor) {
        return Object.prototype.toString.call(valor) === '[object Array]';
    }

    function valorPropiedad(feature, nombre) {
        if (!feature || !feature.properties || typeof feature.properties !== 'object') {
            return null;
        }
        if (Object.prototype.hasOwnProperty.call(feature.properties, nombre)) {
            return feature.properties[nombre];
        }
        var buscado = String(nombre).toLowerCase();
        var claves = Object.keys(feature.properties);
        for (var i = 0; i < claves.length; i++) {
            if (claves[i].toLowerCase() === buscado) {
                return feature.properties[claves[i]];
            }
        }
        return null;
    }

    function mostrarValor(valor) {
        if (valor === null || valor === undefined) {
            return '-';
        }
        var texto = String(valor);
        if (texto.trim() === '') {
            return '-';
        }
        return texto;
    }

    function nombreEstado(valor) {
        if (valor === null || valor === undefined || String(valor).trim() === '') {
            return null;
        }
        if (ns.markers && typeof ns.markers.obtenerNombreEstado === 'function') {
            return ns.markers.obtenerNombreEstado(valor);
        }
        return valor;
    }

    function formatearCoordenada(valor) {
        var numero = Number(valor);
        if (!isFinite(numero)) {
            return '';
        }
        return numero.toFixed(DECIMALES_COORDENADA);
    }

    function buscarGrupo(capaValor) {
        var buscado = normalizar(capaValor);
        for (var i = 0; i < GRUPOS.length; i++) {
            if (normalizar(GRUPOS[i].capa) === buscado) {
                return GRUPOS[i];
            }
        }
        return null;
    }

    function capaActiva(clave) {
        if (ns.capas && typeof ns.capas.estaActiva === 'function') {
            return ns.capas.estaActiva(clave) === true;
        }
        return true;
    }

    // --- Resaltado visual en el mapa (CU10) -----------------------------------

    function asegurarCapaResaltado() {
        var mapa = (ns.mapa && typeof ns.mapa.obtener === 'function') ? ns.mapa.obtener() : null;
        if (!mapa || typeof L === 'undefined') {
            return null;
        }
        if (!mapa.getPane('paneResaltado')) {
            mapa.createPane('paneResaltado');
            var pane = mapa.getPane('paneResaltado');
            if (pane) {
                pane.style.zIndex = '650';
                pane.style.pointerEvents = 'none';
            }
        }
        if (!capaResaltado) {
            capaResaltado = L.geoJSON(null, {
                pane: 'paneResaltado',
                style: function (feat) {
                    var tipoGeom = (feat && feat.geometry) ? feat.geometry.type : '';
                    if (tipoGeom === 'LineString' || tipoGeom === 'MultiLineString') {
                        return {
                            color: '#0284c7',
                            weight: 5.5,
                            opacity: 1,
                            lineCap: 'round',
                            lineJoin: 'round'
                        };
                    }
                    return {
                        color: '#0284c7',
                        weight: 3.5,
                        fillColor: '#38bdf8',
                        fillOpacity: 0.35,
                        dashArray: null
                    };
                },
                pointToLayer: function (feat, latlng) {
                    return L.circleMarker(latlng, {
                        pane: 'paneResaltado',
                        radius: 8,
                        weight: 3,
                        color: '#0284c7',
                        fillColor: '#ffffff',
                        fillOpacity: 1
                    });
                }
            });
            capaResaltado.addTo(mapa);
        }
        return capaResaltado;
    }

    function asegurarBotonLimpiar() {
        if (botonLimpiar) {
            return botonLimpiar;
        }
        var panel = obtenerNodo(ID_PANEL);
        if (!panel) {
            return null;
        }
        botonLimpiar = document.createElement('button');
        botonLimpiar.type = 'button';
        botonLimpiar.id = 'btn-limpiar-identificacion';
        botonLimpiar.className = 'btn-secundario visor-identificacion__btn-limpiar';
        botonLimpiar.textContent = 'Limpiar selección';
        botonLimpiar.hidden = true;
        botonLimpiar.addEventListener('click', function () {
            limpiarSeleccion();
        });
        panel.appendChild(botonLimpiar);
        return botonLimpiar;
    }

    function actualizarVisibilidadBotonLimpiar(mostrar) {
        var btn = asegurarBotonLimpiar();
        if (btn) {
            btn.hidden = !mostrar;
        }
    }

    // Resalta la entidad seleccionada en el mapa y sincroniza con el panel (CU10).
    function seleccionarEntidad(def, feature, elementoTarjeta, centrarMapa) {
        var capa = asegurarCapaResaltado();
        if (!capa) {
            return;
        }

        // Quitar resaltado de la tarjeta previa en el panel
        if (tarjetaSeleccionada) {
            tarjetaSeleccionada.classList.remove('visor-identificacion__entidad--seleccionada');
            tarjetaSeleccionada.removeAttribute('aria-current');
        }

        // Limpiar geometrías previas en el mapa
        capa.clearLayers();

        if (!feature) {
            entidadSeleccionada = null;
            tarjetaSeleccionada = null;
            actualizarVisibilidadBotonLimpiar(false);
            return;
        }

        entidadSeleccionada = { def: def, feature: feature };
        tarjetaSeleccionada = elementoTarjeta || null;

        if (tarjetaSeleccionada) {
            tarjetaSeleccionada.classList.add('visor-identificacion__entidad--seleccionada');
            tarjetaSeleccionada.setAttribute('aria-current', 'true');
        }

        // Añadir la geometría seleccionada a la capa de resaltado en el mapa
        if (feature.geometry) {
            capa.addData(feature);
        }

        actualizarVisibilidadBotonLimpiar(true);

        // Centrado suave opcional cuando el usuario hace clic en el panel
        if (centrarMapa) {
            var mapa = (ns.mapa && typeof ns.mapa.obtener === 'function') ? ns.mapa.obtener() : null;
            if (mapa) {
                var bounds = (typeof capa.getBounds === 'function') ? capa.getBounds() : null;
                if (bounds && bounds.isValid && bounds.isValid()) {
                    mapa.fitBounds(bounds, { maxZoom: Math.max(mapa.getZoom(), 17), padding: [40, 40] });
                }
            }
        }
    }

    // Limpia el resaltado en el mapa y la selección en el panel (CU10 Postcondición)
    function limpiarSeleccion() {
        if (capaResaltado) {
            capaResaltado.clearLayers();
        }
        if (tarjetaSeleccionada) {
            tarjetaSeleccionada.classList.remove('visor-identificacion__entidad--seleccionada');
            tarjetaSeleccionada.removeAttribute('aria-current');
        }
        entidadSeleccionada = null;
        tarjetaSeleccionada = null;
        actualizarVisibilidadBotonLimpiar(false);
    }

    // Excepción 4 de CU10: Si la capa de la entidad seleccionada se apaga, limpia su selección.
    function notificarCapaDesactivada(claveCapa) {
        if (entidadSeleccionada && entidadSeleccionada.def && entidadSeleccionada.def.clave === claveCapa) {
            limpiarSeleccion();
        }
    }

    // --- Panel: coordenadas, estado y resultados ----------------------------

    function establecerCoordenadas(lat, lng) {
        var nodo = obtenerNodo(ID_COORDENADAS);
        if (!nodo) {
            return;
        }
        nodo.textContent = 'Latitud: ' + formatearCoordenada(lat)
            + ' | Longitud: ' + formatearCoordenada(lng);
    }

    function limpiarCoordenadas() {
        var nodo = obtenerNodo(ID_COORDENADAS);
        if (nodo) {
            nodo.textContent = '';
        }
    }

    function fijarEstado(texto, modificador) {
        var nodo = obtenerNodo(ID_ESTADO);
        if (!nodo) {
            return;
        }
        nodo.textContent = texto;
        nodo.className = 'visor-identificacion__estado'
            + (modificador ? ' visor-identificacion__estado--' + modificador : '');
    }

    function limpiarResultados() {
        var nodo = obtenerNodo(ID_RESULTADOS);
        if (!nodo) {
            return;
        }
        while (nodo.firstChild) {
            nodo.removeChild(nodo.firstChild);
        }
    }

    function mostrarPanel() {
        var panel = obtenerNodo(ID_PANEL);
        if (panel) {
            panel.hidden = false;
        }
    }

    function ocultarPanel() {
        var panel = obtenerNodo(ID_PANEL);
        if (panel) {
            panel.hidden = true;
        }
    }

    function crearFila(clave, valor) {
        var fila = document.createElement('div');
        fila.className = 'visor-identificacion__fila';

        var termino = document.createElement('dt');
        termino.className = 'visor-identificacion__clave';
        termino.textContent = clave;

        var definicion = document.createElement('dd');
        definicion.className = 'visor-identificacion__valor';
        definicion.textContent = mostrarValor(valor);

        fila.appendChild(termino);
        fila.appendChild(definicion);
        return fila;
    }

    function crearEntidad(def, feature) {
        var tarjeta = document.createElement('article');
        tarjeta.className = 'visor-identificacion__entidad';
        tarjeta.setAttribute('tabindex', '0');
        tarjeta.setAttribute('role', 'button');
        tarjeta.setAttribute('aria-label', 'Seleccionar y resaltar entidad de ' + def.etiqueta + ' con ID ' + (feature ? feature.id : ''));

        // Cabecera interactiva de la tarjeta
        var cabecera = document.createElement('div');
        cabecera.className = 'visor-identificacion__entidad-head';

        var insignia = document.createElement('span');
        insignia.className = 'visor-identificacion__insignia';
        insignia.textContent = def.etiqueta;
        cabecera.appendChild(insignia);

        var ayudaSeleccion = document.createElement('span');
        ayudaSeleccion.className = 'visor-identificacion__pista';
        ayudaSeleccion.textContent = 'Clic para resaltar';
        cabecera.appendChild(ayudaSeleccion);

        tarjeta.appendChild(cabecera);

        var lista = document.createElement('dl');
        lista.className = 'visor-identificacion__lista';

        for (var i = 0; i < def.campos.length; i++) {
            var etiqueta = def.campos[i][0];
            var columna = def.campos[i][1];
            var valor = valorPropiedad(feature, columna);
            if (columna === 'Estado') {
                valor = nombreEstado(valor);
            }
            lista.appendChild(crearFila(etiqueta, valor));
        }
        lista.appendChild(crearFila('ID', feature ? feature.id : null));

        tarjeta.appendChild(lista);

        // Vínculo bidireccional (CU10): Al hacer clic en la fila se resalta la geometría en el mapa
        tarjeta.addEventListener('click', function () {
            seleccionarEntidad(def, feature, tarjeta, true);
        });

        tarjeta.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                seleccionarEntidad(def, feature, tarjeta, true);
            }
        });

        return tarjeta;
    }

    function crearGrupo(def, items, enlacePrimerResultado) {
        var grupo = document.createElement('section');
        grupo.className = 'visor-identificacion__grupo';

        var titulo = document.createElement('h3');
        titulo.className = 'visor-identificacion__grupo-titulo';
        titulo.textContent = def.etiqueta + ' (' + items.length + ')';
        grupo.appendChild(titulo);

        for (var i = 0; i < items.length; i++) {
            var tarjeta = crearEntidad(def, items[i]);
            grupo.appendChild(tarjeta);

            if (enlacePrimerResultado && !enlacePrimerResultado.establecido) {
                enlacePrimerResultado.establecido = true;
                enlacePrimerResultado.def = def;
                enlacePrimerResultado.feature = items[i];
                enlacePrimerResultado.tarjeta = tarjeta;
            }
        }
        return grupo;
    }

    // CU11 (RF-VIS-07): Ajustar vista al conjunto de geometrías identificadas
    function ajustarExtensionResultados(grupos) {
        var mapa = (ns.mapa && typeof ns.mapa.obtener === 'function') ? ns.mapa.obtener() : null;
        if (!mapa || !grupos) {
            return false;
        }
        var bounds = null;
        for (var i = 0; i < grupos.length; i++) {
            var items = grupos[i].items;
            for (var j = 0; j < items.length; j++) {
                var f = items[j];
                if (f && f.geometry) {
                    var l = L.geoJSON(f);
                    var b = l.getBounds();
                    if (b && b.isValid && b.isValid()) {
                        if (!bounds) {
                            bounds = L.latLngBounds(b.getSouthWest(), b.getNorthEast());
                        } else {
                            bounds.extend(b);
                        }
                    }
                }
            }
        }
        if (bounds && bounds.isValid && bounds.isValid()) {
            mapa.fitBounds(bounds, { padding: [40, 40], maxZoom: 18 });
            return true;
        }
        return false;
    }

    function renderizar(grupos) {
        var contenedor = obtenerNodo(ID_RESULTADOS);
        if (!contenedor) {
            return;
        }
        limpiarResultados();
        asegurarBotonLimpiar();

        var totalIdentificados = 0;
        for (var t = 0; t < grupos.length; t++) {
            totalIdentificados += grupos[t].items.length;
        }

        // Si hay múltiples resultados identificados, permitir ajustar la extensión a todos
        if (totalIdentificados > 1) {
            var barraZoom = document.createElement('div');
            barraZoom.className = 'visor-identificacion__barra-extension';

            var btnZoom = document.createElement('button');
            btnZoom.type = 'button';
            btnZoom.id = 'btn-zoom-identificacion';
            btnZoom.className = 'btn-secundario visor-identificacion__btn-zoom';
            btnZoom.textContent = 'Ajustar a todos los resultados (' + totalIdentificados + ')';
            btnZoom.title = 'Ajustar la vista del mapa a todas las geometrías identificadas';
            btnZoom.addEventListener('click', function () {
                ajustarExtensionResultados(grupos);
            });
            barraZoom.appendChild(btnZoom);
            contenedor.appendChild(barraZoom);
        }

        var primerResultado = { establecido: false, def: null, feature: null, tarjeta: null };

        for (var i = 0; i < grupos.length; i++) {
            if (grupos[i].items.length === 0) {
                continue;
            }
            contenedor.appendChild(crearGrupo(grupos[i].def, grupos[i].items, primerResultado));
        }

        // Selección y resaltado automático del primer elemento coincidente (CU10 Flujo Principal)
        if (primerResultado.establecido) {
            seleccionarEntidad(primerResultado.def, primerResultado.feature, primerResultado.tarjeta, false);
        } else {
            limpiarSeleccion();
        }
    }

    // --- Filtrado por capas activas -----------------------------------------

    function filtrar(features) {
        var grupos = [];
        var porClave = {};
        for (var i = 0; i < GRUPOS.length; i++) {
            var entrada = { def: GRUPOS[i], items: [] };
            grupos.push(entrada);
            porClave[GRUPOS[i].clave] = entrada;
        }

        var total = 0;
        for (var j = 0; j < features.length; j++) {
            var feature = features[j];
            if (!feature || typeof feature !== 'object') {
                continue;
            }
            var def = buscarGrupo(valorPropiedad(feature, '_Capa'));
            if (!def) {
                continue;
            }
            if (!capaActiva(def.clave)) {
                continue;
            }
            porClave[def.clave].items.push(feature);
            total += 1;
        }

        return { grupos: grupos, total: total };
    }

    function textoConteo(total) {
        return total + (total === 1 ? ' elemento identificado' : ' elementos identificados');
    }

    // --- Llamada al endpoint real -------------------------------------------

    function construirRuta(lng, lat) {
        return RUTA_IDENTIFICAR
            + '?lng=' + encodeURIComponent(lng)
            + '&lat=' + encodeURIComponent(lat)
            + '&tolerancia=' + encodeURIComponent(TOLERANCIA_METROS);
    }

    function obtenerToken() {
        if (ns.sesion && typeof ns.sesion.obtenerToken === 'function') {
            return ns.sesion.obtenerToken();
        }
        return null;
    }

    function consultar(lng, lat) {
        return new Promise(function (resolver) {
            if (!ns.api || typeof ns.api.peticion !== 'function') {
                resolver({ ok: false, status: 0, error: 'contrato', features: null });
                return;
            }
            try {
                ns.api.peticion(construirRuta(lng, lat), {
                    metodo: 'GET',
                    token: obtenerToken(),
                    protegida: true
                }).then(function (resultado) {
                    if (resultado && resultado.ok && resultado.status === 200
                        && esArreglo(resultado.datos)) {
                        resolver({
                            ok: true,
                            status: resultado.status,
                            error: null,
                            features: resultado.datos
                        });
                        return;
                    }
                    if (resultado && resultado.ok && resultado.status === 200) {
                        resolver({ ok: false, status: resultado.status, error: 'contrato', features: null });
                        return;
                    }
                    resolver({
                        ok: false,
                        status: resultado ? resultado.status : 0,
                        error: (resultado && resultado.error) ? resultado.error : 'contrato',
                        features: null
                    });
                }).catch(function () {
                    resolver({ ok: false, status: 0, error: 'contrato', features: null });
                });
            } catch (e) {
                resolver({ ok: false, status: 0, error: 'contrato', features: null });
            }
        });
    }

    function aplicarRespuesta(resultado) {
        if (resultado.status === 401) {
            return;
        }
        if (!resultado.ok) {
            limpiarSeleccion();
            fijarEstado(TEXTO_ERROR, 'error');
            return;
        }

        var filtrados = filtrar(resultado.features);
        if (filtrados.total === 0) {
            limpiarSeleccion();
            fijarEstado(TEXTO_VACIO, 'vacio');
            renderizar(filtrados.grupos);
            return;
        }

        fijarEstado(textoConteo(filtrados.total), 'exito');
        renderizar(filtrados.grupos);
    }

    // --- API pública --------------------------------------------------------

    function estaActivo() {
        return activo === true;
    }

    function actualizarBoton() {
        var boton = obtenerNodo(ID_BOTON);
        if (!boton) {
            return;
        }
        boton.setAttribute('aria-pressed', activo ? 'true' : 'false');
        if (activo) {
            boton.classList.add('visor-identificar__boton--activo');
        } else {
            boton.classList.remove('visor-identificar__boton--activo');
        }
    }

    function actualizarCursor() {
        var mapa = (ns.mapa && typeof ns.mapa.obtener === 'function') ? ns.mapa.obtener() : null;
        if (!mapa || typeof mapa.getContainer !== 'function') {
            return;
        }
        var contenedor = mapa.getContainer();
        if (!contenedor) {
            return;
        }
        if (activo) {
            contenedor.classList.add(CLASE_MAPA_IDENTIFICANDO);
        } else {
            contenedor.classList.remove(CLASE_MAPA_IDENTIFICANDO);
        }
    }

    function activar() {
        if (activo) {
            return;
        }
        activo = true;
        secuencia += 1;
        asegurarCapaResaltado();
        actualizarBoton();
        actualizarCursor();
        limpiarResultados();
        limpiarCoordenadas();
        fijarEstado(TEXTO_AYUDA, 'ayuda');
        mostrarPanel();
    }

    function desactivar() {
        if (!activo) {
            return;
        }
        activo = false;
        secuencia += 1;
        limpiarSeleccion();
        actualizarBoton();
        actualizarCursor();
        ocultarPanel();
    }

    function manejarClic(evento) {
        if (!activo) {
            return;
        }
        var latlng = (evento && evento.latlng) ? evento.latlng : null;
        if (!latlng) {
            return;
        }
        var lng = Number(latlng.lng);
        var lat = Number(latlng.lat);
        if (!isFinite(lng) || !isFinite(lat)) {
            return;
        }

        secuencia += 1;
        var actual = secuencia;

        establecerCoordenadas(lat, lng);
        limpiarResultados();
        limpiarSeleccion();
        fijarEstado(TEXTO_CARGANDO, 'cargando');

        consultar(lng, lat).then(function (resultado) {
            if (actual !== secuencia) {
                return;
            }
            if (!activo) {
                return;
            }
            aplicarRespuesta(resultado);
        });
    }

    function inicializar() {
        var boton = obtenerNodo(ID_BOTON);
        if (boton && boton.getAttribute('data-identificar-enlazado') !== '1') {
            boton.setAttribute('data-identificar-enlazado', '1');
            boton.addEventListener('click', function () {
                if (activo) {
                    desactivar();
                } else {
                    activar();
                }
            });
        }

        ocultarPanel();
        actualizarBoton();
        actualizarCursor();
    }

    ns.identify = {
        inicializar: inicializar,
        activar: activar,
        desactivar: desactivar,
        estaActivo: estaActivo,
        manejarClic: manejarClic,
        seleccionarEntidad: seleccionarEntidad,
        limpiarSeleccion: limpiarSeleccion,
        notificarCapaDesactivada: notificarCapaDesactivada,
        ajustarExtensionResultados: ajustarExtensionResultados
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', inicializar);
    } else {
        inicializar();
    }
})(window.VisorSIG);
