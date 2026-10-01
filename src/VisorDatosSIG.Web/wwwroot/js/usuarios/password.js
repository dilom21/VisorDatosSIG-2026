(function (ns) {
    'use strict';
    var el = function (id) { return document.getElementById(id); };
    var trigger = el('btn-menu-usuario'), menu = el('menu-usuario'), dialog = el('password-modal');
    if (!trigger || !menu || !dialog) { return; }
    var form = el('password-form'), fields = el('password-campos'), save = el('password-guardar');
    var cancel = el('password-cancelar'), close = el('password-cerrar'), retry = el('password-reintentar');
    var saving = false, verified = false, revision = 0;

    function closeMenu(restoreFocus) {
        menu.hidden = true; trigger.setAttribute('aria-expanded', 'false');
        if (restoreFocus) { trigger.focus(); }
    }
    function openMenu() { menu.hidden = false; trigger.setAttribute('aria-expanded', 'true'); }
    function message(text, type) {
        var output = el('password-mensaje');
        output.textContent = text; output.hidden = !text; output.dataset.tipo = type || 'info';
    }
    function authenticated() {
        return ns.sesion.estaAutenticado() && !ns.sesion.estaExpirada();
    }
    function closeModal() { if (!saving && dialog.open) { dialog.close(); } }
    async function verify() {
        var current = ++revision;
        verified = false; fields.disabled = true; save.disabled = true; retry.hidden = true;
        message('Verificando sesión…');
        var result = await ns.api.obtenerUsuarioActual(ns.sesion.obtenerToken());
        if (!dialog.open || current !== revision) { return; }
        if (result.ok && result.datos) {
            ns.sesion.actualizarUsuario(result.datos); ns.guard.actualizarHeader();
            verified = true; fields.disabled = false; save.disabled = false; message(''); el('actual').focus();
        } else {
            message('No se pudo verificar la sesión. Intente nuevamente.', 'error'); retry.hidden = false;
        }
    }
    function openModal() {
        closeMenu(false);
        if (!authenticated()) { ns.guard.redirigirALogin(window.location.pathname + window.location.search, 'expirada'); return; }
        if (dialog.open) { return; }
        form.reset(); message(''); cancel.textContent = 'Cancelar'; save.textContent = 'Guardar contraseña';
        dialog.showModal(); document.body.classList.add('password-modal-abierto');
        verify();
    }
    trigger.addEventListener('click', function () { if (menu.hidden) { openMenu(); } else { closeMenu(false); } });
    trigger.addEventListener('keydown', function (e) {
        if (e.key === 'ArrowDown') { e.preventDefault(); openMenu(); el('btn-cambiar-password').focus(); }
        if (e.key === 'Escape' && !menu.hidden) { e.preventDefault(); closeMenu(true); }
    });
    document.addEventListener('click', function (e) { if (!el('sesion-activa').contains(e.target)) { closeMenu(false); } });
    menu.addEventListener('keydown', function (e) { if (e.key === 'Escape') { e.preventDefault(); closeMenu(true); } });
    el('sesion-activa').addEventListener('focusout', function (e) {
        if (!el('sesion-activa').contains(e.relatedTarget)) { closeMenu(false); }
    });
    el('btn-cambiar-password').addEventListener('click', openModal);
    el('btn-cerrar-sesion').addEventListener('click', function () { closeMenu(false); });
    close.addEventListener('click', closeModal); cancel.addEventListener('click', closeModal);
    retry.addEventListener('click', verify);
    dialog.addEventListener('cancel', function (e) { if (saving) { e.preventDefault(); } });
    dialog.addEventListener('click', function (e) {
        if (e.target !== dialog) { return; }
        var box = dialog.getBoundingClientRect();
        if (e.clientX < box.left || e.clientX > box.right || e.clientY < box.top || e.clientY > box.bottom) { closeModal(); }
    });
    dialog.addEventListener('close', function () {
        ++revision; verified = false; form.reset(); message('');
        document.body.classList.remove('password-modal-abierto'); trigger.focus();
    });
    form.addEventListener('submit', async function (e) {
        e.preventDefault();
        if (saving || !verified || !dialog.open) { return; }
        if (!authenticated()) { closeModal(); ns.guard.redirigirALogin(window.location.pathname + window.location.search, 'expirada'); return; }
        var actual = el('actual').value, nueva = el('nueva').value, confirmar = el('confirmacion').value;
        if (nueva !== confirmar) { message('Las contraseñas no coinciden.', 'error'); el('confirmacion').focus(); return; }
        if (actual === nueva) { message('Use una contraseña diferente a la actual.', 'error'); el('nueva').focus(); return; }
        saving = true; fields.disabled = true; save.disabled = true; cancel.disabled = true; close.disabled = true;
        save.textContent = 'Guardando…'; form.setAttribute('aria-busy', 'true');
        try {
            var result = await ns.api.peticion('api/perfil/cambiar-password', {
                metodo: 'PUT', token: ns.sesion.obtenerToken(), protegida: true,
                cuerpo: { passwordActual: actual, passwordNueva: nueva, confirmarPassword: confirmar }
            });
            var errors = result.datos && result.datos.errors;
            if (result.ok) {
                form.reset(); verified = false; cancel.textContent = 'Cerrar';
                message('Contraseña actualizada exitosamente.', 'exito'); cancel.focus();
            } else {
                message((result.datos && result.datos.mensaje) || (errors && Object.values(errors).flat().join('\n')) || 'No se pudo actualizar la contraseña. Intente nuevamente.', 'error');
            }
        } finally {
            saving = false; fields.disabled = !verified; save.disabled = !verified;
            cancel.disabled = false; close.disabled = false; save.textContent = 'Guardar contraseña'; form.removeAttribute('aria-busy');
            if (!verified) { cancel.focus(); }
        }
    });
    if ((new URLSearchParams(window.location.search).get('cambiarPassword') || '').toLowerCase() === 'true') {
        openModal();
        if (dialog.open) {
            var url = new URL(window.location.href); url.searchParams.delete('cambiarPassword');
            window.history.replaceState(null, '', url.pathname + url.search + url.hash);
        }
    }
})(window.VisorSIG);
