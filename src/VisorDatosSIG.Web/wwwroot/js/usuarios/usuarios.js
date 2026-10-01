(function (ns) {
    'use strict';
    var lista = [], editando = null, restableciendo = null, ocupado = false;
    var el = function (id) { return document.getElementById(id); };
    function mensaje(texto) { el('gestion-mensaje').textContent = texto; }
    async function pedir(ruta, metodo, cuerpo) {
        var r = await ns.api.peticion('api/usuarios' + ruta, {
            metodo: metodo || 'GET', cuerpo: cuerpo, token: ns.sesion.obtenerToken(), protegida: true
        });
        if (!r.ok) {
            var errores = r.datos && r.datos.errors;
            mensaje((r.datos && r.datos.mensaje) || (errores && Object.values(errores).flat().join('\n')) ||
                (r.status === 403 ? 'No tiene permisos para administrar usuarios.' : 'No se pudo completar la operación. Verifique la conexión e intente nuevamente.'));
        }
        return r;
    }
    function bloquear(valor) {
        ocupado = valor;
        document.querySelectorAll('.gestion button').forEach(function (b) { b.disabled = valor; });
    }
    async function cargar() {
        var r = await pedir('');
        if (r.ok) { lista = r.datos; dibujar(); }
        return r.ok;
    }
    function boton(texto, accion) {
        var b = document.createElement('button'); b.type = 'button'; b.className = 'btn-secundario';
        b.textContent = texto; b.disabled = ocupado; b.addEventListener('click', accion); return b;
    }
    function dibujar() {
        el('usuarios-lista').replaceChildren();
        var filtro = el('usuarios-buscar').value.trim().toLocaleLowerCase();
        var visibles = lista.filter(function (u) { return (u.login + ' ' + u.nombre).toLocaleLowerCase().includes(filtro); });
        visibles.forEach(function (u) {
            var fila = document.createElement('tr');
            [u.login, u.nombre, u.roles.join(', '), u.activo ? 'Activo' : 'Inactivo'].forEach(function (texto) {
                var celda = document.createElement('td'); celda.textContent = texto; fila.appendChild(celda);
            });
            var acciones = document.createElement('td');
            acciones.appendChild(boton('Editar', function () { editar(u); }));
            acciones.appendChild(boton(u.activo ? 'Desactivar' : 'Activar', async function () {
                if (ocupado || !window.confirm((u.activo ? '¿Desactivar' : '¿Activar') + ' a ' + u.login + '?')) { return; }
                bloquear(true);
                try {
                    var r = await pedir('/' + u.idUsuario + '/estado', 'PATCH', { activo: !u.activo });
                    if (r.ok && await cargar()) { mensaje('Estado actualizado.'); }
                } finally { bloquear(false); }
            }));
            acciones.appendChild(boton('Restablecer contraseña', function () {
                restableciendo = u.idUsuario; el('reset-form').reset(); el('reset-form').hidden = false;
                el('reset-titulo').textContent = 'Restablecer contraseña de ' + u.login; el('reset-password').focus();
            }));
            fila.appendChild(acciones); el('usuarios-lista').appendChild(fila);
        });
        if (!visibles.length) {
            var fila = document.createElement('tr'), celda = document.createElement('td');
            celda.colSpan = 5; celda.textContent = 'No se encontraron usuarios.'; fila.appendChild(celda); el('usuarios-lista').appendChild(fila);
        }
    }
    function editar(u) {
        editando = u.idUsuario; el('usuario-titulo').textContent = 'Editar usuario: ' + u.login;
        el('usuario-login').value = u.login; el('usuario-login').disabled = true;
        el('usuario-nombre').value = u.nombre; el('usuario-password').value = '';
        el('usuario-password').required = false; el('password-inicial-grupo').hidden = true;
        el('usuario-roles').querySelectorAll('input').forEach(function (c) { c.checked = u.roles.includes(c.value); });
        el('usuario-nombre').focus();
    }
    function limpiarEdicion() {
        editando = null; el('usuario-form').reset(); el('usuario-login').disabled = false;
        el('usuario-titulo').textContent = 'Crear usuario'; el('usuario-password').required = true; el('password-inicial-grupo').hidden = false;
    }
    el('usuarios-buscar').addEventListener('input', dibujar);
    el('usuarios-recargar').addEventListener('click', async function () {
        if (ocupado) { return; } bloquear(true);
        try { if (await cargar()) { mensaje('Listado actualizado.'); } } finally { bloquear(false); }
    });
    el('usuario-cancelar').addEventListener('click', limpiarEdicion);
    el('reset-cancelar').addEventListener('click', function () { restableciendo = null; el('reset-form').reset(); el('reset-form').hidden = true; });
    el('usuario-form').addEventListener('submit', async function (e) {
        e.preventDefault(); if (ocupado) { return; }
        var roles = Array.from(el('usuario-roles').querySelectorAll('input:checked')).map(function (c) { return c.value; });
        if (!roles.length) { mensaje('Seleccione al menos un rol.'); return; }
        var cuerpo = { nombre: el('usuario-nombre').value.trim(), roles: roles };
        if (!editando) { cuerpo.login = el('usuario-login').value.trim(); cuerpo.passwordInicial = el('usuario-password').value; }
        bloquear(true);
        try {
            var r = await pedir(editando ? '/' + editando : '', editando ? 'PUT' : 'POST', cuerpo);
            if (r.ok) { limpiarEdicion(); if (await cargar()) { mensaje('Usuario guardado.'); } }
        } finally { bloquear(false); }
    });
    el('reset-form').addEventListener('submit', async function (e) {
        e.preventDefault(); if (ocupado || !restableciendo) { return; }
        if (el('reset-password').value !== el('reset-confirmar').value) { mensaje('Las contraseñas no coinciden.'); return; }
        bloquear(true);
        try {
            var r = await pedir('/' + restableciendo + '/restablecer-password', 'POST', { nuevaPassword: el('reset-password').value });
            if (r.ok) { el('reset-form').reset(); el('reset-form').hidden = true; restableciendo = null; mensaje('Contraseña restablecida.'); }
        } finally { bloquear(false); }
    });
    ns.guard.protegerPagina({ rol: 'Administrador', alValidar: async function () {
        document.querySelector('.gestion-contenido').hidden = false; bloquear(true);
        try {
            var r = await pedir('/roles');
            if (r.ok) {
                el('usuario-roles').querySelectorAll('label').forEach(function (n) { n.remove(); });
                r.datos.forEach(function (rol) {
                    var label = document.createElement('label'), check = document.createElement('input');
                    check.type = 'checkbox'; check.value = rol; label.appendChild(check); label.appendChild(document.createTextNode(rol)); el('usuario-roles').appendChild(label);
                });
                await cargar();
            }
        } finally { bloquear(false); }
    } });
})(window.VisorSIG);
