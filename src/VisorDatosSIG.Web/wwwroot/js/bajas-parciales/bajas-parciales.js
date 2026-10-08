window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var bajas = [], empleados = [], editandoId = null, ocupado = false, focoAnterior = null;
    var $ = function (id) { return document.getElementById(id); };

    function mensaje(texto, error) {
        var nodo = $('bajas-mensaje');
        nodo.textContent = texto || '';
        nodo.hidden = !texto;
        nodo.className = 'bajas-mensaje' + (error ? ' bajas-mensaje--error' : ' bajas-mensaje--exito');
    }

    function textoError(res, accion) {
        var datos = res && res.datos;
        if (datos && typeof datos.mensaje === 'string') return datos.mensaje;
        if (datos && typeof datos.detail === 'string') return datos.detail;
        if (res && res.status === 0) return res.error === 'timeout' ? 'La solicitud tardó demasiado. Intente nuevamente.' : 'No se pudo conectar con la API.';
        if (res && res.status === 400) return 'Revise los datos ingresados y vuelva a intentar.';
        if (res && res.status === 401) return 'La sesión expiró. Inicie sesión nuevamente.';
        if (res && res.status === 403) return 'No cuenta con autorización para esta operación.';
        if (res && res.status === 404) return 'El registro solicitado ya no está disponible.';
        if (res && res.status === 409) return 'La operación entra en conflicto con otro registro.';
        if (res && res.status >= 500) return 'La API no pudo completar la operación. Intente más tarde.';
        return 'No fue posible ' + (accion || 'completar la solicitud') + '.';
    }

    function pedir(ruta, metodo, cuerpo) {
        return ns.api.peticion('api/BajasParciales' + ruta, { metodo: metodo || 'GET', cuerpo: cuerpo || null, token: ns.sesion.obtenerToken(), protegida: true });
    }

    function fechaHoy() { return new Date().toISOString().slice(0, 10); }
    function fechaISO(valor) { return typeof valor === 'string' ? valor.slice(0, 10) : ''; }
    function fechaVisible(valor) { var f = fechaISO(valor).split('-'); return f.length === 3 ? f[2] + '/' + f[1] + '/' + f[0] : '—'; }
    function estadoDerivado(item) {
        var estado = String(item.estado || '').toLowerCase();
        if (estado === 'cancelada') return 'cancelada';
        if (estado === 'finalizada' || fechaISO(item.fechaFin) < fechaHoy()) return 'finalizada';
        return fechaISO(item.fechaInicio) > fechaHoy() ? 'programada' : 'vigente';
    }
    function etiquetaEstado(estado) { return { vigente: 'Vigente', programada: 'Programada', finalizada: 'Finalizada', cancelada: 'Cancelada' }[estado] || estado; }
    function normalizar(valor) { return String(valor || '').toLocaleLowerCase(); }

    function actualizarMetricas() {
        var conteo = { vigente: 0, programada: 0, finalizada: 0, cancelada: 0 };
        bajas.forEach(function (b) { conteo[estadoDerivado(b)] += 1; });
        $('metrica-total').textContent = bajas.length;
        $('metrica-vigentes').textContent = conteo.vigente;
        $('metrica-programadas').textContent = conteo.programada;
        $('metrica-finalizadas').textContent = conteo.finalizada;
        $('metrica-canceladas').textContent = conteo.cancelada;
    }

    function llenarEmpleados() {
        var filtros = $('bajas-filtro-empleado'), formulario = $('baja-empleado');
        filtros.replaceChildren(); formulario.replaceChildren();
        var todos = document.createElement('option'); todos.value = ''; todos.textContent = 'Todos los empleados'; filtros.appendChild(todos);
        empleados.forEach(function (e) {
            var label = e.codigo + ' — ' + e.nombreCompleto;
            var optFiltro = document.createElement('option'); optFiltro.value = e.idEmpleado; optFiltro.textContent = label; filtros.appendChild(optFiltro);
            var optForm = document.createElement('option'); optForm.value = e.idEmpleado; optForm.textContent = label; formulario.appendChild(optForm);
        });
    }

    function botonAccion(texto, fn) { var b = document.createElement('button'); b.type = 'button'; b.className = 'bajas-accion'; b.textContent = texto; b.addEventListener('click', fn); return b; }
    function dibujarTabla() {
        var tbody = $('bajas-lista'); tbody.replaceChildren();
        var termino = normalizar($('bajas-busqueda').value.trim()), estadoFiltro = $('bajas-estado').value, empleadoFiltro = $('bajas-filtro-empleado').value;
        var visibles = bajas.filter(function (b) {
            var estado = estadoDerivado(b), texto = normalizar(b.codigoEmpleado + ' ' + b.nombreEmpleado);
            return (!termino || texto.indexOf(termino) !== -1) && (!estadoFiltro || estado === estadoFiltro) && (!empleadoFiltro || String(b.idEmpleado) === empleadoFiltro);
        });
        actualizarMetricas();
        if (!visibles.length) { var tr = document.createElement('tr'), td = document.createElement('td'); td.colSpan = 6; td.className = 'bajas-vacio'; td.textContent = 'No se encontraron bajas parciales con los filtros seleccionados.'; tr.appendChild(td); tbody.appendChild(tr); return; }
        visibles.forEach(function (b) {
            var estado = estadoDerivado(b), tr = document.createElement('tr');
            var empleado = document.createElement('td'), bloque = document.createElement('div'); bloque.className = 'bajas-empleado'; var nombre = document.createElement('strong'); nombre.textContent = b.nombreEmpleado; var codigo = document.createElement('span'); codigo.textContent = b.codigoEmpleado; bloque.appendChild(nombre); bloque.appendChild(codigo); empleado.appendChild(bloque);
            var periodo = document.createElement('td'), periodoTexto = document.createElement('span'); periodoTexto.className = 'bajas-periodo'; periodoTexto.textContent = fechaVisible(b.fechaInicio) + ' — ' + fechaVisible(b.fechaFin); periodo.appendChild(periodoTexto);
            var motivo = document.createElement('td'); motivo.className = 'bajas-motivo'; motivo.textContent = b.motivo || '—';
            var estadoTd = document.createElement('td'), badge = document.createElement('span'); badge.className = 'bajas-badge bajas-badge--' + estado; badge.textContent = etiquetaEstado(estado); estadoTd.appendChild(badge);
            var registro = document.createElement('td'); registro.textContent = fechaVisible(b.fechaRegistro);
            var acciones = document.createElement('td'), grupo = document.createElement('div'); grupo.className = 'bajas-acciones';
            grupo.appendChild(botonAccion('Detalle', function () { abrirDetalle(b); }));
            if (String(b.estado).toLowerCase() === 'activa') {
                grupo.appendChild(botonAccion('Editar', function () { abrirFormulario(b); }));
                grupo.appendChild(botonAccion('Finalizar', function () { cambiarEstado(b, 'Finalizada'); }));
                grupo.appendChild(botonAccion('Cancelar', function () { cambiarEstado(b, 'Cancelada'); }));
            } else if (String(b.estado).toLowerCase() === 'finalizada' || String(b.estado).toLowerCase() === 'cancelada') {
                grupo.appendChild(botonAccion('Reactivar', function () { cambiarEstado(b, 'Activa'); }));
            }
            acciones.appendChild(grupo); tr.appendChild(empleado); tr.appendChild(periodo); tr.appendChild(motivo); tr.appendChild(estadoTd); tr.appendChild(registro); tr.appendChild(acciones); tbody.appendChild(tr);
        });
    }

    function bloquear(valor) { ocupado = valor; document.querySelectorAll('#bajas-contenido button, #bajas-contenido input, #bajas-contenido select, #modal-baja button, #modal-baja input, #modal-baja select, #modal-baja textarea').forEach(function (n) { n.disabled = valor; }); }
    function abrirModal(modal, foco) { focoAnterior = document.activeElement; modal.hidden = false; document.body.classList.add('modal-abierto'); if (foco) foco.focus(); }
    function cerrarModal(modal) { modal.hidden = true; document.body.classList.remove('modal-abierto'); if (focoAnterior && typeof focoAnterior.focus === 'function') focoAnterior.focus(); }
    function errorFormulario(texto) { var n = $('modal-baja-error'); n.textContent = texto; n.hidden = !texto; }

    function abrirFormulario(b) {
        editandoId = b ? b.idBajaParcial : null; $('modal-baja-titulo').textContent = b ? 'Editar baja parcial' : 'Registrar baja parcial'; $('modal-baja-subtitulo').textContent = b ? 'Actualice fechas, motivo u observaciones.' : 'Complete el periodo y el motivo.';
        $('baja-empleado').value = b ? String(b.idEmpleado) : (empleados[0] ? String(empleados[0].idEmpleado) : ''); $('baja-empleado').disabled = !!b; $('baja-inicio').value = b ? fechaISO(b.fechaInicio) : fechaHoy(); $('baja-fin').value = b ? fechaISO(b.fechaFin) : fechaHoy(); $('baja-motivo').value = b ? (b.motivo || '') : ''; $('baja-observaciones').value = b ? (b.observaciones || '') : ''; errorFormulario(''); abrirModal($('modal-baja'), $('baja-empleado'));
    }
    function abrirDetalle(b) {
        var contenido = $('detalle-baja-contenido'); contenido.replaceChildren(); var dl = document.createElement('dl'); dl.className = 'bajas-detalle'; var pares = [['Empleado', b.codigoEmpleado + ' — ' + b.nombreEmpleado], ['Periodo', fechaVisible(b.fechaInicio) + ' — ' + fechaVisible(b.fechaFin)], ['Estado API', b.estado], ['Estado derivado', etiquetaEstado(estadoDerivado(b))], ['Motivo', b.motivo], ['Observaciones', b.observaciones || '—'], ['Registrada', fechaVisible(b.fechaRegistro)]];
        pares.forEach(function (par) { var dt = document.createElement('dt'), dd = document.createElement('dd'); dt.textContent = par[0]; dd.textContent = par[1] || '—'; dl.appendChild(dt); dl.appendChild(dd); }); contenido.appendChild(dl); abrirModal($('modal-detalle-baja'), $('btn-cerrar-detalle'));
    }

    async function cargarCatalogo() { var res = await pedir('/empleados'); if (!res.ok) { mensaje(textoError(res, 'cargar el catálogo de empleados'), true); return false; } empleados = Array.isArray(res.datos) ? res.datos : []; llenarEmpleados(); return true; }
    async function cargarBajas() { var id = $('bajas-filtro-empleado').value, ruta = id ? '?idEmpleado=' + encodeURIComponent(id) : ''; var res = await pedir(ruta); if (!res.ok) { mensaje(textoError(res, 'cargar el listado'), true); return false; } bajas = Array.isArray(res.datos) ? res.datos : []; dibujarTabla(); return true; }
    async function cambiarEstado(b, estado) { if (ocupado || !window.confirm('¿Desea cambiar el estado de esta baja a ' + estado + '?')) return; bloquear(true); try { var res = await pedir('/' + b.idBajaParcial + '/estado', 'PATCH', { estado: estado }); if (!res.ok) { mensaje(textoError(res, 'cambiar el estado'), true); return; } mensaje('Estado actualizado correctamente.', false); await cargarBajas(); } finally { bloquear(false); } }

    async function guardar(evento) {
        evento.preventDefault(); if (ocupado) return; var inicio = $('baja-inicio').value, fin = $('baja-fin').value, motivo = $('baja-motivo').value.trim();
        if (!inicio || !fin || !motivo || (!editandoId && !$('baja-empleado').value)) { errorFormulario('Complete los campos obligatorios.'); return; }
        if (fin < inicio) { errorFormulario('La fecha de fin debe ser igual o posterior a la fecha de inicio.'); return; }
        var cuerpo = { fechaInicio: inicio, fechaFin: fin, motivo: motivo, observaciones: $('baja-observaciones').value.trim() || null }; if (!editandoId) cuerpo.idEmpleado = Number($('baja-empleado').value);
        bloquear(true); try { var res = await pedir(editandoId ? '/' + editandoId : '', editandoId ? 'PUT' : 'POST', cuerpo); if (!res.ok) { errorFormulario(textoError(res, 'guardar la baja parcial')); return; } cerrarModal($('modal-baja')); mensaje(editandoId ? 'Baja parcial actualizada correctamente.' : 'Baja parcial registrada correctamente.', false); await cargarBajas(); } finally { bloquear(false); }
    }

    function eventos() {
        $('btn-nueva-baja').addEventListener('click', function () { abrirFormulario(null); }); $('btn-actualizar-bajas').addEventListener('click', async function () { bloquear(true); try { await cargarBajas(); if (!$('bajas-mensaje').textContent) mensaje('Listado actualizado.', false); } finally { bloquear(false); } }); $('bajas-busqueda').addEventListener('input', dibujarTabla); $('bajas-estado').addEventListener('change', dibujarTabla); $('bajas-filtro-empleado').addEventListener('change', cargarBajas); $('form-baja').addEventListener('submit', guardar); $('btn-cerrar-baja').addEventListener('click', function () { cerrarModal($('modal-baja')); }); $('btn-cancelar-baja').addEventListener('click', function () { cerrarModal($('modal-baja')); }); $('btn-cerrar-detalle').addEventListener('click', function () { cerrarModal($('modal-detalle-baja')); }); document.querySelector('[data-cerrar-modal]').addEventListener('click', function () { cerrarModal($('modal-baja')); }); document.querySelector('[data-cerrar-detalle]').addEventListener('click', function () { cerrarModal($('modal-detalle-baja')); }); document.addEventListener('keydown', function (e) { if (e.key === 'Escape') { if (!$('modal-baja').hidden) cerrarModal($('modal-baja')); if (!$('modal-detalle-baja').hidden) cerrarModal($('modal-detalle-baja')); } });
    }
    async function iniciar() { $('bajas-contenido').hidden = false; eventos(); bloquear(true); try { await cargarCatalogo(); await cargarBajas(); } finally { bloquear(false); } }
    ns.bajasParciales = { iniciar: iniciar };
})(window.VisorSIG);
