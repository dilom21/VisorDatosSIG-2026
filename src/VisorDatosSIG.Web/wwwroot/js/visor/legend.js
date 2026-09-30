// FE-SIG 2: leyenda de los estados de Código Fijo.
// Solo cubre la capa de Códigos Fijos. Construye la leyenda una única vez y
// la muestra u oculta según el estado de la capa. Sin innerHTML.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var ID_CONTENEDOR = 'leyenda-codigos-fijos';
    var TITULO = 'Estados de Código Fijo';
    var ORDEN_ESTADOS = [1, 2, 3, 4, 5];

    var inicializada = false;

    function obtenerContenedor() {
        return document.getElementById(ID_CONTENEDOR);
    }

    function crearItem(estado) {
        var item = document.createElement('li');
        item.className = 'visor-leyenda__item';

        var swatch = document.createElement('span');
        swatch.className = 'visor-leyenda__swatch';
        swatch.style.backgroundColor = ns.markers.obtenerColorEstado(estado);

        var etiqueta = document.createElement('span');
        etiqueta.className = 'visor-leyenda__etiqueta';
        etiqueta.textContent = ns.markers.obtenerNombreEstado(estado);

        item.appendChild(swatch);
        item.appendChild(etiqueta);
        return item;
    }

    function inicializar() {
        if (inicializada) {
            return;
        }
        var contenedor = obtenerContenedor();
        if (!contenedor || !ns.markers) {
            // Sin contenedor o sin los colores de estado no se construye nada;
            // una llamada posterior puede reintentarlo.
            return;
        }

        var titulo = document.createElement('p');
        titulo.className = 'visor-leyenda__titulo';
        titulo.textContent = TITULO;

        var lista = document.createElement('ul');
        lista.className = 'visor-leyenda__lista';

        for (var i = 0; i < ORDEN_ESTADOS.length; i++) {
            lista.appendChild(crearItem(ORDEN_ESTADOS[i]));
        }

        contenedor.appendChild(titulo);
        contenedor.appendChild(lista);
        inicializada = true;
    }

    function mostrar() {
        var contenedor = obtenerContenedor();
        if (!contenedor) {
            return;
        }
        inicializar();
        contenedor.removeAttribute('hidden');
    }

    function ocultar() {
        var contenedor = obtenerContenedor();
        if (!contenedor) {
            return;
        }
        contenedor.hidden = true;
    }

    function estaVisible() {
        var contenedor = obtenerContenedor();
        return !!contenedor && !contenedor.hidden;
    }

    ns.legend = {
        inicializar: inicializar,
        mostrar: mostrar,
        ocultar: ocultar,
        estaVisible: estaVisible
    };

    // Alias defensivo por si otra parte del visor usa el nombre en español.
    ns.leyenda = ns.legend;
})(window.VisorSIG);
