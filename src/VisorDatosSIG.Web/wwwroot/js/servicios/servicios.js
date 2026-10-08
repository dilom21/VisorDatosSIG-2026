window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var estados = { 1: 'Normal', 2: 'Para Corte', 3: 'Cortado', 4: 'Baja Parcial', 5: 'Baja Total' };
    var estadoClases = { 1: 'normal', 2: 'para-corte', 3: 'cortado', 4: 'baja-parcial', 5: 'baja-total' };
    var pagina = 1, totalPaginas = 1, totalRegistros = 0, ocupado = false, secuencia = 0, temporizador = null;
    var servicioEstado = null, focoAnterior = null, eventosConfigurados = false;
    var $ = function (id) { return document.getElementById(id); };

    function textoError(res, accion) {
        var datos = res && res.datos;
        if (datos && typeof datos.mensaje === 'string') return datos.mensaje;
        if (datos && typeof datos.detail === 'string') return datos.detail;
        if (res && res.status === 0) return res.error === 'timeout' ? 'La solicitud tardó demasiado. Intente nuevamente.' : 'No se pudo conectar con la API.';
        return ({ 400: 'Revise los datos e inténtelo nuevamente.', 401: 'La sesión expiró. Inicie sesión nuevamente.', 403: 'No cuenta con autorización para esta operación.', 404: 'El servicio solicitado no está disponible.', 409: 'La operación entra en conflicto con otro cambio.', 500: 'La API no pudo completar la operación. Intente más tarde.' })[res && res.status] || 'No fue posible ' + (accion || 'completar la solicitud') + '.';
    }
    function avisar(texto, error) { var n = $('servicios-mensaje'); n.textContent = texto || ''; n.hidden = !texto; n.className = 'servicios-mensaje' + (error ? ' servicios-mensaje--error' : ' servicios-mensaje--exito'); }
    function pedir(ruta, metodo, cuerpo) { return ns.api.peticion('api/Servicios' + ruta, { metodo: metodo || 'GET', cuerpo: cuerpo || null, token: ns.sesion.obtenerToken(), protegida: true }); }
    function valor(v, fallback) { return v === null || v === undefined || v === '' ? (fallback || '—') : String(v); }
    function nombreEstado(item) { return item && item.estadoNombre ? item.estadoNombre : (estados[item && item.estado] || 'Estado no identificado'); }
    function fechaVisible(v) { if (!v) return '—'; var d = new Date(v); return isNaN(d.getTime()) ? '—' : d.toLocaleString('es-BO', { dateStyle: 'short', timeStyle: 'short' }); }
    function bloquear(flag) { ocupado = flag; document.querySelectorAll('#servicios-contenido button, #servicios-contenido input, #servicios-contenido select, #form-estado-servicio button, #form-estado-servicio select').forEach(function (n) { n.disabled = flag; }); }
    function textoBoton(label, fn) { var b = document.createElement('button'); b.type = 'button'; b.className = 'servicios-accion'; b.textContent = label; b.addEventListener('click', fn); return b; }
    function detalleCodigo(item) { return valor(item.codFijo) + ' / ' + valor(item.codF_SQL); }
    function dibujarTabla(items) {
        var tbody = $('servicios-lista'); tbody.replaceChildren();
        if (!items.length) { var tr = document.createElement('tr'), td = document.createElement('td'); td.colSpan = 6; td.className = 'servicios-vacio'; td.textContent = 'No se encontraron servicios con los filtros seleccionados.'; tr.appendChild(td); tbody.appendChild(tr); return; }
        items.forEach(function (item) {
            var tr = document.createElement('tr'), tdCodigo = document.createElement('td'), principal = document.createElement('strong'), secundario = document.createElement('span');
            principal.textContent = valor(item.codF_SIG); secundario.textContent = 'CodFijo: ' + valor(item.codFijo) + ' · CodF_SQL: ' + valor(item.codF_SQL); tdCodigo.className = 'servicios-codigo'; tdCodigo.appendChild(principal); tdCodigo.appendChild(secundario);
            var tdTitular = document.createElement('td'); tdTitular.textContent = valor(item.nombre, 'Sin nombre asociado');
            var tdRef = document.createElement('td'); tdRef.textContent = 'IdLote: ' + valor(item.idLote);
            var tdEstado = document.createElement('td'), badge = document.createElement('span'); badge.className = 'servicios-badge servicios-badge--' + (estadoClases[item.estado] || 'desconocido'); badge.textContent = nombreEstado(item); tdEstado.appendChild(badge);
            var tdFecha = document.createElement('td'); tdFecha.textContent = fechaVisible(item.fechaCambioEstado);
            var tdAcciones = document.createElement('td'), acciones = document.createElement('div'); acciones.className = 'servicios-acciones'; acciones.appendChild(textoBoton('Detalle', function () { abrirDetalle(item.idCodigo); })); acciones.appendChild(textoBoton('Cambiar estado', function () { abrirCambio(item); })); tdAcciones.appendChild(acciones);
            [tdCodigo, tdTitular, tdRef, tdEstado, tdFecha, tdAcciones].forEach(function (td) { tr.appendChild(td); }); tbody.appendChild(tr);
        });
    }
    function actualizarPaginacion() { var desde = totalRegistros ? ((pagina - 1) * Number($('servicios-limite').value)) + 1 : 0, hasta = Math.min(pagina * Number($('servicios-limite').value), totalRegistros); $('servicios-rango').textContent = 'Mostrando ' + desde + '-' + hasta + ' de ' + totalRegistros + ' servicios'; $('servicios-pagina-actual').textContent = 'Página ' + pagina + ' de ' + totalPaginas; $('btn-pagina-anterior').disabled = ocupado || pagina <= 1; $('btn-pagina-siguiente').disabled = ocupado || pagina >= totalPaginas; }
    async function cargarLista() {
        var miSecuencia = ++secuencia, params = new URLSearchParams(), q = $('servicios-busqueda').value.trim(), estado = $('servicios-estado').value, limite = $('servicios-limite').value;
        if (q) params.set('busqueda', q); if (estado) params.set('estado', estado); params.set('pagina', pagina); params.set('limite', limite);
        var res = await pedir('?' + params.toString()); if (miSecuencia !== secuencia) return false;
        if (!res.ok) { avisar(textoError(res, 'cargar el listado'), true); return false; }
        var datos = res.datos || {}; pagina = Number(datos.pagina) || pagina; totalPaginas = Number(datos.totalPaginas) || 1; totalRegistros = Number(datos.totalRegistros) || 0; dibujarTabla(Array.isArray(datos.datos) ? datos.datos : []); actualizarPaginacion(); return true;
    }
    async function cargarResumen() { var res = await pedir('/resumen'); if (!res.ok) { avisar(textoError(res, 'cargar el resumen'), true); return false; } var d = res.datos || {}; $('metrica-total').textContent = d.total || 0; $('metrica-normal').textContent = d.normal || 0; $('metrica-para-corte').textContent = d.paraCorte || 0; $('metrica-cortado').textContent = d.cortado || 0; $('metrica-baja-parcial').textContent = d.bajaParcial || 0; $('metrica-baja-total').textContent = d.bajaTotal || 0; return true; }
    function abrirModal(modal, foco) { focoAnterior = document.activeElement; modal.hidden = false; document.body.classList.add('modal-abierto'); if (foco) foco.focus(); }
    function cerrarModal(modal) { modal.hidden = true; document.body.classList.remove('modal-abierto'); if (focoAnterior && typeof focoAnterior.focus === 'function') focoAnterior.focus(); }
    async function abrirDetalle(id) { var res = await pedir('/' + encodeURIComponent(id)); if (!res.ok) { avisar(textoError(res, 'cargar el detalle'), true); return; } var d = res.datos || {}, contenido = $('detalle-servicio-contenido'); contenido.replaceChildren(); var dl = document.createElement('dl'); dl.className = 'servicios-detalle'; [['Código SIG', d.codF_SIG], ['Código fijo', d.codFijo], ['Código SQL', d.codF_SQL], ['Titular', d.nombre || 'Sin nombre asociado'], ['Estado', nombreEstado(d)], ['Fecha de cambio', fechaVisible(d.fechaCambioEstado)], ['IdLote', d.idLote], ['Longitud', d.longitud], ['Latitud', d.latitud]].forEach(function (par) { var dt = document.createElement('dt'), dd = document.createElement('dd'); dt.textContent = par[0]; dd.textContent = valor(par[1]); dl.appendChild(dt); dl.appendChild(dd); }); contenido.appendChild(dl); abrirModal($('modal-detalle-servicio'), $('btn-cerrar-detalle')); }
    function abrirCambio(item) { servicioEstado = item; $('cambio-servicio').textContent = valor(item.codF_SIG); $('cambio-titular').textContent = valor(item.nombre, 'Sin nombre asociado'); $('cambio-actual').textContent = nombreEstado(item); $('cambio-nuevo').value = ''; $('estado-servicio-error').hidden = true; abrirModal($('modal-estado-servicio'), $('cambio-nuevo')); }
    async function guardarEstado(evento) { evento.preventDefault(); if (ocupado || !servicioEstado) return; var nuevo = Number($('cambio-nuevo').value), error = $('estado-servicio-error'); error.hidden = true; if (!nuevo) { error.textContent = 'Seleccione el nuevo estado.'; error.hidden = false; $('cambio-nuevo').focus(); return; } if (nuevo === Number(servicioEstado.estado)) { error.textContent = 'El nuevo estado debe ser diferente al estado actual.'; error.hidden = false; return; } if (!window.confirm('¿Confirma cambiar el estado del servicio ' + valor(servicioEstado.codF_SIG) + '?')) return; bloquear(true); try { var res = await pedir('/' + encodeURIComponent(servicioEstado.idCodigo) + '/estado', 'PATCH', { estado: nuevo }); if (!res.ok) { error.textContent = textoError(res, 'cambiar el estado'); error.hidden = false; return; } cerrarModal($('modal-estado-servicio')); avisar('Estado actualizado correctamente.', false); await Promise.all([cargarLista(), cargarResumen()]); } finally { bloquear(false); actualizarPaginacion(); } }
    function configurarEventos() { if (eventosConfigurados) return; eventosConfigurados = true; $('servicios-busqueda').addEventListener('input', function () { clearTimeout(temporizador); pagina = 1; temporizador = setTimeout(cargarLista, 350); }); $('servicios-estado').addEventListener('change', function () { pagina = 1; cargarLista(); }); $('servicios-limite').addEventListener('change', function () { pagina = 1; cargarLista(); }); $('btn-actualizar-servicios').addEventListener('click', async function () { bloquear(true); try { await Promise.all([cargarLista(), cargarResumen()]); avisar('Listado y resumen actualizados.', false); } finally { bloquear(false); actualizarPaginacion(); } }); $('btn-pagina-anterior').addEventListener('click', function () { if (pagina > 1) { pagina -= 1; cargarLista(); } }); $('btn-pagina-siguiente').addEventListener('click', function () { if (pagina < totalPaginas) { pagina += 1; cargarLista(); } }); $('btn-cerrar-detalle').addEventListener('click', function () { cerrarModal($('modal-detalle-servicio')); }); $('btn-cerrar-estado').addEventListener('click', function () { cerrarModal($('modal-estado-servicio')); }); $('btn-cancelar-estado').addEventListener('click', function () { cerrarModal($('modal-estado-servicio')); }); document.querySelector('[data-cerrar-detalle]').addEventListener('click', function () { cerrarModal($('modal-detalle-servicio')); }); document.querySelector('[data-cerrar-estado]').addEventListener('click', function () { cerrarModal($('modal-estado-servicio')); }); $('form-estado-servicio').addEventListener('submit', guardarEstado); document.addEventListener('keydown', function (e) { if (e.key === 'Escape') { if (!$('modal-detalle-servicio').hidden) cerrarModal($('modal-detalle-servicio')); if (!$('modal-estado-servicio').hidden) cerrarModal($('modal-estado-servicio')); } }); }
    async function iniciar() { $('servicios-contenido').hidden = false; configurarEventos(); bloquear(true); try { await Promise.all([cargarLista(), cargarResumen()]); } finally { bloquear(false); actualizarPaginacion(); } }
    ns.servicios = { iniciar: iniciar };
})(window.VisorSIG);
