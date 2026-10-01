// Preferencia de tema claro / oscuro del workspace autenticado.
//
// La preferencia se guarda en localStorage porque no es información sensible: el
// token JWT sigue viviendo exclusivamente en sessionStorage (sesion.js) y este
// módulo nunca lo lee, lo copia ni lo mueve.
//
// El tema se aplica sobre <html data-theme="dark"> y los estilos viven en
// wwwroot/css/theme.css. El atributo se fija antes del primer pintado desde el
// <script> en línea de _AppLayout para evitar el destello claro/oscuro.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var CLAVE = 'visorSIG.tema';
    var OSCURO = 'oscuro';
    var CLARO = 'claro';
    var ATRIBUTO = 'data-theme';
    var VALOR_OSCURO = 'dark';

    function leerPreferencia() {
        try {
            return window.localStorage.getItem(CLAVE) === OSCURO ? OSCURO : CLARO;
        } catch (e) {
            // localStorage no disponible: se conserva el tema claro.
            return CLARO;
        }
    }

    function guardarPreferencia(tema) {
        try {
            window.localStorage.setItem(CLAVE, tema);
        } catch (e) {
            // Sin almacenamiento la preferencia dura solo esta página.
        }
    }

    function aplicar(tema) {
        if (tema === OSCURO) {
            document.documentElement.setAttribute(ATRIBUTO, VALOR_OSCURO);
            return;
        }

        document.documentElement.removeAttribute(ATRIBUTO);
    }

    function actualizarBoton(boton, tema) {
        if (!boton) {
            return;
        }

        var oscuro = tema === OSCURO;
        var texto = oscuro ? 'Modo claro' : 'Modo oscuro';
        var etiqueta = oscuro
            ? 'Desactivar el modo oscuro'
            : 'Activar el modo oscuro';

        boton.setAttribute('aria-pressed', oscuro ? 'true' : 'false');
        boton.setAttribute('title', etiqueta);
        boton.setAttribute('aria-label', etiqueta);

        var nodoTexto = boton.querySelector('.sidebar__accion-texto');
        if (nodoTexto) {
            nodoTexto.textContent = texto;
        }
    }

    function enlazar() {
        var boton = document.getElementById('btn-tema');
        if (!boton) {
            return;
        }

        // El estado inicial ya lo pintó el script en línea del layout.
        var tema = document.documentElement.getAttribute(ATRIBUTO) === VALOR_OSCURO
            ? OSCURO
            : CLARO;

        actualizarBoton(boton, tema);

        boton.addEventListener('click', function () {
            tema = tema === OSCURO ? CLARO : OSCURO;
            aplicar(tema);
            guardarPreferencia(tema);
            actualizarBoton(boton, tema);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', enlazar);
    } else {
        enlazar();
    }

    ns.tema = {
        CLAVE: CLAVE,
        leerPreferencia: leerPreferencia,
        aplicar: aplicar
    };
})(window.VisorSIG);
