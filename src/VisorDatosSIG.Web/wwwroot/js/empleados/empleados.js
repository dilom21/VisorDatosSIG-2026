/**
 * VisorDatosSIG 2026 — Módulo de Gestión de Empleados (CU04)
 * Maneja la interacción con /api/empleados, filtros, métricas, altas, ediciones,
 * cambios de estado y gestión de disponibilidad operativa.
 */

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var lista = [];
    var editandoId = null;
    var dispTargetId = null;
    var ocupado = false;

    var el = function (id) {
        return document.getElementById(id);
    };

    function mensaje(texto, esError) {
        var aviso = el('gestion-mensaje');
        if (!aviso) return;
        aviso.textContent = texto || '';
        aviso.hidden = !aviso.textContent;
        aviso.className = 'visor-aviso gestion__mensaje ' + (esError ? 'visor-aviso--error' : 'visor-aviso--exito');
        if (texto && !esError) {
            setTimeout(function () {
                if (aviso.textContent === texto) {
                    aviso.hidden = true;
                }
            }, 6000);
        }
    }

    async function pedir(ruta, metodo, cuerpo) {
        var res = await ns.api.peticion('api/empleados' + ruta, {
            metodo: metodo || 'GET',
            cuerpo: cuerpo,
            token: ns.sesion.obtenerToken(),
            protegida: true
        });

        if (!res.ok) {
            var msj = (res.datos && res.datos.mensaje) ||
                      (res.datos && res.datos.detail) ||
                      (res.status === 403 ? 'No cuenta con autorización para gestionar empleados.' :
                       res.status === 404 ? 'El empleado solicitado no se encuentra disponible.' :
                       'Ocurrió un error al procesar la solicitud.');
            mensaje(msj, true);
        }

        return res;
    }

    function bloquear(valor) {
        ocupado = valor;
        document.querySelectorAll('.empleados-seccion button, .empleados-seccion select, .empleados-seccion input').forEach(function (b) {
            b.disabled = valor;
        });
    }

    async function cargarMetricas() {
        var res = await pedir('/metricas');
        if (res.ok && res.datos) {
            el('kpi-total').textContent = res.datos.total || 0;
            el('kpi-disponibles').textContent = res.datos.disponibles || 0;
            el('kpi-servicio').textContent = res.datos.enServicio || 0;
            el('kpi-bajas').textContent = res.datos.bajasParciales || 0;
        }
    }

    async function cargarCatalogos() {
        var resCargos = await pedir('/cargos');
        if (resCargos.ok && Array.isArray(resCargos.datos)) {
            var selectFiltro = el('filtro-cargo');
            var datalist = el('dl-cargos');
            datalist.replaceChildren();

            resCargos.datos.forEach(function (c) {
                var optFiltro = document.createElement('option');
                optFiltro.value = c;
                optFiltro.textContent = c;
                selectFiltro.appendChild(optFiltro);

                var optDl = document.createElement('option');
                optDl.value = c;
                datalist.appendChild(optDl);
            });
        }

        var resDisp = await pedir('/disponibilidades');
        if (resDisp.ok && Array.isArray(resDisp.datos)) {
            var selectDisp = el('filtro-disponibilidad');
            resDisp.datos.forEach(function (d) {
                var opt = document.createElement('option');
                opt.value = d;
                opt.textContent = d;
                selectDisp.appendChild(opt);
            });
        }
    }

    async function cargarEmpleados() {
        var params = new URLSearchParams();
        var q = el('empleados-buscar').value.trim();
        var cargo = el('filtro-cargo').value;
        var disp = el('filtro-disponibilidad').value;
        var estado = el('filtro-estado').value;

        if (q) params.set('busqueda', q);
        if (cargo) params.set('cargo', cargo);
        if (disp) params.set('disponibilidad', disp);
        if (estado !== '') params.set('activo', estado);

        var query = params.toString();
        var res = await pedir(query ? '?' + query : '');

        if (res.ok && Array.isArray(res.datos)) {
            lista = res.datos;
            dibujarTabla();
        }
        return res.ok;
    }

    function obtenerIniciales(nombre, apellido) {
        var n = (nombre || '').trim().charAt(0);
        var a = (apellido || '').trim().charAt(0);
        return (n + a).toUpperCase() || 'EM';
    }

    function claseDisponibilidad(disp) {
        var d = (disp || '').toLowerCase().replace(/\s+/g, '');
        if (d.includes('disponible')) return 'badge-disponibilidad--disponible';
        if (d.includes('servicio')) return 'badge-disponibilidad--enservicio';
        if (d.includes('baja')) return 'badge-disponibilidad--bajaparcial';
        if (d.includes('licencia')) return 'badge-disponibilidad--licencia';
        if (d.includes('vacacion')) return 'badge-disponibilidad--vacaciones';
        return 'badge-disponibilidad--otro';
    }

    function dibujarTabla() {
        var tbody = el('empleados-lista');
        tbody.replaceChildren();

        if (lista.length === 0) {
            var trVacio = document.createElement('tr');
            var tdVacio = document.createElement('td');
            tdVacio.colSpan = 7;
            tdVacio.innerHTML = `
                <div class="empleados-vacio">
                    <svg class="empleados-vacio__icono" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
                        <circle cx="12" cy="12" r="10"></circle>
                        <path d="M16 16s-1.5-2-4-2-4 2-4 2"></path>
                        <line x1="9" y1="9" x2="9.01" y2="9"></line>
                        <line x1="15" y1="9" x2="15.01" y2="9"></line>
                    </svg>
                    <h3 class="empleados-vacio__titulo">No se encontraron empleados</h3>
                    <p class="empleados-vacio__desc">No hay registros que coincidan con los criterios de búsqueda o filtros seleccionados.</p>
                </div>
            `;
            trVacio.appendChild(tdVacio);
            tbody.appendChild(trVacio);
            return;
        }

        lista.forEach(function (emp) {
            var tr = document.createElement('tr');

            // Código
            var tdCodigo = document.createElement('td');
            var spanCodigo = document.createElement('span');
            spanCodigo.className = 'empleado-codigo-badge';
            spanCodigo.textContent = emp.codigo;
            tdCodigo.appendChild(spanCodigo);
            tr.appendChild(tdCodigo);

            // Empleado (Avatar + Nombre + Observaciones/Notas)
            var tdEmpleado = document.createElement('td');
            var divInfo = document.createElement('div');
            divInfo.className = 'empleado-info-celda';

            var divAvatar = document.createElement('div');
            divAvatar.className = 'empleado-avatar';
            divAvatar.textContent = obtenerIniciales(emp.nombres, emp.apellidos);

            var divTexto = document.createElement('div');
            divTexto.className = 'empleado-info-texto';
            var spanNombre = document.createElement('span');
            spanNombre.className = 'empleado-nombre';
            spanNombre.textContent = emp.nombreCompleto;
            var spanObs = document.createElement('span');
            spanObs.className = 'empleado-subtexto';
            spanObs.textContent = emp.observaciones || 'Sin observaciones registradas';

            divTexto.appendChild(spanNombre);
            divTexto.appendChild(spanObs);
            divInfo.appendChild(divAvatar);
            divInfo.appendChild(divTexto);
            tdEmpleado.appendChild(divInfo);
            tr.appendChild(tdEmpleado);

            // Documento / Contacto
            var tdContacto = document.createElement('td');
            var divCont = document.createElement('div');
            divCont.className = 'empleado-info-texto';
            var spanDoc = document.createElement('span');
            spanDoc.style.fontWeight = '600';
            spanDoc.textContent = 'CI: ' + emp.documentoIdentidad;
            var spanTel = document.createElement('span');
            spanTel.className = 'empleado-subtexto';
            spanTel.textContent = emp.telefono ? 'Tel: ' + emp.telefono : (emp.email || '—');
            divCont.appendChild(spanDoc);
            divCont.appendChild(spanTel);
            tdContacto.appendChild(divCont);
            tr.appendChild(tdContacto);

            // Cargo y Área
            var tdCargo = document.createElement('td');
            var divCargo = document.createElement('div');
            divCargo.className = 'empleado-info-texto';
            var spanCargo = document.createElement('span');
            spanCargo.style.fontWeight = '700';
            spanCargo.textContent = emp.cargo;
            var spanArea = document.createElement('span');
            spanArea.className = 'empleado-subtexto';
            spanArea.textContent = emp.area;
            divCargo.appendChild(spanCargo);
            divCargo.appendChild(spanArea);
            tdCargo.appendChild(divCargo);
            tr.appendChild(tdCargo);

            // Disponibilidad
            var tdDisp = document.createElement('td');
            var badgeDisp = document.createElement('span');
            badgeDisp.className = 'badge-disponibilidad ' + claseDisponibilidad(emp.disponibilidad);
            badgeDisp.textContent = emp.disponibilidad;
            tdDisp.appendChild(badgeDisp);
            tr.appendChild(tdDisp);

            // Estado (Activo / Inactivo)
            var tdEstado = document.createElement('td');
            var badgeEst = document.createElement('span');
            badgeEst.className = 'badge-estado ' + (emp.activo ? 'badge-estado--activo' : 'badge-estado--inactivo');
            badgeEst.textContent = emp.activo ? 'Activo' : 'Inactivo';
            tdEstado.appendChild(badgeEst);
            tr.appendChild(tdEstado);

            // Acciones
            var tdAcciones = document.createElement('td');
            var divAcciones = document.createElement('div');
            divAcciones.className = 'empleado-acciones';
            divAcciones.style.justifyContent = 'center';

            // Botón Editar
            var btnEditar = document.createElement('button');
            btnEditar.type = 'button';
            btnEditar.className = 'btn-accion-icono btn-accion-icono--editar';
            btnEditar.title = 'Editar datos del empleado';
            btnEditar.innerHTML = '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg>';
            btnEditar.addEventListener('click', function () {
                abrirModalEdicion(emp);
            });
            divAcciones.appendChild(btnEditar);

            // Botón Disponibilidad
            var btnDisp = document.createElement('button');
            btnDisp.type = 'button';
            btnDisp.className = 'btn-accion-icono btn-accion-icono--disponibilidad';
            btnDisp.title = 'Cambiar disponibilidad / registrar baja parcial';
            btnDisp.innerHTML = '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>';
            btnDisp.addEventListener('click', function () {
                abrirModalDisponibilidad(emp);
            });
            divAcciones.appendChild(btnDisp);

            // Botón Desactivar / Activar
            var btnEstado = document.createElement('button');
            btnEstado.type = 'button';
            btnEstado.className = 'btn-accion-icono ' + (emp.activo ? 'btn-accion-icono--desactivar' : 'btn-accion-icono--activar');
            btnEstado.title = emp.activo ? 'Desactivar empleado (baja lógica)' : 'Activar empleado';
            btnEstado.innerHTML = emp.activo ?
                '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="4.93" y1="4.93" x2="19.07" y2="19.07"/></svg>' :
                '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>';

            btnEstado.addEventListener('click', async function () {
                var accion = emp.activo ? 'desactivar' : 'activar';
                var confirmar = window.confirm('¿Está seguro de que desea ' + accion + ' al empleado ' + emp.codigo + ' - ' + emp.nombreCompleto + '?');
                if (!confirmar) return;

                bloquear(true);
                try {
                    var r = await pedir('/' + emp.idEmpleado + '/estado', 'PATCH', { activo: !emp.activo });
                    if (r.ok) {
                        mensaje(r.datos.mensaje || 'Estado actualizado correctamente.', false);
                        await cargarEmpleados();
                        await cargarMetricas();
                    }
                } finally {
                    bloquear(false);
                }
            });
            divAcciones.appendChild(btnEstado);

            tdAcciones.appendChild(divAcciones);
            tr.appendChild(tdAcciones);

            tbody.appendChild(tr);
        });
    }

    // Modal Crear / Editar
    function abrirModalCreacion() {
        editandoId = null;
        el('modal-empleado-titulo').textContent = 'Registrar Nuevo Empleado';
        el('form-empleado').reset();
        el('emp-codigo').disabled = false;
        el('emp-area').value = 'Operaciones';
        el('modal-empleado-error').hidden = true;
        el('modal-empleado').hidden = false;
        document.body.classList.add('modal-abierto');
        el('emp-codigo').focus();
    }

    function abrirModalEdicion(emp) {
        editandoId = emp.idEmpleado;
        el('modal-empleado-titulo').textContent = 'Modificar Empleado: ' + emp.codigo;
        el('emp-codigo').value = emp.codigo;
        el('emp-codigo').disabled = true;
        el('emp-ci').value = emp.documentoIdentidad;
        el('emp-nombres').value = emp.nombres;
        el('emp-apellidos').value = emp.apellidos;
        el('emp-telefono').value = emp.telefono || '';
        el('emp-email').value = emp.email || '';
        el('emp-cargo').value = emp.cargo;
        el('emp-area').value = emp.area;
        el('emp-disponibilidad').value = emp.disponibilidad;
        el('emp-observaciones').value = emp.observaciones || '';
        el('modal-empleado-error').hidden = true;
        el('modal-empleado').hidden = false;
        document.body.classList.add('modal-abierto');
        el('emp-nombres').focus();
    }

    function cerrarModalEmpleado() {
        el('modal-empleado').hidden = true;
        document.body.classList.remove('modal-abierto');
        editandoId = null;
    }

    // Modal Disponibilidad
    function abrirModalDisponibilidad(emp) {
        dispTargetId = emp.idEmpleado;
        el('modal-disp-subtitulo').textContent = 'Empleado: ' + emp.codigo + ' — ' + emp.nombreCompleto;
        el('disp-estado').value = emp.disponibilidad;
        el('disp-motivo').value = '';
        el('modal-disp-error').hidden = true;
        el('modal-disponibilidad').hidden = false;
        document.body.classList.add('modal-abierto');
        el('disp-estado').focus();
    }

    function cerrarModalDisponibilidad() {
        el('modal-disponibilidad').hidden = true;
        document.body.classList.remove('modal-abierto');
        dispTargetId = null;
    }

    function configurarEventos() {
        el('btn-abrir-crear').addEventListener('click', abrirModalCreacion);
        el('btn-cerrar-modal').addEventListener('click', cerrarModalEmpleado);
        el('btn-cancelar-modal').addEventListener('click', cerrarModalEmpleado);
        el('modal-empleado-fondo').addEventListener('click', cerrarModalEmpleado);

        el('btn-cerrar-disp').addEventListener('click', cerrarModalDisponibilidad);
        el('btn-cancelar-disp').addEventListener('click', cerrarModalDisponibilidad);
        el('modal-disp-fondo').addEventListener('click', cerrarModalDisponibilidad);

        // Escape para cerrar modales
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (!el('modal-empleado').hidden) cerrarModalEmpleado();
                if (!el('modal-disponibilidad').hidden) cerrarModalDisponibilidad();
            }
        });

        // Filtros y búsqueda
        var timerBusqueda = null;
        el('empleados-buscar').addEventListener('input', function () {
            clearTimeout(timerBusqueda);
            timerBusqueda = setTimeout(cargarEmpleados, 300);
        });

        el('filtro-cargo').addEventListener('change', cargarEmpleados);
        el('filtro-disponibilidad').addEventListener('change', cargarEmpleados);
        el('filtro-estado').addEventListener('change', cargarEmpleados);

        el('btn-recargar').addEventListener('click', async function () {
            bloquear(true);
            try {
                await Promise.all([cargarEmpleados(), cargarMetricas()]);
                mensaje('Listado y métricas actualizados.', false);
            } finally {
                bloquear(false);
            }
        });

        // Submit Formulario Empleado
        el('form-empleado').addEventListener('submit', async function (e) {
            e.preventDefault();
            if (ocupado) return;

            var errorBox = el('modal-empleado-error');
            errorBox.hidden = true;

            var codigo = el('emp-codigo').value.trim();
            var ci = el('emp-ci').value.trim();
            var nombres = el('emp-nombres').value.trim();
            var apellidos = el('emp-apellidos').value.trim();
            var cargo = el('emp-cargo').value.trim();
            var area = el('emp-area').value.trim();
            var disponibilidad = el('emp-disponibilidad').value.trim();
            var telefono = el('emp-telefono').value.trim();
            var email = el('emp-email').value.trim();
            var observaciones = el('emp-observaciones').value.trim();

            if (!editandoId && !codigo) {
                errorBox.textContent = 'El código de empleado es obligatorio.';
                errorBox.hidden = false;
                el('emp-codigo').focus();
                return;
            }

            if (!ci || !nombres || !apellidos || !cargo || !area || !disponibilidad) {
                errorBox.textContent = 'Debe completar todos los campos obligatorios marcados con (*).';
                errorBox.hidden = false;
                return;
            }

            bloquear(true);
            try {
                var res;
                if (editandoId) {
                    var cuerpoActualizar = {
                        nombres: nombres,
                        apellidos: apellidos,
                        documentoIdentidad: ci,
                        telefono: telefono || null,
                        email: email || null,
                        cargo: cargo,
                        area: area,
                        disponibilidad: disponibilidad,
                        observaciones: observaciones || null
                    };
                    res = await pedir('/' + editandoId, 'PUT', cuerpoActualizar);
                } else {
                    var cuerpoCrear = {
                        codigo: codigo,
                        nombres: nombres,
                        apellidos: apellidos,
                        documentoIdentidad: ci,
                        telefono: telefono || null,
                        email: email || null,
                        cargo: cargo,
                        area: area,
                        disponibilidad: disponibilidad,
                        observaciones: observaciones || null
                    };
                    res = await pedir('', 'POST', cuerpoCrear);
                }

                if (res.ok) {
                    cerrarModalEmpleado();
                    mensaje(editandoId ? 'Datos del empleado actualizados exitosamente.' : 'Empleado registrado exitosamente.', false);
                    await cargarEmpleados();
                    await cargarMetricas();
                } else {
                    errorBox.textContent = (res.datos && res.datos.mensaje) || 'No se pudo guardar la información del empleado.';
                    errorBox.hidden = false;
                }
            } finally {
                bloquear(false);
            }
        });

        // Submit Formulario Disponibilidad
        el('form-disponibilidad').addEventListener('submit', async function (e) {
            e.preventDefault();
            if (ocupado || !dispTargetId) return;

            var errorBox = el('modal-disp-error');
            errorBox.hidden = true;

            var disp = el('disp-estado').value;
            var motivo = el('disp-motivo').value.trim();

            bloquear(true);
            try {
                var res = await pedir('/' + dispTargetId + '/disponibilidad', 'PATCH', {
                    disponibilidad: disp,
                    motivo: motivo || null
                });

                if (res.ok) {
                    cerrarModalDisponibilidad();
                    mensaje('Disponibilidad operativa actualizada exitosamente.', false);
                    await cargarEmpleados();
                    await cargarMetricas();
                } else {
                    errorBox.textContent = (res.datos && res.datos.mensaje) || 'No se pudo actualizar la disponibilidad.';
                    errorBox.hidden = false;
                }
            } finally {
                bloquear(false);
            }
        });
    }

    async function iniciar() {
        el('empleados-contenido').hidden = false;
        configurarEventos();
        bloquear(true);
        try {
            await Promise.all([cargarCatalogos(), cargarEmpleados(), cargarMetricas()]);
        } finally {
            bloquear(false);
        }
    }

    ns.empleados = {
        iniciar: iniciar
    };
})(window.VisorSIG);
