// FE-SIG 3: simbología y popups de Manzanas, Lotes y Vías.
// Construye los estilos Leaflet leyendo las variables CSS y arma los popups con
// nodos del DOM y `textContent`: nunca se usa innerHTML. Los colores vienen de
// las variables CSS (FE-UI 1 / FE-SIG 3), nunca se escriben hexadecimales aquí.
// Los Códigos Fijos conservan su representación en markers.js.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    // Un renderer de canvas por capa: se crea una sola vez y se reutiliza.
    // Reutilizarlo evita crear miles de lienzos cuando una capa trae muchos
    // registros. Cada uno se monta en su propio pane para conservar el orden.
    var rendererManzanas = null;
    var rendererLotes = null;
    var rendererVias = null;

    function leerVariable(nombre) {
        if (typeof window.getComputedStyle !== 'function' || !document.documentElement) {
            return '';
        }
        var valor = window.getComputedStyle(document.documentElement).getPropertyValue(nombre);
        if (typeof valor !== 'string') {
            return '';
        }
        return valor.trim();
    }

    // El backend compara nombres de propiedad sin distinguir mayúsculas; aquí
    // se replica esa tolerancia para no depender del casing del JSON.
    function valorPropiedad(feature, nombre) {
        if (!feature || !feature.properties || typeof feature.properties !== 'object') {
            return null;
        }
        if (Object.prototype.hasOwnProperty.call(feature.properties, nombre)) {
            return feature.properties[nombre];
        }
        var buscado = String(nombre).toLowerCase();
        var claves = Object.keys(feature.properties);
        for (var i = 0; i < claves.length; i++) {
            if (claves[i].toLowerCase() === buscado) {
                return feature.properties[claves[i]];
            }
        }
        return null;
    }

    function mostrarValor(valor) {
        if (valor === null || valor === undefined) {
            return '—';
        }
        var texto = String(valor);
        if (texto.trim() === '') {
            return '—';
        }
        return texto;
    }

    function crearFila(clave, valor) {
        var fila = document.createElement('div');
        fila.className = 'visor-popup__fila';

        var termino = document.createElement('dt');
        termino.className = 'visor-popup__clave';
        termino.textContent = clave;

        var definicion = document.createElement('dd');
        definicion.className = 'visor-popup__valor';
        definicion.textContent = mostrarValor(valor);

        fila.appendChild(termino);
        fila.appendChild(definicion);
        return fila;
    }

    // `titulo` es opcional; `filas` es un arreglo de pares [clave, valor].
    function crearPopup(titulo, filas) {
        var contenedor = document.createElement('div');
        contenedor.className = 'visor-popup';

        if (titulo) {
            var encabezado = document.createElement('p');
            encabezado.className = 'visor-popup__titulo';
            encabezado.textContent = titulo;
            contenedor.appendChild(encabezado);
        }

        var lista = document.createElement('dl');
        lista.className = 'visor-popup__lista';

        for (var i = 0; i < filas.length; i++) {
            lista.appendChild(crearFila(filas[i][0], filas[i][1]));
        }

        contenedor.appendChild(lista);
        return contenedor;
    }

    function obtenerRendererManzanas() {
        if (typeof L === 'undefined') {
            return null;
        }
        if (!rendererManzanas) {
            rendererManzanas = L.canvas({ padding: 0.5, pane: 'paneManzanas' });
        }
        return rendererManzanas;
    }

    function obtenerRendererLotes() {
        if (typeof L === 'undefined') {
            return null;
        }
        if (!rendererLotes) {
            rendererLotes = L.canvas({ padding: 0.5, pane: 'paneLotes' });
        }
        return rendererLotes;
    }

    function obtenerRendererVias() {
        if (typeof L === 'undefined') {
            return null;
        }
        if (!rendererVias) {
            rendererVias = L.canvas({ padding: 0.5, pane: 'paneVias' });
        }
        return rendererVias;
    }

    // El color se toma de CSS; si la variable no está disponible la clave se
    // omite para que Leaflet aplique su valor por defecto en lugar de un
    // hexadecimal inventado.
    function estiloManzana(feature) {
        var color = leerVariable('--capa-manzanas-stroke');
        var relleno = leerVariable('--capa-manzanas-fill');
        var estilo = {
            weight: 1.5,
            opacity: 0.85,
            fillOpacity: 0.12,
            fill: true
        };
        if (color) {
            estilo.color = color;
        }
        if (relleno) {
            estilo.fillColor = relleno;
        }
        return estilo;
    }

    function estiloLote(feature) {
        var color = leerVariable('--capa-lotes-stroke');
        var relleno = leerVariable('--capa-lotes-fill');
        var estilo = {
            weight: 1,
            opacity: 0.7,
            fillOpacity: 0.06,
            fill: true
        };
        if (color) {
            estilo.color = color;
        }
        if (relleno) {
            estilo.fillColor = relleno;
        }
        return estilo;
    }

    function estiloVia(feature) {
        var color = leerVariable('--capa-vias-stroke');
        var estilo = {
            weight: 2.5,
            opacity: 0.9,
            fill: false
        };
        if (color) {
            estilo.color = color;
        }
        return estilo;
    }

    function crearPopupManzana(feature) {
        return crearPopup(null, [
            ['UV_MZA', valorPropiedad(feature, 'UV_MZA')],
            ['UV', valorPropiedad(feature, 'UV')],
            ['MZA', valorPropiedad(feature, 'MZA')],
            ['IdOrigen', valorPropiedad(feature, 'IdOrigen')],
            ['ID interno', feature ? feature.id : null]
        ]);
    }

    function crearPopupLote(feature) {
        return crearPopup(null, [
            ['NroLote', valorPropiedad(feature, 'NroLote')],
            ['IdManzana', valorPropiedad(feature, 'IdManzana')],
            ['IdOrigen', valorPropiedad(feature, 'IdOrigen')],
            ['ID interno', feature ? feature.id : null]
        ]);
    }

    function crearPopupVia(feature) {
        return crearPopup(null, [
            ['Nombre', valorPropiedad(feature, 'Nombre')],
            ['TipoVia', valorPropiedad(feature, 'TipoVia')],
            ['OSMID', valorPropiedad(feature, 'OSMID')],
            ['OBJECTID', valorPropiedad(feature, 'OBJECTID')],
            ['ID interno', feature ? feature.id : null]
        ]);
    }

    ns.features = {
        obtenerRendererManzanas: obtenerRendererManzanas,
        obtenerRendererLotes: obtenerRendererLotes,
        obtenerRendererVias: obtenerRendererVias,
        estiloManzana: estiloManzana,
        estiloLote: estiloLote,
        estiloVia: estiloVia,
        crearPopupManzana: crearPopupManzana,
        crearPopupLote: crearPopupLote,
        crearPopupVia: crearPopupVia
    };
})(window.VisorSIG);
