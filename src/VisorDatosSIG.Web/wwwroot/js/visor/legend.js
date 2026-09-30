// FE-SIG 2 y 3: leyenda del visor.
// La primera sección describe las capas vectoriales activas (Manzanas, Lotes y
// Vías); la segunda conserva los estados de Código Fijo. Se construye una única
// vez y se muestra u oculta sin reconstruir el DOM. Sin innerHTML.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var ID_CONTENEDOR = 'leyenda-capas';
    var SUBTITULO_TIPOS = 'Capas activas';
    var SUBTITULO_ESTADOS = 'Estados de Código Fijo';
    var ORDEN_ESTADOS = [1, 2, 3, 4, 5];

    // Entradas de tipo. `variableBorde` y `variableFondo` apuntan a los tokens
    // CSS (FE-SIG 3); los colores se aplican en línea, nunca en hexadecimal.
    var TIPOS = [
        {
            clave: 'manzanas',
            etiqueta: 'Manzanas',
            modificador: 'poligono',
            variableBorde: '--capa-manzanas-stroke',
            variableFondo: '--capa-manzanas-fill'
        },
        {
            clave: 'lotes',
            etiqueta: 'Lotes',
            modificador: 'poligono',
            variableBorde: '--capa-lotes-stroke',
            variableFondo: '--capa-lotes-fill'
        },
        {
            clave: 'vias',
            etiqueta: 'Vías',
            modificador: 'linea',
            variableBorde: '--capa-vias-stroke',
            variableFondo: null
        }
    ];

    var inicializada = false;
    var contenedor = null;
    var itemsTipos = {};
    var bloqueEstados = null;

    function obtenerContenedor() {
        return document.getElementById(ID_CONTENEDOR);
    }

    function leerVariable(nombre) {
        if (!nombre || typeof window.getComputedStyle !== 'function' || !document.documentElement) {
            return '';
        }
        var valor = window.getComputedStyle(document.documentElement).getPropertyValue(nombre);
        if (typeof valor !== 'string') {
            return '';
        }
        return valor.trim();
    }

    function crearItemEstado(estado) {
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

    function crearItemTipo(tipo) {
        var item = document.createElement('li');
        item.className = 'visor-leyenda__item';

        var swatch = document.createElement('span');
        swatch.className = 'visor-leyenda__swatch visor-leyenda__swatch--' + tipo.modificador;

        var borde = leerVariable(tipo.variableBorde);
        if (borde) {
            swatch.style.borderColor = borde;
            // `color` alimenta el borde del swatch de línea (borde superior).
            swatch.style.color = borde;
        }
        var fondo = leerVariable(tipo.variableFondo);
        if (fondo) {
            swatch.style.backgroundColor = fondo;
        }

        var etiqueta = document.createElement('span');
        etiqueta.className = 'visor-leyenda__etiqueta';
        etiqueta.textContent = tipo.etiqueta;

        item.appendChild(swatch);
        item.appendChild(etiqueta);
        return item;
    }

    function crearBloque(subtitulo, items) {
        var bloque = document.createElement('div');
        bloque.className = 'visor-leyenda__bloque';

        var encabezado = document.createElement('p');
        encabezado.className = 'visor-leyenda__subtitulo';
        encabezado.textContent = subtitulo;
        bloque.appendChild(encabezado);

        var lista = document.createElement('ul');
        lista.className = 'visor-leyenda__lista';
        for (var i = 0; i < items.length; i++) {
            lista.appendChild(items[i]);
        }
        bloque.appendChild(lista);
        return bloque;
    }

    function inicializar() {
        if (inicializada) {
            return;
        }
        var nodo = obtenerContenedor();
        if (!nodo || !ns.markers) {
            // Sin contenedor o sin los colores de estado no se construye nada;
            // una llamada posterior puede reintentarlo.
            return;
        }

        var itemsTipo = [];
        for (var i = 0; i < TIPOS.length; i++) {
            var item = crearItemTipo(TIPOS[i]);
            itemsTipos[TIPOS[i].clave] = item;
            itemsTipo.push(item);
        }
        var bloqueTipos = crearBloque(SUBTITULO_TIPOS, itemsTipo);

        var itemsEstado = [];
        for (var j = 0; j < ORDEN_ESTADOS.length; j++) {
            itemsEstado.push(crearItemEstado(ORDEN_ESTADOS[j]));
        }
        bloqueEstados = crearBloque(SUBTITULO_ESTADOS, itemsEstado);

        nodo.appendChild(bloqueTipos);
        nodo.appendChild(bloqueEstados);
        contenedor = nodo;
        inicializada = true;
    }

    // `activas` es un objeto tipo { manzanas:true, lotes:false, ... }.
    // No reconstruye el DOM: solo alterna `hidden` en los nodos existentes.
    function actualizar(activas) {
        inicializar();
        if (!inicializada || !contenedor) {
            return;
        }
        activas = activas || {};

        var algunoActivo = false;
        for (var i = 0; i < TIPOS.length; i++) {
            if (activas[TIPOS[i].clave]) {
                algunoActivo = true;
            }
        }
        if (activas.codigosfijos) {
            algunoActivo = true;
        }

        for (var j = 0; j < TIPOS.length; j++) {
            var item = itemsTipos[TIPOS[j].clave];
            if (item) {
                item.hidden = !activas[TIPOS[j].clave];
            }
        }

        if (bloqueEstados) {
            // El bloque de estados solo aplica a Códigos Fijos.
            bloqueEstados.hidden = !activas.codigosfijos;
        }

        contenedor.hidden = !algunoActivo;
    }

    function mostrar() {
        var nodo = obtenerContenedor();
        if (!nodo) {
            return;
        }
        inicializar();
        nodo.removeAttribute('hidden');
    }

    function ocultar() {
        var nodo = obtenerContenedor();
        if (!nodo) {
            return;
        }
        nodo.hidden = true;
    }

    function estaVisible() {
        var nodo = obtenerContenedor();
        return !!nodo && !nodo.hidden;
    }

    ns.legend = {
        inicializar: inicializar,
        actualizar: actualizar,
        mostrar: mostrar,
        ocultar: ocultar,
        estaVisible: estaVisible
    };

    // Alias defensivo por si otra parte del visor usa el nombre en español.
    ns.leyenda = ns.legend;
})(window.VisorSIG);
