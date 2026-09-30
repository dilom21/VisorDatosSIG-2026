// FE-SIG 4: identificación de entidades geográficas por clic (CU17).
// El modo Identificar NO está siempre activo: se habilita con el botón
// #btn-identificar. Con el modo activo, el clic del mapa se delega aquí desde
// el único despachador de layers.js y se consulta el endpoint espacial real
// GET api/capas/identificar (SQL Server STIntersects) con JWT.
//
// Este módulo NO registra map.on('click'): no puede haber dos sistemas
// compitiendo. Tampoco dibuja geometrías ni crea capas Leaflet: solo presenta
// la lista textual de resultados, ya filtrada por las capas activas.
// Seguridad del DOM: todo se construye con document.createElement y
// textContent; nunca se usa innerHTML.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var ID_BOTON = 'btn-identificar';
    var ID_PANEL = 'panel-identificacion';
    var ID_ESTADO = 'estado-identificacion';
    var ID_RESULTADOS = 'resultados-identificacion';
    var ID_COORDENADAS = 'coordenadas-identificacion';

    // Tolerancia fija pedida por la fase: 10 metros. El usuario no puede
    // escribir una tolerancia arbitraria todavía.
    var TOLERANCIA_METROS = 10;
    var RUTA_IDENTIFICAR = 'api/capas/identificar';
    var DECIMALES_COORDENADA = 6;

    var TEXTO_AYUDA = 'Haga clic sobre el mapa para identificar elementos.';
    var TEXTO_CARGANDO = 'Identificando...';
    var TEXTO_VACIO = 'No se encontraron elementos en este punto.';
    var TEXTO_ERROR = 'No fue posible identificar elementos.';

    var CLASE_MAPA_IDENTIFICANDO = 'visor-map--identificando';

    // Orden visual de los grupos: Códigos Fijos, Vías, Lotes, Manzanas.
    // Coincide con la prioridad visual del despachador de FE-SIG 3.
    // `capa` es el valor que llega en properties._Capa; `clave` la clave
    // interna de layers.js (estaActiva); `campos` los pares [etiqueta, columna].
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

    // Estado del módulo. `secuencia` protege contra respuestas antiguas: solo
    // se pinta la respuesta cuyo número coincide con el último clic emitido.
    var activo = false;
    var secuencia = 0;

    // --- Utilidades de DOM y de valores -------------------------------------

    function obtenerNodo(id) {
        return document.getElementById(id);
    }

    // Tolerante a mayúsculas/minúsculas y a acentos, igual que el backend y que
    // markers.js/features.js.
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

    // Null, indefinido o cadena vacía se muestran como guion largo.
    function mostrarValor(valor) {
        if (valor === null || valor === undefined) {
            return '—';
        }
        var texto = String(valor);
        if (texto.trim() === '') {
            return '—';
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
        // Sin el módulo de capas no hay filtro posible: se muestran todos.
        return true;
    }

    // --- Panel: coordenadas, estado y resultados ----------------------------

    function establecerCoordenadas(lat, lng) {
        var nodo = obtenerNodo(ID_COORDENADAS);
        if (!nodo) {
            return;
        }
        nodo.textContent = 'Latitud: ' + formatearCoordenada(lat)
            + ' · Longitud: ' + formatearCoordenada(lng);
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
        return tarjeta;
    }

    function crearGrupo(def, items) {
        var grupo = document.createElement('section');
        grupo.className = 'visor-identificacion__grupo';

        var titulo = document.createElement('h3');
        titulo.className = 'visor-identificacion__grupo-titulo';
        titulo.textContent = def.etiqueta + ' (' + items.length + ')';
        grupo.appendChild(titulo);

        for (var i = 0; i < items.length; i++) {
            grupo.appendChild(crearEntidad(def, items[i]));
        }
        return grupo;
    }

    function renderizar(grupos) {
        var contenedor = obtenerNodo(ID_RESULTADOS);
        if (!contenedor) {
            return;
        }
        limpiarResultados();
        for (var i = 0; i < grupos.length; i++) {
            if (grupos[i].items.length === 0) {
                continue;
            }
            contenedor.appendChild(crearGrupo(grupos[i].def, grupos[i].items));
        }
    }

    // --- Filtrado por capas activas -----------------------------------------

    // El backend identifica sobre las cuatro tablas; aquí se descartan los
    // resultados de las capas que el usuario tiene apagadas. No se vuelve a
    // consultar ningún dato: solo se filtra la respuesta ya recibida.
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
        return total + (total === 1 ? ' elemento encontrado' : ' elementos encontrados');
    }

    // --- Llamada al endpoint real -------------------------------------------

    function construirRuta(lng, lat) {
        // Se usa la precisión numérica real del evento; no se redondea la
        // petición (el redondeo de 6 decimales es solo visual).
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

    // Nunca rechaza: siempre resuelve con un objeto normalizado.
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
            // Sesión expirada: api.js limpia y guard.js redirige; no se pisa
            // ese flujo con un mensaje de error propio.
            return;
        }
        if (!resultado.ok) {
            fijarEstado(TEXTO_ERROR, 'error');
            return;
        }

        var filtrados = filtrar(resultado.features);
        if (filtrados.total === 0) {
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
        // Invalida cualquier respuesta anterior en vuelo.
        secuencia += 1;
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
        // Una respuesta en vuelo ya no debe pintar nada.
        secuencia += 1;
        actualizarBoton();
        actualizarCursor();
        ocultarPanel();
    }

    // Recibe el clic desde el despachador único de layers.js. Nunca se registra
    // aquí un map.on('click').
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
        fijarEstado(TEXTO_CARGANDO, 'cargando');

        consultar(lng, lat).then(function (resultado) {
            // Respuesta antigua: un clic más reciente ya salió.
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
        // El botón se enlaza una sola vez, aunque el script se cargue dos veces.
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
        manejarClic: manejarClic
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', inicializar);
    } else {
        inicializar();
    }
})(window.VisorSIG);
