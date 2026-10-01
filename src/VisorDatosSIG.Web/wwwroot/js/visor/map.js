// Inicialización del mapa Leaflet.
// FE-SIG 1: creación del mapa y del mapa base OpenStreetMap.
// No contiene lógica de capas, datos, identificación ni leyenda.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var CENTRO_INICIAL = [-16.3811, -60.9636];
    var ZOOM_INICIAL = 13;
    var URL_TILES_OSM = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
    var ATRIBUCION_OSM = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';
    var MAX_ZOOM_OSM = 19;

    // El estado vive en el namespace para que una segunda ejecución del script
    // (carga duplicada) reutilice el mapa en lugar de crear otro sobre #map.
    var mapa = ns._mapa || null;
    var capaBase = ns._capaBase || null;

    function obtenerContenedor() {
        return document.getElementById('map');
    }

    function estaInicializado() {
        return !!ns._mapa;
    }

    function crearCapaBase() {
        return L.tileLayer(URL_TILES_OSM, {
            maxZoom: MAX_ZOOM_OSM,
            attribution: ATRIBUCION_OSM
        });
    }

    function inicializar(opciones) {
        if (estaInicializado()) {
            mapa = ns._mapa;
            capaBase = ns._capaBase;
            return mapa;
        }

        if (typeof L === 'undefined') {
            return null;
        }

        var contenedor = obtenerContenedor();
        if (!contenedor || contenedor._leaflet_id) {
            return null;
        }

        opciones = opciones || {};

        mapa = L.map(contenedor, {
            center: opciones.center || CENTRO_INICIAL,
            zoom: opciones.zoom || ZOOM_INICIAL
        });

        capaBase = crearCapaBase();
        capaBase.addTo(mapa);

        // CU11 (RF-VIS-05): Escala gráfica visible en el visor
        if (typeof L.control.scale === 'function') {
            L.control.scale({ imperial: false, metric: true, position: 'bottomleft' }).addTo(mapa);
        }

        // CU11 (RF-VIS-05): Botón de navegación rápida a Extensión General integrado en mapa
        var ControlExtensionGeneral = L.Control.extend({
            options: { position: 'topleft' },
            onAdd: function () {
                var contenedor = document.createElement('div');
                contenedor.className = 'leaflet-bar leaflet-control';

                var boton = document.createElement('a');
                boton.href = '#';
                boton.className = 'leaflet-control-extension-btn';
                boton.title = 'Ajustar a extensión general (todo el catastro)';
                boton.setAttribute('role', 'button');
                boton.setAttribute('aria-label', 'Extensión general');

                var icono = document.createElement('span');
                icono.className = 'leaflet-control-extension-icono';
                icono.textContent = '⛶';
                icono.setAttribute('aria-hidden', 'true');
                boton.appendChild(icono);

                L.DomEvent.disableClickPropagation(contenedor);
                L.DomEvent.disableScrollPropagation(contenedor);

                boton.addEventListener('click', function (e) {
                    if (e && e.preventDefault) { e.preventDefault(); }
                    if (ns.capas && typeof ns.capas.ajustarExtensionGeneral === 'function') {
                        ns.capas.ajustarExtensionGeneral();
                    }
                });

                contenedor.appendChild(boton);
                return contenedor;
            }
        });
        new ControlExtensionGeneral().addTo(mapa);

        // CU11: Indicador de nivel de zoom y estado espacial
        var ControlIndicadorZoom = L.Control.extend({
            options: { position: 'bottomright' },
            onAdd: function () {
                var contenedor = document.createElement('div');
                contenedor.className = 'visor-map-status';
                contenedor.id = 'visor-map-status';

                var texto = document.createElement('span');
                texto.className = 'visor-map-status__zoom';
                texto.id = 'visor-map-status-zoom';
                texto.textContent = 'Zoom: ' + mapa.getZoom();
                contenedor.appendChild(texto);

                function actualizar() {
                    texto.textContent = 'Zoom: ' + mapa.getZoom();
                }

                mapa.on('zoomend', actualizar);
                return contenedor;
            }
        });
        new ControlIndicadorZoom().addTo(mapa);

        ns._mapa = mapa;
        ns._capaBase = capaBase;
        return mapa;
    }

    function obtenerMapa() {
        return ns._mapa || mapa || null;
    }

    function obtenerCapaBase() {
        return ns._capaBase || capaBase || null;
    }

    function refrescarTamano() {
        var instancia = obtenerMapa();
        if (instancia) {
            instancia.invalidateSize();
        }
    }

    ns.mapa = {
        centroInicial: CENTRO_INICIAL,
        zoomInicial: ZOOM_INICIAL,
        urlTilesOsm: URL_TILES_OSM,
        inicializar: inicializar,
        obtener: obtenerMapa,
        obtenerCapaBase: obtenerCapaBase,
        estaInicializado: estaInicializado,
        refrescarTamano: refrescarTamano
    };

    function iniciar() {
        inicializar();
        if (!ns._resizeEnlazado) {
            window.addEventListener('resize', refrescarTamano);
            ns._resizeEnlazado = true;
        }
        if (typeof requestAnimationFrame === 'function') {
            requestAnimationFrame(refrescarTamano);
        }
        setTimeout(refrescarTamano, 250);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
})(window.VisorSIG);
