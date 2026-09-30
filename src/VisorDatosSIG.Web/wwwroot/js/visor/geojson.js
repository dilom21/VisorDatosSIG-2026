// FE-SIG 2: consumo de los servicios GeoJSON de Códigos Fijos.
// Este módulo arma las consultas, llama a la API y valida lo mínimo
// indispensable. No contiene lógica de Leaflet ni manipulación del DOM.
// `peticion` nunca rechaza; aun así cada ruta resuelve siempre.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var RUTA_EXTENSION = 'api/capas/extension';
    var RUTA_GEOJSON = 'api/capas/codigosfijos/geojson';
    var CAPA = 'codigosfijos';
    var LIMITE_MAXIMO = 10000;

    function obtenerToken() {
        if (ns.sesion && typeof ns.sesion.obtenerToken === 'function') {
            return ns.sesion.obtenerToken();
        }
        return null;
    }

    function esNumero(valor) {
        return typeof valor === 'number' && isFinite(valor);
    }

    function esColeccionValida(datos) {
        if (!datos || typeof datos !== 'object') {
            return false;
        }
        if (datos.type !== 'FeatureCollection') {
            return false;
        }
        return Object.prototype.toString.call(datos.features) === '[object Array]';
    }

    function boundsValido(bounds) {
        return !!bounds
            && typeof bounds.getWest === 'function'
            && typeof bounds.getSouth === 'function'
            && typeof bounds.getEast === 'function'
            && typeof bounds.getNorth === 'function';
    }

    // El límite se acota entre 1 y LIMITE_MAXIMO. Cuando no es un número
    // positivo se usa LIMITE_MAXIMO; nunca se confía en el valor recibido.
    function normalizarLimite(limit) {
        if (typeof limit !== 'number' || !isFinite(limit) || limit <= 0) {
            return LIMITE_MAXIMO;
        }
        var entero = Math.floor(limit);
        if (entero < 1) {
            return 1;
        }
        if (entero > LIMITE_MAXIMO) {
            return LIMITE_MAXIMO;
        }
        return entero;
    }

    function obtenerExtensionCodigosFijos() {
        return new Promise(function (resolver) {
            var ruta = RUTA_EXTENSION + '?capa=' + encodeURIComponent(CAPA);
            try {
                ns.api.peticion(ruta, {
                    metodo: 'GET',
                    token: obtenerToken(),
                    protegida: true
                }).then(function (resultado) {
                    var datos = (resultado && resultado.datos) ? resultado.datos : null;
                    if (resultado && resultado.ok && resultado.status === 200 && datos
                        && esNumero(datos.minX) && esNumero(datos.minY)
                        && esNumero(datos.maxX) && esNumero(datos.maxY)) {
                        resolver({
                            ok: true,
                            status: resultado.status,
                            error: null,
                            extension: {
                                minX: datos.minX,
                                minY: datos.minY,
                                maxX: datos.maxX,
                                maxY: datos.maxY
                            }
                        });
                        return;
                    }
                    resolver({
                        ok: false,
                        status: resultado ? resultado.status : 0,
                        error: (resultado && resultado.error) ? resultado.error : 'contrato',
                        extension: null
                    });
                }).catch(function () {
                    resolver({ ok: false, status: 0, error: 'contrato', extension: null });
                });
            } catch (e) {
                resolver({ ok: false, status: 0, error: 'contrato', extension: null });
            }
        });
    }

    function obtenerCodigosFijos(bounds, limit) {
        return new Promise(function (resolver) {
            if (!ns.api || typeof ns.api.peticion !== 'function') {
                resolver({ ok: false, status: 0, error: 'contrato', coleccion: null });
                return;
            }
            if (!boundsValido(bounds)) {
                // Sin extensión válida no hay consulta posible: se resuelve sin lanzar.
                resolver({ ok: false, status: 0, error: 'contrato', coleccion: null });
                return;
            }

            var limite = normalizarLimite(limit);
            var consulta = '?minX=' + encodeURIComponent(bounds.getWest())
                + '&minY=' + encodeURIComponent(bounds.getSouth())
                + '&maxX=' + encodeURIComponent(bounds.getEast())
                + '&maxY=' + encodeURIComponent(bounds.getNorth())
                + '&limit=' + encodeURIComponent(limite);

            try {
                ns.api.peticion(RUTA_GEOJSON + consulta, {
                    metodo: 'GET',
                    token: obtenerToken(),
                    protegida: true
                }).then(function (resultado) {
                    if (resultado && resultado.ok && resultado.status === 200
                        && esColeccionValida(resultado.datos)) {
                        resolver({
                            ok: true,
                            status: resultado.status,
                            error: null,
                            coleccion: resultado.datos
                        });
                        return;
                    }
                    if (resultado && resultado.ok && resultado.status === 200) {
                        resolver({
                            ok: false,
                            status: resultado.status,
                            error: 'contrato',
                            coleccion: null
                        });
                        return;
                    }
                    resolver({
                        ok: false,
                        status: resultado ? resultado.status : 0,
                        error: (resultado && resultado.error) ? resultado.error : 'contrato',
                        coleccion: null
                    });
                }).catch(function () {
                    resolver({ ok: false, status: 0, error: 'contrato', coleccion: null });
                });
            } catch (e) {
                resolver({ ok: false, status: 0, error: 'contrato', coleccion: null });
            }
        });
    }

    ns.geojson = {
        LIMITE_MAXIMO: LIMITE_MAXIMO,
        RUTA_EXTENSION: RUTA_EXTENSION,
        RUTA_GEOJSON: RUTA_GEOJSON,
        obtenerExtensionCodigosFijos: obtenerExtensionCodigosFijos,
        obtenerCodigosFijos: obtenerCodigosFijos
    };
})(window.VisorSIG);
