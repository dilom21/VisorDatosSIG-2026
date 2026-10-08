// CU07 - Gestión de mapas base.
// Permite alternar entre OpenStreetMap y OpenTopoMap
// sin afectar las capas geográficas del visor.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var CLAVE_OSM = 'osm';
    var CLAVE_TOPOGRAFICO = 'topografico';

    var MAPAS_BASE = {
        osm: {
            clave: CLAVE_OSM,
            nombre: 'OpenStreetMap',
            url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
            opciones: {
                maxZoom: 19,
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            }
        },

        topografico: {
            clave: CLAVE_TOPOGRAFICO,
            nombre: 'OpenTopoMap',
            url: 'https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png',
            opciones: {
                maxNativeZoom: 17,
                maxZoom: 19,
                attribution:
                    'Datos: &copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors, SRTM | ' +
                    'Mapa: &copy; <a href="https://opentopomap.org">OpenTopoMap</a> (CC-BY-SA)'
            }
        }
    };

    var capas = ns._mapasBase || {};
    var claveActiva = ns._mapaBaseActivo || CLAVE_OSM;

    function obtenerMapa() {
        return ns.mapa && typeof ns.mapa.obtener === 'function'
            ? ns.mapa.obtener()
            : null;
    }

    function crearCapas() {
        if (typeof L === 'undefined') {
            return;
        }

        if (!capas.osm) {
            // Reutilizar la capa OSM que map.js ya creó.
            if (ns.mapa && typeof ns.mapa.obtenerCapaBase === 'function') {
                capas.osm = ns.mapa.obtenerCapaBase();
            }

            if (!capas.osm) {
                capas.osm = L.tileLayer(
                    MAPAS_BASE.osm.url,
                    MAPAS_BASE.osm.opciones
                );
            }
        }

        if (!capas.topografico) {
            capas.topografico = L.tileLayer(
                MAPAS_BASE.topografico.url,
                MAPAS_BASE.topografico.opciones
            );
        }

        ns._mapasBase = capas;
    }

    function cambiar(clave) {
        var mapa = obtenerMapa();

        if (!mapa || !MAPAS_BASE[clave]) {
            return false;
        }

        crearCapas();

        Object.keys(capas).forEach(function (claveCapa) {
            var capa = capas[claveCapa];

            if (capa && mapa.hasLayer(capa)) {
                mapa.removeLayer(capa);
            }
        });

        var capaSeleccionada = capas[clave];

        if (!capaSeleccionada) {
            return false;
        }

        capaSeleccionada.addTo(mapa);

        claveActiva = clave;
        ns._mapaBaseActivo = clave;

        document.dispatchEvent(new CustomEvent('visor:mapabasecambiado', {
            detail: {
                clave: clave,
                nombre: MAPAS_BASE[clave].nombre
            }
        }));

        return true;
    }

    function obtenerActivo() {
        return ns._mapaBaseActivo || claveActiva;
    }

    function obtenerCatalogo() {
        return MAPAS_BASE;
    }

    function enlazarSelector() {
    var selector = document.getElementById('selector-mapa-base');

    if (!selector || selector.dataset.enlazado === 'true') {
        return;
    }

    selector.value = obtenerActivo();

    selector.addEventListener('change', function () {
        var claveSeleccionada = selector.value;

        if (!cambiar(claveSeleccionada)) {
            selector.value = obtenerActivo();
        }
    });

    document.addEventListener('visor:mapabasecambiado', function (evento) {
        if (evento.detail && evento.detail.clave) {
            selector.value = evento.detail.clave;
        }
    });

    selector.dataset.enlazado = 'true';
}

    ns.mapasBase = {
        cambiar: cambiar,
        obtenerActivo: obtenerActivo,
        obtenerCatalogo: obtenerCatalogo
    };

    function iniciar() {
    crearCapas();
    enlazarSelector();
}

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }

})(window.VisorSIG);