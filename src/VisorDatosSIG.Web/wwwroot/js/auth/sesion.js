// FE-AUTH 1: administración de la sesión del usuario en el navegador.
// La sesión vive únicamente en sessionStorage (se pierde al cerrar la pestaña).
// No se usa localStorage, cookies, variables globales persistentes, archivos
// ni el servidor Web. Nunca se registra el token en la consola.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var CLAVE = 'visorSIG.sesion';

    function leer() {
        try {
            var bruto = window.sessionStorage.getItem(CLAVE);
            if (!bruto) {
                return null;
            }
            var datos = JSON.parse(bruto);
            if (!datos || typeof datos !== 'object') {
                return null;
            }
            return datos;
        } catch (e) {
            // sessionStorage no disponible o contenido corrupto:
            // la sesión se comporta como "no autenticado".
            return null;
        }
    }

    function escribir(datos) {
        try {
            window.sessionStorage.setItem(CLAVE, JSON.stringify(datos));
            return true;
        } catch (e) {
            return false;
        }
    }

    function guardar(respuesta) {
        if (!respuesta || typeof respuesta !== 'object') {
            return;
        }

        var usuario = (respuesta.usuario && typeof respuesta.usuario === 'object')
            ? respuesta.usuario
            : null;

        var segundos = Number(respuesta.expiresIn);

        // Criterio explícito: si expiresIn no es un número mayor que cero no se
        // inventa una duración por defecto. La sesión se guarda con
        // expiresAt = 0 y se trata como expirada de inmediato.
        var expiracion = segundos > 0 ? Date.now() + segundos * 1000 : 0;

        escribir({
            accessToken: typeof respuesta.accessToken === 'string' ? respuesta.accessToken : '',
            usuario: usuario,
            expiresAt: expiracion
        });
    }

    function obtenerToken() {
        var datos = leer();
        if (datos && typeof datos.accessToken === 'string' && datos.accessToken !== '') {
            return datos.accessToken;
        }
        return null;
    }

    function obtenerUsuario() {
        var datos = leer();
        if (datos && datos.usuario && typeof datos.usuario === 'object') {
            return datos.usuario;
        }
        return null;
    }

    function obtenerExpiracion() {
        var datos = leer();
        if (datos && typeof datos.expiresAt === 'number') {
            return datos.expiresAt;
        }
        return null;
    }

    function estaAutenticado() {
        return !!obtenerToken() && !!obtenerUsuario();
    }

    function estaExpirada() {
        if (!estaAutenticado()) {
            return true;
        }
        var expiracion = obtenerExpiracion();
        if (expiracion === null) {
            return true;
        }
        return Date.now() >= expiracion;
    }

    function actualizarUsuario(usuario) {
        if (!usuario || typeof usuario !== 'object') {
            return;
        }
        var datos = leer();
        if (!datos) {
            return;
        }
        // Conserva el token y la expiración vigentes; reemplaza solo el usuario.
        datos.usuario = usuario;
        escribir(datos);
    }

    function limpiar() {
        try {
            window.sessionStorage.removeItem(CLAVE);
        } catch (e) {
            // Sin sessionStorage disponible no hay nada que limpiar.
        }
    }

    function nombreParaMostrar() {
        var usuario = obtenerUsuario();
        if (!usuario) {
            return '';
        }
        if (typeof usuario.nombre === 'string' && usuario.nombre !== '') {
            return usuario.nombre;
        }
        if (typeof usuario.login === 'string') {
            return usuario.login;
        }
        return '';
    }

    function rolesTexto() {
        var usuario = obtenerUsuario();
        if (usuario && Object.prototype.toString.call(usuario.roles) === '[object Array]'
            && usuario.roles.length > 0) {
            return usuario.roles.join(', ');
        }
        return 'sin rol';
    }

    ns.sesion = {
        CLAVE: CLAVE,
        guardar: guardar,
        obtenerToken: obtenerToken,
        obtenerUsuario: obtenerUsuario,
        obtenerExpiracion: obtenerExpiracion,
        estaAutenticado: estaAutenticado,
        estaExpirada: estaExpirada,
        actualizarUsuario: actualizarUsuario,
        limpiar: limpiar,
        nombreParaMostrar: nombreParaMostrar,
        rolesTexto: rolesTexto
    };
})(window.VisorSIG);
