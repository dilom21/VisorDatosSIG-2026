// Utilidad centralizada de fechas del workspace autenticado.
//
// La API entrega `DateTime` sin zona (por ejemplo "2026-09-30T14:23:11.123"): la cadena se
// interpreta como hora local y no se convierte a UTC ni se le aplica ningún desplazamiento,
// de modo que la fecha mostrada conserva la semántica del backend.
//
// No se usa `new Date(cadena)` con formatos ambiguos: los componentes (año, mes, día, hora,
// minuto y segundo) se extraen explícitamente con una expresión regular y se construye la
// fecha local con el constructor de componentes.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var PATRON = /^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2}))?)?/;

    function dosDigitos(valor) {
        return (valor < 10 ? '0' : '') + valor;
    }

    function numero(valor, alterno) {
        return valor === undefined || valor === null ? alterno : Number(valor);
    }

    // La fecha del backend es local: se construye con componentes, nunca con Date.parse.
    function aFecha(valor) {
        if (typeof valor !== 'string') {
            return null;
        }

        var partes = PATRON.exec(valor.trim());
        if (!partes) {
            return null;
        }

        var fecha = new Date(
            Number(partes[1]),
            Number(partes[2]) - 1,
            Number(partes[3]),
            numero(partes[4], 0),
            numero(partes[5], 0),
            numero(partes[6], 0));

        return isNaN(fecha.getTime()) ? null : fecha;
    }

    function formatearFecha(valor) {
        var fecha = aFecha(valor);
        if (!fecha) {
            return '—';
        }

        return dosDigitos(fecha.getDate()) + '/'
            + dosDigitos(fecha.getMonth() + 1) + '/'
            + fecha.getFullYear();
    }

    function formatearFechaHora(valor) {
        var fecha = aFecha(valor);
        if (!fecha) {
            return '—';
        }

        return formatearFecha(valor) + ' '
            + dosDigitos(fecha.getHours()) + ':'
            + dosDigitos(fecha.getMinutes()) + ':'
            + dosDigitos(fecha.getSeconds());
    }

    ns.fechas = {
        aFecha: aFecha,
        formatearFecha: formatearFecha,
        formatearFechaHora: formatearFechaHora
    };
})(window.VisorSIG);
