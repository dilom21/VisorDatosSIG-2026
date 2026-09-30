// FE-SIG 2: representación de los puntos de Códigos Fijos.
// Construye los marcadores Leaflet y los popups. Los popups se arman con
// nodos del DOM y `textContent`: nunca se usa innerHTML. Los colores se leen
// de las variables CSS (FE-UI 1), nunca se escriben hexadecimales aquí.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    // Un único renderer de canvas para todos los puntos: reutilizarlo evita
    // crear miles de lienzos cuando la capa trae muchos registros.
    var renderer = null;

    var ESTADOS = {
        1: { nombre: 'Normal', variable: '--estado-normal' },
        2: { nombre: 'Para Corte', variable: '--estado-para-corte' },
        3: { nombre: 'Cortado', variable: '--estado-cortado' },
        4: { nombre: 'Baja Parcial', variable: '--estado-baja-parcial' },
        5: { nombre: 'Baja Total', variable: '--estado-baja-total' }
    };

    var ESTADO_DESCONOCIDO = { nombre: 'Desconocido', variable: '--estado-desconocido' };

    function obtenerRenderer() {
        if (typeof L === 'undefined') {
            return null;
        }
        if (!renderer) {
            renderer = L.canvas({ padding: 0.5 });
        }
        return renderer;
    }

    function normalizarEstado(estado) {
        var numero;
        if (typeof estado === 'number') {
            numero = estado;
        } else if (typeof estado === 'string' && estado !== '') {
            numero = Number(estado);
        } else {
            return null;
        }
        if (!isFinite(numero) || Math.floor(numero) !== numero) {
            return null;
        }
        return numero;
    }

    function obtenerEstadoInfo(estado) {
        var normalizado = normalizarEstado(estado);
        if (normalizado !== null && ESTADOS[normalizado]) {
            return ESTADOS[normalizado];
        }
        return ESTADO_DESCONOCIDO;
    }

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

    function obtenerNombreEstado(estado) {
        return obtenerEstadoInfo(estado).nombre;
    }

    function obtenerColorEstado(estado) {
        // Sin variable CSS disponible queda vacío: Leaflet aplica su color por defecto.
        return leerVariable(obtenerEstadoInfo(estado).variable);
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

    function crearPopup(feature) {
        var contenedor = document.createElement('div');
        contenedor.className = 'visor-popup';

        var lista = document.createElement('dl');
        lista.className = 'visor-popup__lista';

        lista.appendChild(crearFila('Código SIG', valorPropiedad(feature, 'CodF_SIG')));
        lista.appendChild(crearFila('Código fijo', valorPropiedad(feature, 'CodFijo')));
        lista.appendChild(crearFila('Nombre', valorPropiedad(feature, 'Nombre')));
        lista.appendChild(crearFila('Estado', obtenerNombreEstado(valorPropiedad(feature, 'Estado'))));
        lista.appendChild(crearFila('Lote', valorPropiedad(feature, 'IdLote')));
        lista.appendChild(crearFila('ID interno', feature ? feature.id : null));

        contenedor.appendChild(lista);
        return contenedor;
    }

    function crearMarker(feature, latlng) {
        var color = obtenerColorEstado(valorPropiedad(feature, 'Estado'));
        var opciones = {
            renderer: obtenerRenderer(),
            radius: 5,
            weight: 1,
            fillOpacity: 0.85,
            opacity: 0.9
        };

        // El color se toma de CSS; si no está disponible se deja el valor por
        // defecto de Leaflet en lugar de inventar un hexadecimal.
        if (color) {
            opciones.color = color;
            opciones.fillColor = color;
        }

        var marcador = L.circleMarker(latlng, opciones);
        marcador.bindPopup(crearPopup(feature));
        return marcador;
    }

    ns.markers = {
        obtenerRenderer: obtenerRenderer,
        obtenerNombreEstado: obtenerNombreEstado,
        obtenerColorEstado: obtenerColorEstado,
        crearPopup: crearPopup,
        crearMarker: crearMarker
    };
})(window.VisorSIG);
