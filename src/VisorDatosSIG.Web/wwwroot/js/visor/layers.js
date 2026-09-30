// FE-SIG 2: administración de la capa de Códigos Fijos.
// Orquesta la carga (extensión + GeoJSON), el estado visible en el panel, el
// control de activación y la recarga por movimiento del mapa. La capa Leaflet
// se crea UNA sola vez y se reutiliza; en cada moveend solo se piden datos.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var ID_CHECKBOX = 'capa-codigos-fijos';
    var ID_ESTADO = 'estado-codigos-fijos';
    var ID_CONTEO = 'conteo-codigos-fijos';
    var DEBOUNCE_MS = 250;
    var LIMITE = 10000;
    var PADDING_FIT = [20, 20];
    var TEXTO_ERROR = 'No fue posible cargar Códigos Fijos.';
    var TEXTO_VACIO = 'Sin puntos en esta extensión.';
    var TEXTO_CARGANDO = 'Cargando...';

    function obtenerMapa() {
        return (ns.mapa && typeof ns.mapa.obtener === 'function') ? ns.mapa.obtener() : null;
    }

    function establecerEstado(texto, modificador) {
        var nodo = document.getElementById(ID_ESTADO);
        if (!nodo) {
            return;
        }
        nodo.textContent = texto;
        nodo.className = 'visor-badge visor-badge--' + modificador;
    }

    function establecerConteo(texto) {
        var nodo = document.getElementById(ID_CONTEO);
        if (nodo) {
            nodo.textContent = texto;
        }
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

    function aplicar(coleccion) {
        if (!ns._capaCodigosFijos) {
            return;
        }
        ns._capaCodigosFijos.clearLayers();
        ns._capaCodigosFijos.addData(coleccion);

        var total = (coleccion && coleccion.features) ? coleccion.features.length : 0;
        ns._capaCodigosFijosConDatos = total > 0;

        if (total === 0) {
            establecerEstado('Sin puntos', 'vacia');
            establecerConteo(TEXTO_VACIO);
            return;
        }
        establecerEstado('Activa', 'activa');
        establecerConteo(total + ' puntos visibles');
    }

    function cargar(mapa) {
        if (!ns._capaCodigosFijosActiva) {
            // Desactivada: nunca se consulta la API.
            return;
        }
        if (!mapa || typeof mapa.getBounds !== 'function' || !ns.geojson) {
            return;
        }

        ns._secuenciaCarga = (ns._secuenciaCarga || 0) + 1;
        var secuencia = ns._secuenciaCarga;

        // Solo se muestra "Cargando..." mientras no haya datos ya pintados;
        // así una recarga por movimiento no parpadea sobre la capa visible.
        if (!ns._capaCodigosFijosConDatos) {
            establecerEstado(TEXTO_CARGANDO, 'cargando');
        }

        ns.geojson.obtenerCodigosFijos(mapa.getBounds(), LIMITE).then(function (resultado) {
            if (secuencia !== ns._secuenciaCarga) {
                // Respuesta antigua: una petición nueva ya salió.
                return;
            }
            if (!ns._capaCodigosFijosActiva) {
                // La capa se desactivó mientras la petición viajaba: la
                // respuesta no debe reescribir el estado del panel.
                return;
            }
            if (resultado.status === 401) {
                // api.js gestiona la sesión y guard.js redirige.
                return;
            }
            if (!resultado.ok) {
                establecerEstado('Error', 'error');
                establecerConteo(TEXTO_ERROR);
                return;
            }
            aplicar(resultado.coleccion);
        });
    }

    function enlazarMoveend(mapa) {
        if (ns._moveendEnlazado) {
            return;
        }
        ns._moveendEnlazado = true;
        mapa.on('moveend', function () {
            if (ns._temporizadorDebounce) {
                clearTimeout(ns._temporizadorDebounce);
            }
            ns._temporizadorDebounce = setTimeout(function () {
                ns._temporizadorDebounce = null;
                cargar(mapa);
            }, DEBOUNCE_MS);
        });
    }

    function activar(mapa) {
        if (!mapa || !ns._capaCodigosFijos) {
            return;
        }
        ns._capaCodigosFijosActiva = true;
        if (!mapa.hasLayer(ns._capaCodigosFijos)) {
            ns._capaCodigosFijos.addTo(mapa);
        }
        if (ns.legend) {
            ns.legend.mostrar();
        }
        cargar(mapa);
    }

    function desactivar(mapa) {
        ns._capaCodigosFijosActiva = false;
        // Invalidar cualquier petición en vuelo: su respuesta ya no aplica.
        ns._secuenciaCarga = (ns._secuenciaCarga || 0) + 1;
        if (ns._temporizadorDebounce) {
            clearTimeout(ns._temporizadorDebounce);
            ns._temporizadorDebounce = null;
        }
        if (mapa && ns._capaCodigosFijos && mapa.hasLayer(ns._capaCodigosFijos)) {
            mapa.removeLayer(ns._capaCodigosFijos);
        }
        if (ns.legend) {
            ns.legend.ocultar();
        }
        establecerEstado('Inactiva', 'inactiva');
        establecerConteo('');
    }

    function enlazarCheckbox(mapa) {
        var casilla = document.getElementById(ID_CHECKBOX);
        if (!casilla) {
            return;
        }
        casilla.checked = !!ns._capaCodigosFijosActiva;

        if (casilla.getAttribute('data-capa-enlazada') === '1') {
            return;
        }
        casilla.setAttribute('data-capa-enlazada', '1');
        casilla.addEventListener('change', function () {
            if (casilla.checked) {
                activar(mapa);
            } else {
                desactivar(mapa);
            }
        });
    }

    function crearCapa(mapa) {
        if (ns._capaCodigosFijos) {
            return;
        }
        if (typeof L === 'undefined' || !ns.markers) {
            return;
        }
        ns._capaCodigosFijos = L.geoJSON(
            { type: 'FeatureCollection', features: [] },
            {
                pointToLayer: function (feature, latlng) {
                    return ns.markers.crearMarker(feature, latlng);
                },
                renderer: ns.markers.obtenerRenderer()
            }
        );
    }

    function cargaInicial(mapa) {
        ns.geojson.obtenerExtensionCodigosFijos().then(function (r) {
            if (r.status === 401) {
                // Sesión expirada: api.js y guard.js ya están resolviendo el
                // redirect; no se pisa ese flujo con un mensaje de error.
                return;
            }
            if (!r.ok || !r.extension) {
                // Se conserva el centro inicial del mapa base; el moveend queda
                // enlazado para reintentar en el próximo movimiento.
                establecerEstado('Error', 'error');
                establecerConteo(TEXTO_ERROR);
                enlazarMoveend(mapa);
                return;
            }

            var bounds = L.latLngBounds(
                [r.extension.minY, r.extension.minX],
                [r.extension.maxY, r.extension.maxX]
            );
            mapa.fitBounds(bounds, { padding: PADDING_FIT, animate: false });

            window.requestAnimationFrame(function () {
                cargar(mapa);
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
                establecerEstado('Error', 'error');
                establecerConteo(TEXTO_ERROR);
                return;
            }
            esperarSesionValidada().then(function () {
                if (ns._capasIniciadas) {
                    return;
                }
                ns._capasIniciadas = true;
                crearCapa(mapa);
                if (!ns._capaCodigosFijos) {
                    establecerEstado('Error', 'error');
                    establecerConteo(TEXTO_ERROR);
                    return;
                }
                // La capa inicia ACTIVA: el estado se fija antes de enlazar la
                // casilla para que el control refleje el estado real.
                ns._capaCodigosFijosActiva = true;
                enlazarCheckbox(mapa);
                if (ns.legend) {
                    ns.legend.inicializar();
                }
                ns._capaCodigosFijos.addTo(mapa);
                if (ns.legend) {
                    ns.legend.mostrar();
                }
                establecerEstado(TEXTO_CARGANDO, 'cargando');
                cargaInicial(mapa);
            });
        });
    }

    ns.capas = {
        inicializar: iniciar,
        activar: activar,
        desactivar: desactivar,
        estaActiva: function () {
            return !!ns._capaCodigosFijosActiva;
        },
        obtenerCapa: function () {
            return ns._capaCodigosFijos || null;
        },
        cargar: cargar
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
})(window.VisorSIG);
