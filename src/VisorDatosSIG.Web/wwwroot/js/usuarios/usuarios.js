// =============================================================================
// VisorDatosSIG 2026 — Módulo de Gestión de Usuarios (CU03)
// Frontend moderno, reactivo y accesible.
//
// Seguridad del renderizado: nunca se usa innerHTML; todo nodo se crea con
// document.createElement, los textos se asignan con textContent y los atributos
// con setAttribute. Compatible con modo claro/oscuro y WCAG 2.1 AA.
// =============================================================================

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var lista = [];
    var rolesCatalogo = [];
    var editando = null;
    var restableciendo = null;
    var estadoObjetivo = null;
    var ocupado = false;
    var notifTimeout = null;

    var el = function (id) { return document.getElementById(id); };

    // --- Notificaciones accesibles ---
    function notificar(texto, tipo) {
        var caja = el('gestion-mensaje');
        var textoEl = el('notif-texto');
        var iconoEl = el('notif-icono');
        if (!caja || !textoEl) return;

        if (notifTimeout) {
            clearTimeout(notifTimeout);
            notifTimeout = null;
        }

        if (!texto) {
            caja.hidden = true;
            return;
        }

        textoEl.textContent = texto;
        caja.className = 'usuarios-notificacion ' + (tipo === 'error' ? 'usuarios-notificacion--error' : 'usuarios-notificacion--exito');
        if (iconoEl) {
            iconoEl.textContent = tipo === 'error' ? '⚠️' : '✅';
        }
        caja.hidden = false;

        notifTimeout = setTimeout(function () {
            caja.hidden = true;
        }, 6000);
    }

    // --- Peticiones HTTP seguras hacia la API ---
    async function pedir(ruta, metodo, cuerpo) {
        var r = await ns.api.peticion('api/usuarios' + ruta, {
            metodo: metodo || 'GET',
            cuerpo: cuerpo,
            token: ns.sesion.obtenerToken(),
            protegida: true
        });

        if (!r.ok) {
            var errores = r.datos && r.datos.errors;
            var msj = (r.datos && r.datos.mensaje) ||
                      (errores && Object.values(errores).flat().join('\n')) ||
                      (r.status === 403 ? 'No tiene permisos para administrar usuarios.' : 'No se pudo completar la operación. Verifique la conexión.');
            notificar(msj, 'error');
        }

        return r;
    }

    function bloquear(valor) {
        ocupado = valor;
        document.querySelectorAll('.usuarios-seccion button, .usuarios-seccion input, .usuarios-seccion select').forEach(function (b) {
            b.disabled = valor;
        });
    }

    // --- Carga de datos ---
    async function cargar() {
        var r = await pedir('');
        if (r.ok && Array.isArray(r.datos)) {
            lista = r.datos;
            actualizarMetricas();
            dibujar();
        }
        return r.ok;
    }

    // --- Métricas operativas (KPIs) ---
    function actualizarMetricas() {
        var total = lista.length;
        var activos = lista.filter(function (u) { return u.activo; }).length;
        var inactivos = total - activos;
        var admins = lista.filter(function (u) {
            return u.roles && u.roles.some(function (r) { return r.toLowerCase().includes('admin'); });
        }).length;

        if (el('kpi-total')) el('kpi-total').textContent = total;
        if (el('kpi-activos')) el('kpi-activos').textContent = activos;
        if (el('kpi-inactivos')) el('kpi-inactivos').textContent = inactivos;
        if (el('kpi-admin')) el('kpi-admin').textContent = admins;
    }

    // --- Creación de avatares con iniciales ---
    function obtenerIniciales(nombre, login) {
        if (nombre && typeof nombre === 'string') {
            var partes = nombre.trim().split(/\s+/);
            if (partes.length >= 2) {
                return (partes[0].charAt(0) + partes[1].charAt(0)).toUpperCase();
            } else if (partes.length === 1 && partes[0].length > 0) {
                return partes[0].substring(0, 2).toUpperCase();
            }
        }
        return (login || 'US').substring(0, 2).toUpperCase();
    }

    // --- Resolución de clase visual para los chips de rol ---
    function obtenerClaseRol(rol) {
        var r = (rol || '').toLowerCase();
        if (r.includes('admin')) return 'rol-chip--admin';
        if (r.includes('supervisor')) return 'rol-chip--supervisor';
        if (r.includes('empleado')) return 'rol-chip--empleado';
        if (r.includes('consultor')) return 'rol-chip--consultor';
        if (r.includes('migrador')) return 'rol-chip--migrador';
        return 'rol-chip--generico';
    }

    // --- Renderizado de la tabla de usuarios ---
    function dibujar() {
        var tbody = el('usuarios-lista');
        if (!tbody) return;
        tbody.replaceChildren();

        var textoFiltro = (el('usuarios-buscar').value || '').trim().toLowerCase();
        var rolFiltro = el('usuarios-filtro-rol') ? el('usuarios-filtro-rol').value : '';
        var estadoFiltro = el('usuarios-filtro-estado') ? el('usuarios-filtro-estado').value : '';

        var visibles = lista.filter(function (u) {
            var coincideTexto = !textoFiltro ||
                (u.login && u.login.toLowerCase().includes(textoFiltro)) ||
                (u.nombre && u.nombre.toLowerCase().includes(textoFiltro)) ||
                (u.roles && u.roles.some(function (r) { return r.toLowerCase().includes(textoFiltro); }));

            var coincideRol = !rolFiltro || (u.roles && u.roles.includes(rolFiltro));
            var coincideEstado = estadoFiltro === '' ||
                (estadoFiltro === 'true' && u.activo) ||
                (estadoFiltro === 'false' && !u.activo);

            return coincideTexto && coincideRol && coincideEstado;
        });

        if (el('usuarios-conteo')) {
            el('usuarios-conteo').textContent = 'Mostrando ' + visibles.length + ' de ' + lista.length + ' usuarios';
        }

        if (visibles.length === 0) {
            var trVacio = document.createElement('tr');
            var tdVacio = document.createElement('td');
            tdVacio.colSpan = 5;
            tdVacio.className = 'tabla-vacia';

            var wrapper = document.createElement('div');
            wrapper.className = 'tabla-vacia__contenido';

            var icono = document.createElement('div');
            icono.className = 'tabla-vacia__icono';
            var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.setAttribute('viewBox', '0 0 24 24');
            svg.setAttribute('fill', 'none');
            svg.setAttribute('stroke', 'currentColor');
            svg.setAttribute('stroke-width', '1.5');
            var circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            circle.setAttribute('cx', '11');
            circle.setAttribute('cy', '11');
            circle.setAttribute('r', '8');
            var line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('x1', '21');
            line.setAttribute('y1', '21');
            line.setAttribute('x2', '16.65');
            line.setAttribute('y2', '16.65');
            svg.appendChild(circle);
            svg.appendChild(line);
            icono.appendChild(svg);

            var texto = document.createElement('span');
            texto.textContent = 'No se encontraron usuarios que coincidan con los filtros.';

            wrapper.appendChild(icono);
            wrapper.appendChild(texto);
            tdVacio.appendChild(wrapper);
            trVacio.appendChild(tdVacio);
            tbody.appendChild(trVacio);
            return;
        }

        visibles.forEach(function (u) {
            var tr = document.createElement('tr');

            // 1. Columna Usuario con Avatar
            var tdUsuario = document.createElement('td');
            var divPerfil = document.createElement('div');
            divPerfil.className = 'usuario-celda-perfil';

            var avatar = document.createElement('div');
            avatar.className = 'usuario-avatar';
            avatar.textContent = obtenerIniciales(u.nombre, u.login);

            var divTexto = document.createElement('div');
            divTexto.className = 'usuario-perfil-info';

            var loginSpan = document.createElement('span');
            loginSpan.className = 'usuario-login-text';
            loginSpan.textContent = u.login;

            divTexto.appendChild(loginSpan);
            divPerfil.appendChild(avatar);
            divPerfil.appendChild(divTexto);
            tdUsuario.appendChild(divPerfil);
            tr.appendChild(tdUsuario);

            // 2. Columna Nombre Completo
            var tdNombre = document.createElement('td');
            tdNombre.textContent = u.nombre || '—';
            tr.appendChild(tdNombre);

            // 3. Columna Roles
            var tdRoles = document.createElement('td');
            var divRoles = document.createElement('div');
            divRoles.className = 'roles-lista-celda';

            if (u.roles && u.roles.length > 0) {
                u.roles.forEach(function (rol) {
                    var chip = document.createElement('span');
                    chip.className = 'rol-chip ' + obtenerClaseRol(rol);
                    chip.textContent = rol;
                    divRoles.appendChild(chip);
                });
            } else {
                var sinRol = document.createElement('span');
                sinRol.className = 'rol-chip rol-chip--generico';
                sinRol.textContent = 'Sin roles';
                divRoles.appendChild(sinRol);
            }
            tdRoles.appendChild(divRoles);
            tr.appendChild(tdRoles);

            // 4. Columna Estado
            var tdEstado = document.createElement('td');
            var badge = document.createElement('span');
            badge.className = 'estado-badge ' + (u.activo ? 'estado-badge--activo' : 'estado-badge--inactivo');

            var punto = document.createElement('span');
            punto.className = 'estado-badge__punto';
            badge.appendChild(punto);
            badge.appendChild(document.createTextNode(u.activo ? 'Conectado' : 'Desconectado'));
            tdEstado.appendChild(badge);
            tr.appendChild(tdEstado);

            // 5. Columna Acciones
            var tdAcciones = document.createElement('td');
            var grupoAcciones = document.createElement('div');
            grupoAcciones.className = 'acciones-grupo';

            // Botón Editar
            var btnEditar = document.createElement('button');
            btnEditar.type = 'button';
            btnEditar.className = 'btn-accion btn-accion--editar';
            btnEditar.title = 'Editar usuario';
            btnEditar.setAttribute('aria-label', 'Editar usuario ' + u.login);
            btnEditar.innerHTML = '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path></svg>';
            btnEditar.addEventListener('click', function () { abrirEditar(u); });
            grupoAcciones.appendChild(btnEditar);

            // Botón Cambiar Estado
            var btnEstado = document.createElement('button');
            btnEstado.type = 'button';
            btnEstado.className = 'btn-accion btn-accion--estado';
            btnEstado.title = u.activo ? 'Desconectar usuario' : 'Conectar usuario';
            btnEstado.setAttribute('aria-label', (u.activo ? 'Desconectar' : 'Conectar') + ' a ' + u.login);
            btnEstado.innerHTML = u.activo
                ? '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="4.93" y1="4.93" x2="19.07" y2="19.07"></line></svg>'
                : '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>';
            btnEstado.addEventListener('click', function () { abrirConfirmarEstado(u); });
            grupoAcciones.appendChild(btnEstado);

            // Botón Restablecer Contraseña
            var btnReset = document.createElement('button');
            btnReset.type = 'button';
            btnReset.className = 'btn-accion btn-accion--password';
            btnReset.title = 'Restablecer contraseña';
            btnReset.setAttribute('aria-label', 'Restablecer contraseña de ' + u.login);
            btnReset.innerHTML = '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect><path d="M7 11V7a5 5 0 0 1 10 0v4"></path></svg>';
            btnReset.addEventListener('click', function () { abrirReset(u); });
            grupoAcciones.appendChild(btnReset);

            tdAcciones.appendChild(grupoAcciones);
            tr.appendChild(tdAcciones);

            tbody.appendChild(tr);
        });
    }

    // --- Control de Modales ---
    function abrirModal(id) {
        var modal = el(id);
        if (modal) {
            modal.hidden = false;
            document.body.classList.add('modal-abierto');
        }
    }

    function cerrarModal(id) {
        var modal = el(id);
        if (modal) {
            modal.hidden = true;
            document.body.classList.remove('modal-abierto');
        }
    }

    function cerrarTodosModales() {
        document.querySelectorAll('[data-modal]').forEach(function (m) {
            m.hidden = true;
        });
        document.body.classList.remove('modal-abierto');
    }

    // --- Modal Crear / Editar ---
    function abrirCrear() {
        editando = null;
        el('usuario-form').reset();
        el('usuario-titulo').textContent = 'Crear nuevo usuario';
        el('usuario-subtitulo').textContent = 'Complete los datos de la cuenta y asigne los roles de acceso.';
        el('usuario-login').disabled = false;
        el('password-inicial-grupo').hidden = false;
        el('usuario-password').required = true;
        el('modal-usuario-error').hidden = true;

        el('usuario-roles').querySelectorAll('input').forEach(function (c) {
            c.checked = false;
        });

        abrirModal('modal-usuario');
        setTimeout(function () { el('usuario-login').focus(); }, 50);
    }

    function abrirEditar(u) {
        editando = u.idUsuario;
        el('usuario-form').reset();
        el('usuario-titulo').textContent = 'Editar usuario: ' + u.login;
        el('usuario-subtitulo').textContent = 'Modifique el nombre o los roles asignados a esta cuenta.';
        el('usuario-login').value = u.login;
        el('usuario-login').disabled = true;
        el('usuario-nombre').value = u.nombre;
        el('password-inicial-grupo').hidden = true;
        el('usuario-password').required = false;
        el('modal-usuario-error').hidden = true;

        el('usuario-roles').querySelectorAll('input').forEach(function (c) {
            c.checked = u.roles && u.roles.includes(c.value);
        });

        abrirModal('modal-usuario');
        setTimeout(function () { el('usuario-nombre').focus(); }, 50);
    }

    // --- Modal Restablecer Contraseña ---
    function abrirReset(u) {
        restableciendo = u.idUsuario;
        el('reset-form').reset();
        el('reset-titulo').textContent = 'Restablecer contraseña';
        el('reset-subtitulo').textContent = 'Asigne una nueva credencial para el usuario ' + u.login + '.';
        el('modal-reset-error').hidden = true;

        abrirModal('modal-reset');
        setTimeout(function () { el('reset-password').focus(); }, 50);
    }

    // --- Modal Confirmar Estado ---
    function abrirConfirmarEstado(u) {
        estadoObjetivo = u;
        var nuevoEstado = !u.activo;
        var accion = nuevoEstado ? 'conectar' : 'desconectar';

        el('estado-titulo').textContent = (nuevoEstado ? 'Conectar' : 'Desconectar') + ' usuario';
        el('estado-mensaje-pregunta').innerHTML = '¿Está seguro de que desea ' + accion + ' la sesión del usuario <strong>' + u.login + '</strong> (' + (u.nombre || 'Sin nombre') + ')?' +
            (nuevoEstado ? ' El usuario figurará como conectado en el sistema.' : ' La sesión del usuario finalizará inmediatamente.');

        abrirModal('modal-estado');
    }

    // --- Construcción dinámica de casillas de roles ---
    function pintarRoles(datos) {
        rolesCatalogo = datos || [];
        var contenedor = el('usuario-roles');
        var selectFiltro = el('usuarios-filtro-rol');
        if (!contenedor) return;

        contenedor.replaceChildren();

        // Opciones del select de filtro en toolbar
        if (selectFiltro) {
            selectFiltro.replaceChildren();
            var optTodos = document.createElement('option');
            optTodos.value = '';
            optTodos.textContent = 'Todos los roles';
            selectFiltro.appendChild(optTodos);

            rolesCatalogo.forEach(function (rol) {
                var opt = document.createElement('option');
                opt.value = rol;
                opt.textContent = rol;
                selectFiltro.appendChild(opt);
            });
        }

        // Tarjetas interactivas de checkbox en el formulario
        rolesCatalogo.forEach(function (rol) {
            var label = document.createElement('label');
            label.className = 'rol-card-label';

            var check = document.createElement('input');
            check.type = 'checkbox';
            check.value = rol;

            var spanTexto = document.createElement('span');
            spanTexto.textContent = rol;

            label.appendChild(check);
            label.appendChild(spanTexto);
            contenedor.appendChild(label);
        });
    }

    // --- Enlace de eventos del DOM ---
    function enlazarEventos() {
        // Abrir modal de nuevo usuario
        var btnNuevo = el('btn-abrir-crear');
        if (btnNuevo) btnNuevo.addEventListener('click', abrirCrear);

        // Recargar listado
        var btnRecargar = el('usuarios-recargar');
        if (btnRecargar) {
            btnRecargar.addEventListener('click', async function () {
                if (ocupado) return;
                bloquear(true);
                try {
                    if (await cargar()) notificar('Listado de usuarios actualizado.', 'exito');
                } finally {
                    bloquear(false);
                }
            });
        }

        // Filtros en tiempo real
        var inputBuscar = el('usuarios-buscar');
        if (inputBuscar) inputBuscar.addEventListener('input', dibujar);

        var filtroRol = el('usuarios-filtro-rol');
        if (filtroRol) filtroRol.addEventListener('change', dibujar);

        var filtroEstado = el('usuarios-filtro-estado');
        if (filtroEstado) filtroEstado.addEventListener('change', dibujar);

        // Cerrar notificación
        var btnCerrarNotif = el('notif-cerrar');
        if (btnCerrarNotif) {
            btnCerrarNotif.addEventListener('click', function () {
                el('gestion-mensaje').hidden = true;
            });
        }

        // Botones de cancelar en modales
        if (el('btn-cerrar-modal')) el('btn-cerrar-modal').addEventListener('click', function () { cerrarModal('modal-usuario'); });
        if (el('usuario-cancelar')) el('usuario-cancelar').addEventListener('click', function () { cerrarModal('modal-usuario'); });

        if (el('btn-cerrar-reset')) el('btn-cerrar-reset').addEventListener('click', function () { cerrarModal('modal-reset'); });
        if (el('reset-cancelar')) el('reset-cancelar').addEventListener('click', function () { cerrarModal('modal-reset'); });

        if (el('btn-cerrar-estado')) el('btn-cerrar-estado').addEventListener('click', function () { cerrarModal('modal-estado'); });
        if (el('btn-cancelar-estado')) el('btn-cancelar-estado').addEventListener('click', function () { cerrarModal('modal-estado'); });

        // Cierre al hacer clic en el backdrop oscuro
        if (el('modal-usuario-fondo')) el('modal-usuario-fondo').addEventListener('click', function () { cerrarModal('modal-usuario'); });
        if (el('modal-reset-fondo')) el('modal-reset-fondo').addEventListener('click', function () { cerrarModal('modal-reset'); });
        if (el('modal-estado-fondo')) el('modal-estado-fondo').addEventListener('click', function () { cerrarModal('modal-estado'); });

        // Cierre con tecla Escape
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') cerrarTodosModales();
        });

        // Alternadores de visibilidad de contraseñas (ojito)
        document.querySelectorAll('.btn-toggle-pass').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var targetId = btn.getAttribute('data-target');
                var input = el(targetId);
                if (input) {
                    var esPass = input.type === 'password';
                    input.type = esPass ? 'text' : 'password';
                    btn.innerHTML = esPass
                        ? '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"></path><line x1="1" y1="1" x2="23" y2="23"></line></svg>'
                        : '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path><circle cx="12" cy="12" r="3"></circle></svg>';
                }
            });
        });

        // Guardar Usuario (Crear / Editar)
        var formUsuario = el('usuario-form');
        if (formUsuario) {
            formUsuario.addEventListener('submit', async function (e) {
                e.preventDefault();
                if (ocupado) return;

                var errorEl = el('modal-usuario-error');
                errorEl.hidden = true;

                var rolesSeleccionados = Array.from(el('usuario-roles').querySelectorAll('input:checked')).map(function (c) {
                    return c.value;
                });

                if (!rolesSeleccionados.length) {
                    errorEl.textContent = 'Debe seleccionar al menos un rol de acceso.';
                    errorEl.hidden = false;
                    return;
                }

                var cuerpo = {
                    nombre: el('usuario-nombre').value.trim(),
                    roles: rolesSeleccionados
                };

                if (!editando) {
                    cuerpo.login = el('usuario-login').value.trim();
                    cuerpo.passwordInicial = el('usuario-password').value;

                    if (!cuerpo.login) {
                        errorEl.textContent = 'Ingrese el nombre de usuario (login).';
                        errorEl.hidden = false;
                        return;
                    }

                    if (!cuerpo.passwordInicial || cuerpo.passwordInicial.length < 8) {
                        errorEl.textContent = 'La contraseña inicial debe tener al menos 8 caracteres.';
                        errorEl.hidden = false;
                        return;
                    }
                }

                bloquear(true);
                try {
                    var r = await pedir(editando ? '/' + editando : '', editando ? 'PUT' : 'POST', cuerpo);
                    if (r.ok) {
                        cerrarModal('modal-usuario');
                        if (await cargar()) {
                            notificar('Usuario ' + (editando ? 'actualizado' : 'creado') + ' correctamente.', 'exito');
                        }
                    } else {
                        var msj = (r.datos && r.datos.mensaje) || 'No se pudo guardar el usuario.';
                        errorEl.textContent = msj;
                        errorEl.hidden = false;
                    }
                } finally {
                    bloquear(false);
                }
            });
        }

        // Restablecer Contraseña
        var formReset = el('reset-form');
        if (formReset) {
            formReset.addEventListener('submit', async function (e) {
                e.preventDefault();
                if (ocupado || !restableciendo) return;

                var errorEl = el('modal-reset-error');
                errorEl.hidden = true;

                var p1 = el('reset-password').value;
                var p2 = el('reset-confirmar').value;

                if (!p1 || p1.length < 8) {
                    errorEl.textContent = 'La nueva contraseña debe tener al menos 8 caracteres.';
                    errorEl.hidden = false;
                    return;
                }

                if (p1 !== p2) {
                    errorEl.textContent = 'Las contraseñas no coinciden.';
                    errorEl.hidden = false;
                    return;
                }

                bloquear(true);
                try {
                    var r = await pedir('/' + restableciendo + '/restablecer-password', 'POST', { nuevaPassword: p1 });
                    if (r.ok) {
                        cerrarModal('modal-reset');
                        restableciendo = null;
                        notificar('Contraseña restablecida exitosamente.', 'exito');
                    } else {
                        var msj = (r.datos && r.datos.mensaje) || 'No se pudo restablecer la contraseña.';
                        errorEl.textContent = msj;
                        errorEl.hidden = false;
                    }
                } finally {
                    bloquear(false);
                }
            });
        }

        // Confirmar Cambio de Estado
        var btnConfirmarEstado = el('btn-confirmar-estado');
        if (btnConfirmarEstado) {
            btnConfirmarEstado.addEventListener('click', async function () {
                if (ocupado || !estadoObjetivo) return;
                var u = estadoObjetivo;
                bloquear(true);
                try {
                    var r = await pedir('/' + u.idUsuario + '/estado', 'PATCH', { activo: !u.activo });
                    if (r.ok) {
                        cerrarModal('modal-estado');
                        estadoObjetivo = null;
                        if (await cargar()) {
                            notificar('Estado del usuario ' + u.login + ' actualizado.', 'exito');
                        }
                    }
                } finally {
                    bloquear(false);
                }
            });
        }
    }

    // --- Arranque del módulo ---
    async function iniciar() {
        if (!el('usuarios-lista')) return;
        bloquear(true);
        try {
            var r = await pedir('/roles');
            if (!r.ok) return;
            pintarRoles(r.datos);
            if (!await cargar()) return;
            el('gestion-contenido').hidden = false;
            enlazarEventos();
        } finally {
            bloquear(false);
        }
    }

    ns.usuarios = { iniciar: iniciar };
})(window.VisorSIG);
