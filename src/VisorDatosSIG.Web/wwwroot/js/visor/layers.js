// FE-SIG 2 y 3: administración de las capas del visor.
// Un único registro (CAPAS) describe las cuatro capas: Manzanas, Lotes,
// Códigos Fijos y Vías. La carga (extensión global + GeoJSON por capa), el
// estado visible en el panel, el control de activación y la recarga por
// movimiento del mapa se resuelven de forma genérica sobre ese registro.
// Cada capa Leaflet se crea UNA sola vez y se reutiliza; en cada moveend solo
// se piden datos de las capas activas. Códigos Fijos conserva su renderer y
// pane por defecto (por encima de los tres panes vectoriales nuevos).

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var DEBOUNCE_MS = 250;
    var LIMITE = 10000;
    var PADDING_FIT = [20, 20];
    var TEXTO_CARGANDO = 'Cargando...';
    var TEXTO_VACIO = 'Sin elementos en esta extensión.';
    var TEXTO_INACTIVA = 'Inactiva';
    var ID_RESUMEN = 'resumen-capas';

    // Registro de capas en orden de panel. `catalogo` es el nombre exacto que
    // devuelve GET api/capas; `tipo` elige la representación Leaflet.
    // Lotes es la capa más pesada, por eso arranca desactivada; Manzanas,
    // Códigos Fijos y Vías arrancan activas.
    var CAPAS = [
        {
            clave: 'manzanas',
            etiqueta: 'Manzanas',
            api: 'manzanas',
            idCheckbox: 'capa-manzanas',
            idEstado: 'estado-manzanas',
            idConteo: 'conteo-manzanas',
            limite: LIMITE,
            activaInicial: true,
            catalogo: 'Manzanas',
            tipo: 'poligono-manzana'
        },
        {
            clave: 'lotes',
            etiqueta: 'Lotes',
            api: 'lotes',
            idCheckbox: 'capa-lotes',
            idEstado: 'estado-lotes',
            idConteo: 'conteo-lotes',
            limite: LIMITE,
            activaInicial: false,
            catalogo: 'Lotes',
            tipo: 'poligono-lote'
        },
        {
            clave: 'codigosfijos',
            etiqueta: 'Códigos Fijos',
            api: 'codigosfijos',
            idCheckbox: 'capa-codigos-fijos',
            idEstado: 'estado-codigos-fijos',
            idConteo: 'conteo-codigos-fijos',
            limite: LIMITE,
            activaInicial: true,
            catalogo: 'CodigosFijos',
            tipo: 'punto'
        },
        {
            clave: 'vias',
            etiqueta: 'Vías',
            api: 'vias',
            idCheckbox: 'capa-vias',
            idEstado: 'estado-vias',
            idConteo: 'conteo-vias',
            limite: LIMITE,
            activaInicial: true,
            catalogo: 'Vias',
            tipo: 'linea'
        }
    ];

    // Panes por tipo de geometría. Códigos Fijos no tiene pane propio: usa el
    // pane por defecto de Leaflet, que queda por encima de estos.
    var PANES = {
        'poligono-manzana': { nombre: 'paneManzanas', zIndex: 350 },
        'poligono-lote': { nombre: 'paneLotes', zIndex: 360 },
        'linea': { nombre: 'paneVias', zIndex: 370 }
    };

    // Estado por capa en un único objeto del namespace: una carga duplicada del
    // script reutiliza las capas y las secuencias ya existentes.
    var ESTADO = ns._estadoCapas;
    if (!ESTADO) {
        ESTADO = {};
        ns._estadoCapas = ESTADO;
    }
    for (var c = 0; c < CAPAS.length; c++) {
        if (!ESTADO[CAPAS[c].clave]) {
            ESTADO[CAPAS[c].clave] = {
                capa: null,
                activa: false,
                secuencia: 0,
                conDatos: false,
                ultimoTotal: 0,
                limiteAlcanzado: false
            };
        }
    }

    function obtenerMapa() {
        return (ns.mapa && typeof ns.mapa.obtener === 'function') ? ns.mapa.obtener() : null;
    }

    function buscarDefinicion(clave) {
        for (var i = 0; i < CAPAS.length; i++) {
            if (CAPAS[i].clave === clave) {
                return CAPAS[i];
            }
        }
        return null;
    }

    function establecerEstado(def, texto, modificador) {
        var nodo = document.getElementById(def.idEstado);
        if (!nodo) {
            return;
        }
        nodo.textContent = texto;
        nodo.className = 'visor-badge visor-badge--' + modificador;
    }

    function establecerConteo(def, texto) {
        var nodo = document.getElementById(def.idConteo);
        if (nodo) {
            nodo.textContent = texto;
        }
    }

    // Separador de miles con punto, sin depender de Intl ni de la configuración
    // regional del navegador.
    function formatearNumero(n) {
        var entero = Math.floor(Number(n));
        if (!isFinite(entero)) {
            return '';
        }
        var signo = entero < 0 ? '-' : '';
        var texto = String(Math.abs(entero));
        var partes = [];
        var i = texto.length;
        while (i > 3) {
            partes.unshift(texto.slice(i - 3, i));
            i -= 3;
        }
        partes.unshift(texto.slice(0, i));
        return signo + partes.join('.');
    }

    // El guard marca body con `sesion-verificando` mientras valida contra /me.
    // Se espera a que desaparezca con un MutationObserver, sin sondeos.
    function esperarSesionValidada() {
        return new Promise(function (resolver) {
            var cuerpo = document.body;
            if (!cuerpo || !cuerpo.classList.contains('sesion-verificando')) {
                resolver();
                return;
            }
            if (typeof MutationObserver === 'undefined') {
                resolver();
                return;
            }
            var observador = new MutationObserver(function () {
                if (!cuerpo.classList.contains('sesion-verificando')) {
                    observador.disconnect();
                    resolver();
                }
            });
            observador.observe(cuerpo, { attributes: true, attributeFilter: ['class'] });
        });
    }

    function esperarMapa() {
        return new Promise(function (resolver) {
            var mapa = obtenerMapa();
            if (mapa) {
                resolver(mapa);
                return;
            }
            if (document.readyState !== 'loading') {
                // El DOM ya está listo: DOMContentLoaded no volverá a dispararse.
                resolver(obtenerMapa());
                return;
            }
            document.addEventListener('DOMContentLoaded', function () {
                resolver(obtenerMapa());
            }, { once: true });
        });
    }

    function crearPane(mapa, nombre, zIndex) {
        if (!mapa || typeof mapa.createPane !== 'function' || !nombre) {
            return;
        }
        var pane = mapa.getPane(nombre);
        if (!pane) {
            mapa.createPane(nombre);
            pane = mapa.getPane(nombre);
        }
        if (pane) {
            pane.style.zIndex = zIndex;
        }
    }

    // Orden de prioridad del clic, de arriba abajo. Coincide con el orden
    // visual de los panes: Códigos Fijos arriba, luego Vías, Lotes y Manzanas.
    var ORDEN_CLIC = ['codigosfijos', 'vias', 'lotes', 'manzanas'];

    // Crea la capa Leaflet una sola vez por capa. Para los puntos se conserva
    // el comportamiento de FE-SIG 2 (markers.js); para polígonos y líneas se
    // usa la simbología y el popup de features.js.
    function crearCapa(mapa, def, estado) {
        if (estado.capa || typeof L === 'undefined') {
            return;
        }

        var opciones = {};

        if (def.tipo === 'punto') {
            if (!ns.markers) {
                return;
            }
            opciones.pointToLayer = function (feature, latlng) {
                return ns.markers.crearMarker(feature, latlng);
            };
            opciones.renderer = ns.markers.obtenerRenderer();
        } else {
            if (!ns.features) {
                return;
            }
            if (def.tipo === 'poligono-manzana') {
                opciones.style = ns.features.estiloManzana;
                opciones.renderer = ns.features.obtenerRendererManzanas();
            } else if (def.tipo === 'poligono-lote') {
                opciones.style = ns.features.estiloLote;
                opciones.renderer = ns.features.obtenerRendererLotes();
            } else if (def.tipo === 'linea') {
                opciones.style = ns.features.estiloVia;
                opciones.renderer = ns.features.obtenerRendererVias();
            } else {
                return;
            }

            // El contenido se entrega como función: Leaflet la invoca solo al
            // abrir el popup, de modo que miles de geometrías no construyen
            // miles de nodos DOM por adelantado.
            opciones.onEachFeature = function (feature, capa) {
                var creador = null;
                if (def.tipo === 'poligono-manzana') {
                    creador = ns.features.crearPopupManzana;
                } else if (def.tipo === 'poligono-lote') {
                    creador = ns.features.crearPopupLote;
                } else if (def.tipo === 'linea') {
                    creador = ns.features.crearPopupVia;
                }
                if (creador) {
                    capa.bindPopup(function () {
                        return creador(feature);
                    });
                }
            };
        }

        estado.capa = L.geoJSON({ type: 'FeatureCollection', features: [] }, opciones);
    }

    // Cada renderer de Leaflet dibuja en su propio <canvas> y adjunta su
    // manejador de clic a ESE canvas, marcandolo ademas con
    // `_leaflet_disable_events` para que el mapa ignore sus eventos. Con un
    // canvas por capa, el canvas mas alto se queda con todos los clics y las
    // capas de abajo nunca abririan su popup. Para conservar a la vez el orden
    // visual pedido y popups en las cuatro capas, los canvas no capturan el
    // puntero y un unico despachador en el mapa reparte el clic.
    function desactivarPunteroDeRenderers() {
        var renderers = [];
        if (ns.markers && typeof ns.markers.obtenerRenderer === 'function') {
            renderers.push(ns.markers.obtenerRenderer());
        }
        if (ns.features) {
            if (typeof ns.features.obtenerRendererManzanas === 'function') { renderers.push(ns.features.obtenerRendererManzanas()); }
            if (typeof ns.features.obtenerRendererLotes === 'function') { renderers.push(ns.features.obtenerRendererLotes()); }
            if (typeof ns.features.obtenerRendererVias === 'function') { renderers.push(ns.features.obtenerRendererVias()); }
        }
        for (var i = 0; i < renderers.length; i++) {
            var renderer = renderers[i];
            if (renderer && renderer._container && renderer._container.style) {
                renderer._container.style.pointerEvents = 'none';
            }
        }
    }

    // Busca la geometria mas alta de una capa bajo el punto del clic, usando la
    // misma prueba que emplea el renderer de Leaflet.
    //
    // Deuda tecnica NO bloqueante: `_containsPoint` es un metodo interno del
    // renderer Canvas de Leaflet 1.9.4, version que esta fijada localmente en el
    // repositorio. Se usa porque el despachador necesita exactamente el mismo
    // hit-test que ese renderer, y el acceso queda encapsulado aqui. Si Leaflet
    // se actualiza hay que revisar que el metodo siga existiendo y conservando
    // su comportamiento. La comprobacion `typeof capa._containsPoint ===
    // 'function'` de abajo evita que un cambio de version rompa el despachador
    // con un TypeError.
    function buscarObjetivoEnPunto(estado, punto) {
        if (!estado.capa || typeof estado.capa.getLayers !== 'function') {
            return null;
        }
        var capas = estado.capa.getLayers();
        for (var i = capas.length - 1; i >= 0; i--) {
            var capa = capas[i];
            if (capa && typeof capa._containsPoint === 'function' && capa._containsPoint(punto)) {
                return capa;
            }
        }
        return null;
    }

    function despacharClic(evento) {
        if (!evento || !evento.layerPoint) {
            return;
        }
        for (var i = 0; i < ORDEN_CLIC.length; i++) {
            var def = buscarDefinicion(ORDEN_CLIC[i]);
            var estado = def ? ESTADO[def.clave] : null;
            if (!def || !estado || !estado.activa) {
                continue;
            }
            var capa = buscarObjetivoEnPunto(estado, evento.layerPoint);
            if (capa && typeof capa.openPopup === 'function') {
                capa.openPopup(evento.latlng);
                return;
            }
        }
    }

    function enlazarClic(mapa) {
        if (ns._clicEnlazado || !mapa) {
            return;
        }
        ns._clicEnlazado = true;
        mapa.on('click', despacharClic);
    }

    function obtenerTotalCatalogo(def) {
        var totales = ns._totalesCatalogo;
        if (!totales || !Object.prototype.hasOwnProperty.call(totales, def.clave)) {
            return null;
        }
        var total = totales[def.clave];
        if (typeof total === 'number' && isFinite(total)) {
            return total;
        }
        return null;
    }

    function textoConteo(def, total) {
        if (total === 0) {
            return TEXTO_VACIO;
        }
        var totalCatalogo = obtenerTotalCatalogo(def);
        if (total >= def.limite) {
            // Nunca se afirma que haya más que el límite, solo que se alcanzó.
            var cargados = formatearNumero(total) + ' cargados — límite alcanzado; acerque el mapa';
            if (totalCatalogo !== null) {
                cargados += ' · ' + formatearNumero(totalCatalogo) + ' en total';
            }
            return cargados;
        }
        var visibles = formatearNumero(total) + ' visibles';
        if (totalCatalogo !== null && totalCatalogo !== total) {
            visibles += ' · ' + formatearNumero(totalCatalogo) + ' en total';
        }
        return visibles;
    }

    function nombresActivos() {
        var nombres = [];
        for (var i = 0; i < CAPAS.length; i++) {
            var estado = ESTADO[CAPAS[i].clave];
            if (estado && estado.activa) {
                nombres.push(CAPAS[i].etiqueta);
            }
        }
        return nombres;
    }

    // Región viva consolidada: se actualiza al iniciar, al activar/desactivar y
    // cuando una capa reporta Error o Aviso; nunca en movimientos ordinarios.
    function actualizarResumen() {
        var nodo = document.getElementById(ID_RESUMEN);
        if (!nodo) {
            return;
        }
        var nombres = nombresActivos();
        if (nombres.length === 0) {
            nodo.textContent = '';
            return;
        }
        var texto = 'Capas activas: ' + nombres.join(', ') + '.';
        for (var i = 0; i < CAPAS.length; i++) {
            var estado = ESTADO[CAPAS[i].clave];
            if (estado && estado.activa && estado.limiteAlcanzado) {
                texto += ' ' + CAPAS[i].etiqueta + ': se alcanzó el límite de '
                    + formatearNumero(CAPAS[i].limite) + ' elementos cargados.';
            }
        }
        nodo.textContent = texto;
    }

    function estadoActivas() {
        var activas = {};
        for (var i = 0; i < CAPAS.length; i++) {
            var estado = ESTADO[CAPAS[i].clave];
            activas[CAPAS[i].clave] = !!(estado && estado.activa);
        }
        return activas;
    }

    function actualizarLeyenda() {
        if (ns.legend && typeof ns.legend.actualizar === 'function') {
            ns.legend.actualizar(estadoActivas());
        }
    }

    function aplicar(def, estado, coleccion) {
        if (!estado.capa) {
            return;
        }
        estado.capa.clearLayers();
        estado.capa.addData(coleccion);

        // El contenedor <canvas> de un renderer nace en su primer render, no al
        // crear el renderer. Si solo se desactiva el puntero al activar la capa,
        // una capa que arranca vacia (o que se activa despues) conserva su canvas
        // interactivo, el mapa marca el evento como `_leaflet_disable_events` y el
        // despachador unico nunca recibe el clic. Reaplicarlo aqui, ya renderizado
        // el canvas, garantiza que el despachador sea la unica via del clic.
        desactivarPunteroDeRenderers();

        var total = (coleccion && coleccion.features) ? coleccion.features.length : 0;
        var alcanzabaLimite = estado.limiteAlcanzado;
        estado.conDatos = total > 0;
        estado.ultimoTotal = total;
        estado.limiteAlcanzado = total >= def.limite;

        if (total === 0) {
            establecerEstado(def, 'Vacía', 'vacia');
        } else if (estado.limiteAlcanzado) {
            establecerEstado(def, 'Aviso', 'aviso');
        } else {
            establecerEstado(def, 'Activa', 'activa');
        }
        establecerConteo(def, textoConteo(def, total));

        // El resumen solo cambia cuando el límite aparece o deja de alcanzarse.
        if (estado.limiteAlcanzado || alcanzabaLimite !== estado.limiteAlcanzado) {
            actualizarResumen();
        }
    }

    function cargar(mapa, def, estado) {
        if (!estado.activa) {
            // Desactivada: nunca se consulta la API.
            return;
        }
        if (!mapa || typeof mapa.getBounds !== 'function' || !ns.geojson
            || typeof ns.geojson.obtenerCapa !== 'function') {
            return;
        }

        estado.secuencia += 1;
        var secuencia = estado.secuencia;

        // Solo se muestra "Cargando..." mientras no haya datos ya pintados;
        // así una recarga por movimiento no parpadea sobre la capa visible.
        if (!estado.conDatos) {
            establecerEstado(def, TEXTO_CARGANDO, 'cargando');
        }

        ns.geojson.obtenerCapa(def.api, mapa.getBounds(), def.limite).then(function (resultado) {
            if (secuencia !== estado.secuencia) {
                // Respuesta antigua: una petición nueva ya salió.
                return;
            }
            if (!estado.activa) {
                // La capa se desactivó mientras la petición viajaba: la
                // respuesta no debe reescribir el estado del panel.
                return;
            }
            if (resultado.status === 401) {
                // api.js gestiona la sesión y guard.js redirige.
                return;
            }
            if (!resultado.ok) {
                establecerEstado(def, 'Error', 'error');
                establecerConteo(def, 'No fue posible cargar ' + def.etiqueta + '.');
                actualizarResumen();
                return;
            }
            aplicar(def, estado, resultado.coleccion);
        });
    }

    // Única vía de recarga por movimiento: recorre el registro y carga solo las
    // capas activas. El listener de moveend llama exclusivamente a esto.
    function cargarActivas(mapa) {
        for (var i = 0; i < CAPAS.length; i++) {
            var def = CAPAS[i];
            var estado = ESTADO[def.clave];
            if (estado && estado.activa) {
                cargar(mapa, def, estado);
            }
        }
    }

    // Un único listener para las cuatro capas; el guard evita duplicarlo.
    function enlazarMoveend(mapa) {
        if (ns._moveendEnlazado || !mapa) {
            return;
        }
        ns._moveendEnlazado = true;
        mapa.on('moveend', function () {
            if (ns._temporizadorDebounce) {
                clearTimeout(ns._temporizadorDebounce);
            }
            ns._temporizadorDebounce = setTimeout(function () {
                ns._temporizadorDebounce = null;
                cargarActivas(mapa);
            }, DEBOUNCE_MS);
        });
    }

    function activar(mapa, def) {
        var estado = ESTADO[def.clave];
        if (!estado) {
            return;
        }
        estado.activa = true;
        if (mapa && estado.capa && !mapa.hasLayer(estado.capa)) {
            estado.capa.addTo(mapa);
        }
        // El renderer de una capa puede aparecer al activarla por primera vez.
        desactivarPunteroDeRenderers();
        actualizarLeyenda();
        actualizarResumen();
        cargar(mapa, def, estado);
    }

    function desactivar(mapa, def) {
        var estado = ESTADO[def.clave];
        if (!estado) {
            return;
        }
        estado.activa = false;
        // Invalidar cualquier petición en vuelo: su respuesta ya no aplica.
        estado.secuencia += 1;
        if (ns._temporizadorDebounce) {
            clearTimeout(ns._temporizadorDebounce);
            ns._temporizadorDebounce = null;
        }
        if (mapa && estado.capa && mapa.hasLayer(estado.capa)) {
            mapa.removeLayer(estado.capa);
        }
        actualizarLeyenda();
        actualizarResumen();
        establecerEstado(def, TEXTO_INACTIVA, 'inactiva');
        establecerConteo(def, '');
    }

    function enlazarCheckbox(mapa, def) {
        var casilla = document.getElementById(def.idCheckbox);
        if (!casilla) {
            return;
        }
        var estado = ESTADO[def.clave];
        casilla.checked = !!(estado && estado.activa);

        if (casilla.getAttribute('data-capa-enlazada') === '1') {
            return;
        }
        casilla.setAttribute('data-capa-enlazada', '1');
        casilla.addEventListener('change', function () {
            if (casilla.checked) {
                activar(mapa, def);
            } else {
                desactivar(mapa, def);
            }
        });
    }

    // Recalcula el conteo de las capas que ya tienen datos cuando llega el
    // catálogo (los totales pasan a ser conocidos).
    function refrescarConteos() {
        for (var i = 0; i < CAPAS.length; i++) {
            var def = CAPAS[i];
            var estado = ESTADO[def.clave];
            if (estado && estado.conDatos && estado.activa) {
                establecerConteo(def, textoConteo(def, estado.ultimoTotal));
            }
        }
    }

    function cargarCatalogo() {
        if (!ns.geojson || typeof ns.geojson.obtenerCatalogo !== 'function') {
            return;
        }
        ns.geojson.obtenerCatalogo().then(function (resultado) {
            if (!resultado || !resultado.ok || !resultado.catalogo) {
                // Sin totales el visor sigue operativo: no se pinta error.
                return;
            }
            var totales = {};
            for (var i = 0; i < resultado.catalogo.length; i++) {
                var entrada = resultado.catalogo[i];
                if (!entrada || typeof entrada !== 'object') {
                    continue;
                }
                var nombre = entrada.Capa;
                var total = entrada.TotalRegistros;
                if (typeof nombre !== 'string' || typeof total !== 'number' || !isFinite(total)) {
                    continue;
                }
                for (var j = 0; j < CAPAS.length; j++) {
                    if (CAPAS[j].catalogo === nombre) {
                        totales[CAPAS[j].clave] = total;
                    }
                }
            }
            ns._totalesCatalogo = totales;
            refrescarConteos();
        });
    }

    function cargaInicial(mapa) {
        if (!ns.geojson || typeof ns.geojson.obtenerExtension !== 'function') {
            return;
        }
        ns.geojson.obtenerExtension().then(function (resultado) {
            if (resultado.status === 401) {
                // Sesión expirada: api.js y guard.js ya están resolviendo el
                // redirect; no se pisa ese flujo con un mensaje de error.
                return;
            }
            if (!resultado.ok || !resultado.extension) {
                // Se conserva el centro inicial del mapa base; el moveend queda
                // enlazado para reintentar en el próximo movimiento.
                for (var i = 0; i < CAPAS.length; i++) {
                    establecerEstado(CAPAS[i], 'Error', 'error');
                    establecerConteo(CAPAS[i], 'No fue posible cargar ' + CAPAS[i].etiqueta + '.');
                }
                actualizarResumen();
                enlazarMoveend(mapa);
                return;
            }

            var bounds = L.latLngBounds(
                [resultado.extension.minY, resultado.extension.minX],
                [resultado.extension.maxY, resultado.extension.maxX]
            );
            mapa.fitBounds(bounds, { padding: PADDING_FIT, animate: false });

            window.requestAnimationFrame(function () {
                cargarActivas(mapa);
                // El listener se enlaza después de la carga inicial para que el
                // moveend del fitBounds no dispare una petición duplicada.
                enlazarMoveend(mapa);
            });
        });
    }

    function iniciar() {
        if (ns._capasIniciadas) {
            return;
        }
        esperarMapa().then(function (mapa) {
            if (!mapa) {
                for (var k = 0; k < CAPAS.length; k++) {
                    establecerEstado(CAPAS[k], 'Error', 'error');
                    establecerConteo(CAPAS[k], 'No fue posible cargar ' + CAPAS[k].etiqueta + '.');
                }
                return;
            }
            esperarSesionValidada().then(function () {
                if (ns._capasIniciadas) {
                    return;
                }
                ns._capasIniciadas = true;

                // 1. Crear panes y capas Leaflet (una sola vez por capa).
                for (var i = 0; i < CAPAS.length; i++) {
                    var def = CAPAS[i];
                    var estado = ESTADO[def.clave];
                    if (!estado) {
                        continue;
                    }
                    if (PANES[def.tipo]) {
                        crearPane(mapa, PANES[def.tipo].nombre, PANES[def.tipo].zIndex);
                    }
                    crearCapa(mapa, def, estado);
                }

                // 2. Fijar las banderas activas ANTES de enlazar las casillas
                //    para que el control refleje el estado real.
                for (var j = 0; j < CAPAS.length; j++) {
                    var estadoJ = ESTADO[CAPAS[j].clave];
                    if (estadoJ) {
                        estadoJ.activa = CAPAS[j].activaInicial === true;
                    }
                }

                // 3. Enlazar casillas.
                for (var m = 0; m < CAPAS.length; m++) {
                    enlazarCheckbox(mapa, CAPAS[m]);
                }

                if (ns.legend && typeof ns.legend.inicializar === 'function') {
                    ns.legend.inicializar();
                }

                // 4. Agregar al mapa las capas activas y fijar badges iniciales.
                for (var n = 0; n < CAPAS.length; n++) {
                    var defN = CAPAS[n];
                    var estadoN = ESTADO[defN.clave];
                    if (!estadoN || !estadoN.capa) {
                        establecerEstado(defN, 'Error', 'error');
                        establecerConteo(defN, 'No fue posible cargar ' + defN.etiqueta + '.');
                        continue;
                    }
                    if (estadoN.activa) {
                        estadoN.capa.addTo(mapa);
                        establecerEstado(defN, TEXTO_CARGANDO, 'cargando');
                    } else {
                        establecerEstado(defN, TEXTO_INACTIVA, 'inactiva');
                        establecerConteo(defN, '');
                    }
                }

                // 5. Repartir el clic desde el mapa: los canvas no capturan el
                //    puntero, asi que todas las capas son clicables.
                desactivarPunteroDeRenderers();
                enlazarClic(mapa);

                actualizarLeyenda();
                actualizarResumen();
                cargarCatalogo();
                cargaInicial(mapa);
            });
        });
    }

    ns.capas = {
        inicializar: iniciar,
        activar: function (clave) {
            var def = buscarDefinicion(clave);
            if (def) {
                activar(obtenerMapa(), def);
            }
        },
        desactivar: function (clave) {
            var def = buscarDefinicion(clave);
            if (def) {
                desactivar(obtenerMapa(), def);
            }
        },
        estaActiva: function (clave) {
            var estado = ESTADO[clave];
            return !!(estado && estado.activa);
        },
        obtenerCapa: function (clave) {
            var estado = ESTADO[clave];
            return (estado && estado.capa) ? estado.capa : null;
        },
        cargarTodas: function () {
            cargarActivas(obtenerMapa());
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
})(window.VisorSIG);
