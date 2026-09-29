// FE-AUTH 1: cliente HTTP mínimo para la API de autenticación.
// Decide SIEMPRE por el código de estado HTTP. Un 401 puede llegar sin cuerpo
// JSON (lo emite el middleware del backend), por lo que nunca se asume que el
// cuerpo existe o es JSON. `peticion` nunca lanza: siempre resuelve con un
// objeto { ok, status, datos, error }.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var EVENTO_SESION_EXPIRADA = 'visorSIG:sesion-expirada';
    var RUTA_INICIAR = 'api/autenticacion/iniciar';
    var RUTA_ME = 'api/autenticacion/me';
    var RUTA_CERRAR = 'api/autenticacion/cerrar';

    var baseUrl = '';
    if (ns.config && typeof ns.config.apiBaseUrl === 'string') {
        baseUrl = ns.config.apiBaseUrl.replace(/\/+$/, '');
    }

    function construirUrl(ruta) {
        if (!baseUrl) {
            return '';
        }
        return baseUrl + '/' + ruta;
    }

    function leerCuerpo(respuesta) {
        if (respuesta.status === 204) {
            return Promise.resolve(null);
        }
        return respuesta.text().then(function (texto) {
            if (!texto) {
                return null;
            }
            try {
                return JSON.parse(texto);
            } catch (e) {
                // Cuerpo no JSON (por ejemplo problem+json no válido o vacío).
                return null;
            }
        }).catch(function () {
            return null;
        });
    }

    function notificarSesionExpirada() {
        if (ns.sesion && typeof ns.sesion.limpiar === 'function') {
            ns.sesion.limpiar();
        }
        try {
            document.dispatchEvent(new CustomEvent(EVENTO_SESION_EXPIRADA));
        } catch (e) {
            // Navegador sin soporte de CustomEvent: no se propaga el aviso.
        }
    }

    function peticion(ruta, opciones) {
        opciones = opciones || {};

        var metodo = opciones.metodo || 'GET';
        var cuerpo = opciones.cuerpo || null;
        var token = opciones.token || null;
        var protegida = opciones.protegida === true;
        var timeout = typeof opciones.timeout === 'number' ? opciones.timeout : 10000;

        var url = construirUrl(ruta);
        if (!url) {
            // Sin Api:BaseUrl configurada la API no se llama.
            return Promise.resolve({ ok: false, status: 0, datos: null, error: 'red' });
        }

        var headers = { 'Accept': 'application/json' };
        if (cuerpo) {
            headers['Content-Type'] = 'application/json';
        }
        if (token) {
            // El prefijo Bearer se agrega aquí y una sola vez.
            headers['Authorization'] = 'Bearer ' + token;
        }

        var controlador = null;
        var temporizador = null;
        var senal = null;

        if (typeof AbortController !== 'undefined') {
            controlador = new AbortController();
            senal = controlador.signal;
            temporizador = setTimeout(function () {
                controlador.abort();
            }, timeout);
        }

        var config = { method: metodo, headers: headers };
        if (cuerpo) {
            config.body = JSON.stringify(cuerpo);
        }
        if (senal) {
            config.signal = senal;
        }

        return new Promise(function (resolver) {
            fetch(url, config)
                .then(function (respuesta) {
                    return leerCuerpo(respuesta).then(function (datos) {
                        if (respuesta.status === 401 && protegida) {
                            // Sesión inválida en una llamada protegida.
                            notificarSesionExpirada();
                        }
                        resolver({
                            ok: respuesta.ok,
                            status: respuesta.status,
                            datos: datos,
                            error: null
                        });
                    });
                })
                .catch(function (error) {
                    if (error && error.name === 'AbortError') {
                        resolver({ ok: false, status: 0, datos: null, error: 'timeout' });
                        return;
                    }
                    resolver({ ok: false, status: 0, datos: null, error: 'red' });
                })
                .then(function () {
                    // Equivalente a finally: siempre se limpia el temporizador.
                    if (temporizador) {
                        clearTimeout(temporizador);
                        temporizador = null;
                    }
                });
        });
    }

    function iniciarSesion(login, password) {
        // Un 401 aquí significa credenciales incorrectas, no sesión expirada:
        // por eso `protegida` es false.
        return peticion(RUTA_INICIAR, {
            metodo: 'POST',
            cuerpo: { login: login, password: password },
            protegida: false
        });
    }

    function obtenerUsuarioActual(token) {
        return peticion(RUTA_ME, {
            metodo: 'GET',
            token: token,
            protegida: true
        });
    }

    function cerrarSesionRemota(token) {
        // Best-effort: el cierre local de la sesión ya limpia el estado del
        // navegador. Se marca como no protegida para que un 401 no dispare el
        // flujo de sesión expirada y evitar un doble redirect.
        return peticion(RUTA_CERRAR, {
            metodo: 'POST',
            token: token,
            protegida: false
        });
    }

    ns.api = {
        EVENTO_SESION_EXPIRADA: EVENTO_SESION_EXPIRADA,
        baseUrl: baseUrl,
        peticion: peticion,
        iniciarSesion: iniciarSesion,
        obtenerUsuarioActual: obtenerUsuarioActual,
        cerrarSesionRemota: cerrarSesionRemota
    };
})(window.VisorSIG);
