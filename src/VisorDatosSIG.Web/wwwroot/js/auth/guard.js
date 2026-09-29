// FE-AUTH 1: protección de páginas y administración del estado de sesión en la UI.
// La protección real está en la API; este módulo solo evita mostrar el visor sin
// una sesión validada contra GET /me (fail-closed si la API no responde).
// No usa innerHTML en ningún punto.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var temporizadorExpiracion = null;

    function rutaActual() {
        return window.location.pathname + window.location.search;
    }

    function redirigirALogin(returnUrl, motivo) {
        var destino = '/Cuenta/Login?returnUrl='
            + encodeURIComponent(returnUrl || '/Visor');
        if (motivo) {
            destino += '&motivo=' + encodeURIComponent(motivo);
        }
        window.location.href = destino;
    }

    function obtenerAviso() {
        return document.querySelector('[data-visor-aviso]')
            || document.getElementById('visor-aviso');
    }

    function limpiarAviso() {
        var aviso = obtenerAviso();
        if (aviso) {
            aviso.textContent = '';
            aviso.hidden = true;
        }
    }

    function mostrarAviso(opciones) {
        var aviso = obtenerAviso();
        if (!aviso) {
            // Sin contenedor de aviso no se puede informar, pero tampoco se
            // otorga acceso: se retira el estado de verificación y se detiene.
            document.body.classList.remove('sesion-verificando');
            return;
        }

        aviso.textContent =
            'No se pudo validar la sesión con el servidor. Verifique que la API esté disponible.';
        aviso.hidden = false;

        if (!aviso.querySelector('[data-reintentar]')) {
            var boton = document.createElement('button');
            boton.type = 'button';
            boton.setAttribute('data-reintentar', '');
            boton.className = 'btn-sesion';
            boton.textContent = 'Reintentar';
            boton.addEventListener('click', function () {
                if (boton.parentNode) {
                    boton.parentNode.removeChild(boton);
                }
                limpiarAviso();
                protegerPagina(opciones);
            });
            aviso.appendChild(boton);
        }
    }

    function actualizarHeader() {
        var sesion = ns.sesion;
        var autenticado = sesion.estaAutenticado() && !sesion.estaExpirada();

        var anonima = document.getElementById('sesion-anonima');
        var activa = document.getElementById('sesion-activa');
        var nombre = document.getElementById('sesion-nombre');
        var rol = document.getElementById('sesion-rol');

        if (anonima) {
            anonima.hidden = autenticado;
        }
        if (activa) {
            activa.hidden = !autenticado;
        }
        if (nombre) {
            nombre.textContent = sesion.nombreParaMostrar();
        }
        if (rol) {
            rol.textContent = 'rol: ' + sesion.rolesTexto();
        }

        var nodosNombre = document.querySelectorAll('[data-sesion-nombre]');
        for (var i = 0; i < nodosNombre.length; i++) {
            nodosNombre[i].textContent = sesion.nombreParaMostrar();
        }

        var bloques = document.querySelectorAll('[data-sesion-bloque]');
        for (var j = 0; j < bloques.length; j++) {
            bloques[j].hidden = !autenticado;
        }

        var anonimos = document.querySelectorAll('[data-sesion-anonimo]');
        for (var k = 0; k < anonimos.length; k++) {
            anonimos[k].hidden = autenticado;
        }
    }

    function iniciarAutoExpiracion() {
        // Idempotente: nunca se acumulan temporizadores.
        if (temporizadorExpiracion !== null) {
            clearTimeout(temporizadorExpiracion);
            temporizadorExpiracion = null;
        }

        var expiracion = ns.sesion.obtenerExpiracion();
        if (typeof expiracion !== 'number' || expiracion <= 0) {
            return;
        }

        var restante = expiracion - Date.now();
        if (restante <= 0) {
            expirarSesion();
            return;
        }

        temporizadorExpiracion = setTimeout(function () {
            temporizadorExpiracion = null;
            expirarSesion();
        }, restante);
    }

    function expirarSesion() {
        ns.sesion.limpiar();
        actualizarHeader();
        redirigirALogin(rutaActual(), 'expirada');
    }

    function cerrarSesion() {
        var token = ns.sesion.obtenerToken();
        var remoto;
        try {
            remoto = ns.api.cerrarSesionRemota(token);
        } catch (e) {
            remoto = Promise.resolve();
        }

        // Cierre best-effort: la sesión local SIEMPRE se limpia, sin importar
        // el resultado de la llamada a la API.
        Promise.resolve(remoto).catch(function () {
            return null;
        }).then(function () {
            ns.sesion.limpiar();
            actualizarHeader();
            window.location.href = '/Cuenta/Login?motivo=sesion-finalizada';
        });
    }

    function protegerPagina(opciones) {
        opciones = opciones || {};
        var returnUrl = opciones.returnUrl || rutaActual();

        if (ns.sesion && ns.api) {
            document.body.classList.add('sesion-verificando');

            if (!ns.sesion.estaAutenticado()) {
                limpiarAviso();
                redirigirALogin(returnUrl, null);
                return;
            }

            if (ns.sesion.estaExpirada()) {
                ns.sesion.limpiar();
                actualizarHeader();
                redirigirALogin(returnUrl, 'expirada');
                return;
            }

            var token = ns.sesion.obtenerToken();
            ns.api.obtenerUsuarioActual(token).then(function (resultado) {
                if (resultado.ok && resultado.status === 200 && resultado.datos) {
                    ns.sesion.actualizarUsuario(resultado.datos);
                    actualizarHeader();
                    iniciarAutoExpiracion();
                    limpiarAviso();
                    document.body.classList.remove('sesion-verificando');
                    return;
                }

                if (resultado.status === 401) {
                    ns.sesion.limpiar();
                    actualizarHeader();
                    redirigirALogin(returnUrl, 'expirada');
                    return;
                }

                // Decisión deliberada de seguridad (fail-closed): ante error de
                // red o timeout NO se borra la sesión, pero tampoco se permite el
                // uso hasta validarla contra /me.
                mostrarAviso(opciones);
            });
            return;
        }

        redirigirALogin(returnUrl, null);
    }

    function enlazarBotonCerrar() {
        var boton = document.getElementById('btn-cerrar-sesion');
        if (!boton || boton.getAttribute('data-visor-sig-enlazado') === '1') {
            return;
        }
        boton.setAttribute('data-visor-sig-enlazado', '1');
        boton.addEventListener('click', function (evento) {
            evento.preventDefault();
            cerrarSesion();
        });
    }

    function iniciar() {
        actualizarHeader();
        iniciarAutoExpiracion();
        enlazarBotonCerrar();
    }

    // Un 401 en una llamada de datos limpia la sesión y avisa aquí para que la
    // página no quede sin sesión ni sin explicación.
    if (ns.api && ns.api.EVENTO_SESION_EXPIRADA) {
        document.addEventListener(ns.api.EVENTO_SESION_EXPIRADA, function () {
            actualizarHeader();
            redirigirALogin(rutaActual(), 'expirada');
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }

    ns.guard = {
        protegerPagina: protegerPagina,
        actualizarHeader: actualizarHeader,
        iniciarAutoExpiracion: iniciarAutoExpiracion,
        cerrarSesion: cerrarSesion,
        redirigirALogin: redirigirALogin
    };
})(window.VisorSIG);
