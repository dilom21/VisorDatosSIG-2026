// FE-SIG 2 y 3: consumo de los servicios GeoJSON de las capas del visor.
// Este módulo arma las consultas, llama a la API y valida lo mínimo
// indispensable. No contiene lógica de Leaflet ni manipulación del DOM.
// `peticion` nunca rechaza; aun así cada ruta resuelve siempre.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var RUTA_CATALOGO = 'api/capas';
    var RUTA_EXTENSION = 'api/capas/extension';
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

    // El catálogo debe ser un arreglo donde cada entrada es un objeto; ninguna
    // entrada se inspecciona más a fondo aquí, eso es responsabilidad del visor.
    function esCatalogoValido(datos) {
        if (Object.prototype.toString.call(datos) !== '[object Array]') {
            return false;
        }
        for (var i = 0; i < datos.length; i++) {
            if (!datos[i] || typeof datos[i] !== 'object') {
                return false;
            }
        }
        return true;
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

    function obtenerCatalogo() {
        return new Promise(function (resolver) {
            if (!ns.api || typeof ns.api.peticion !== 'function') {
                resolver({ ok: false, status: 0, error: 'contrato', catalogo: null });
                return;
            }
            try {
                ns.api.peticion(RUTA_CATALOGO, {
                    metodo: 'GET',
                    token: obtenerToken(),
                    protegida: true
                }).then(function (resultado) {
                    if (resultado && resultado.ok && resultado.status === 200
                        && esCatalogoValido(resultado.datos)) {
                        resolver({
                            ok: true,
                            status: resultado.status,
                            error: null,
                            catalogo: resultado.datos
                        });
                        return;
                    }
                    if (resultado && resultado.ok && resultado.status === 200) {
                        resolver({
                            ok: false,
                            status: resultado.status,
                            error: 'contrato',
                            catalogo: null
                        });
                        return;
                    }
                    resolver({
                        ok: false,
                        status: resultado ? resultado.status : 0,
                        error: (resultado && resultado.error) ? resultado.error : 'contrato',
                        catalogo: null
                    });
                }).catch(function () {
                    resolver({ ok: false, status: 0, error: 'contrato', catalogo: null });
                });
            } catch (e) {
                resolver({ ok: false, status: 0, error: 'contrato', catalogo: null });
            }
        });
    }

    // Sin `capa` se consulta la extensión global de las cuatro capas; con una
    // capa no vacía se consulta la extensión de esa capa.
    function obtenerExtension(capa) {
        return new Promise(function (resolver) {
            if (!ns.api || typeof ns.api.peticion !== 'function') {
                resolver({ ok: false, status: 0, error: 'contrato', extension: null });
                return;
            }

            var ruta = RUTA_EXTENSION;
            if (typeof capa === 'string' && capa !== '') {
                ruta += '?capa=' + encodeURIComponent(capa);
            }

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

    function obtenerCapa(capa, bounds, limit) {
        return new Promise(function (resolver) {
            if (!ns.api || typeof ns.api.peticion !== 'function') {
                resolver({ ok: false, status: 0, error: 'contrato', coleccion: null });
                return;
            }
            if (typeof capa !== 'string' || capa === '') {
                resolver({ ok: false, status: 0, error: 'contrato', coleccion: null });
                return;
            }
            if (!boundsValido(bounds)) {
                // Sin extensión válida no hay consulta posible: se resuelve sin lanzar.
                resolver({ ok: false, status: 0, error: 'contrato', coleccion: null });
                return;
            }

            var limite = normalizarLimite(limit);
            var ruta = 'api/capas/' + encodeURIComponent(capa) + '/geojson';
            var consulta = '?minX=' + encodeURIComponent(bounds.getWest())
                + '&minY=' + encodeURIComponent(bounds.getSouth())
                + '&maxX=' + encodeURIComponent(bounds.getEast())
                + '&maxY=' + encodeURIComponent(bounds.getNorth())
                + '&limit=' + encodeURIComponent(limite);

            try {
                ns.api.peticion(ruta + consulta, {
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

    // Envoltorios de compatibilidad con FE-SIG 2: siguen siendo válidos y evitan
    // romper llamadas antiguas mientras el visor migra al API generalizado.
    function obtenerExtensionCodigosFijos() {
        return obtenerExtension('codigosfijos');
    }

    function obtenerCodigosFijos(bounds, limit) {
        return obtenerCapa('codigosfijos', bounds, limit);
    }

    ns.geojson = {
        LIMITE_MAXIMO: LIMITE_MAXIMO,
        RUTA_CATALOGO: RUTA_CATALOGO,
        RUTA_EXTENSION: RUTA_EXTENSION,
        obtenerCatalogo: obtenerCatalogo,
        obtenerExtension: obtenerExtension,
        obtenerCapa: obtenerCapa,
        obtenerExtensionCodigosFijos: obtenerExtensionCodigosFijos,
        obtenerCodigosFijos: obtenerCodigosFijos
    };
})(window.VisorSIG);
