// Ejecutar: node --test tests/VisorDatosSIG.Web.Tests/disponibilidad.test.cjs
// Pruebas del flujo HTTP y de concurrencia del CU19. El DOM falso no valida apariencia visual.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');

const codigo = fs.readFileSync(
    path.resolve(__dirname, '../../src/VisorDatosSIG.Web/wwwroot/js/servicios/disponibilidad.js'),
    'utf8'
);

class Nodo {
    constructor(tag) {
        this.tagName = tag || 'div';
        this.children = [];
        this.parentNode = null;
        this.dataset = {};
        this.attrs = {};
        this.events = {};
        this._clases = new Set();
        this._texto = '';
        this.hidden = false;
        this.disabled = false;
        this.value = '';
        this.type = '';
        this.tabIndex = 0;
        this.title = '';
    }
    get firstChild() { return this.children[0] || null; }
    get textContent() { return this._texto; }
    set textContent(valor) {
        this._texto = valor === null || valor === undefined ? '' : String(valor);
        this.children = [];
    }
    get className() { return Array.from(this._clases).join(' '); }
    set className(valor) {
        this._clases = new Set(String(valor || '').split(/\s+/).filter(Boolean));
    }
    get classList() {
        const self = this;
        return {
            add() { Array.from(arguments).forEach(c => self._clases.add(c)); },
            remove() { Array.from(arguments).forEach(c => self._clases.delete(c)); },
            contains(c) { return self._clases.has(c); },
            toggle(c, forzar) {
                const activo = forzar === undefined ? !self._clases.has(c) : !!forzar;
                if (activo) { self._clases.add(c); } else { self._clases.delete(c); }
                return activo;
            }
        };
    }
    appendChild(nodo) { nodo.parentNode = this; this.children.push(nodo); return nodo; }
    removeChild(nodo) {
        const i = this.children.indexOf(nodo);
        if (i >= 0) { this.children.splice(i, 1); }
        nodo.parentNode = null;
        return nodo;
    }
    replaceChildren() {
        const lista = Array.from(arguments);
        this.children = [];
        const self = this;
        lista.forEach(n => { n.parentNode = self; self.children.push(n); });
    }
    setAttribute(k, v) { this.attrs[k] = String(v); }
    getAttribute(k) { return Object.prototype.hasOwnProperty.call(this.attrs, k) ? this.attrs[k] : null; }
    removeAttribute(k) { delete this.attrs[k]; }
    addEventListener(tipo, fn) { (this.events[tipo] = this.events[tipo] || []).push(fn); }
    emit(tipo, evento) {
        const ev = evento || { preventDefault() {}, stopPropagation() {} };
        (this.events[tipo] || []).forEach(fn => fn.call(this, ev));
        return ev;
    }
    focus() {}
}

function entorno(opciones) {
    opciones = opciones || {};
    const nodos = new Map();
    const get = id => {
        if (!nodos.has(id)) { nodos.set(id, new Nodo('div')); }
        return nodos.get(id);
    };

    if (opciones.permisos) { get('cd-raiz').dataset.permisos = opciones.permisos; }
    get('cd-fecha').value = '2025-05-15';
    get('cd-hora').value = '10:00';
    get('cd-limite').value = '8';
    get('cd-busqueda').value = '';
    get('cd-estado').value = '';
    get('cd-hor-select').value = '1';
    get('cd-asigtab-empleado').value = '';
    get('cd-asigtab-estado').value = '';
    get('cd-asigtab-tipo').value = '';
    get('cd-asigtab-fecha').value = '';
    get('cd-asignacion-empleado').value = '1';
    get('cd-asignacion-tipo').value = 'Inspección';
    get('cd-asignacion-prioridad').value = 'Normal';
    get('cd-horario-dia').value = '1';
    // En el marcado real estos nodos nacen ocultos (atributo hidden).
    get('cd-modal-horario').hidden = true;
    get('cd-modal-asignacion').hidden = true;
    get('cd-toast').hidden = true;
    get('cd-exito').hidden = true;
    get('cd-det-contenido').hidden = true;
    get('cd-horario-vacio').hidden = true;
    get('cd-asig-vacio').hidden = true;

    const requests = [];
    const ns = {
        sesion: { obtenerToken: () => 'jwt-de-prueba' },
        api: {
            peticion(ruta, op) {
                return new Promise(resolve => requests.push({ ruta, opciones: op, resolve }));
            }
        }
    };
    const timeouts = [];
    const contexto = {
        window: { VisorSIG: ns },
        document: { getElementById: get, createElement: tag => new Nodo(tag) },
        setTimeout: fn => { timeouts.push(fn); return timeouts.length; },
        clearTimeout: () => {},
        console
    };
    vm.runInNewContext(codigo, contexto);
    ns.disponibilidadPersonal.iniciar();
    return { get, requests, ns, timeouts };
}

const avanzar = async () => { for (let i = 0; i < 8; i++) { await new Promise(r => setImmediate(r)); } };

function registros(cantidad) {
    const estados = ['Disponible', 'En Servicio', 'Fuera de horario', 'Baja Parcial'];
    const lista = [];
    for (let i = 0; i < cantidad; i++) {
        lista.push({
            idEmpleado: i + 1,
            codigo: 'EMP-' + String(i + 1).padStart(3, '0'),
            nombreCompleto: 'Empleado ' + (i + 1) + ' Apellido',
            cargo: 'Técnico de Campo',
            area: 'Operaciones',
            estadoDisponibilidad: estados[i % 4],
            motivo: 'Disponible para asignación',
            horarioAplicable: '08:00 - 16:00',
            asignacionActual: null
        });
    }
    return lista;
}

const okDisponibilidad = lista => ({ ok: true, status: 200, datos: lista });
const okHorarios = lista => ({ ok: true, status: 200, datos: lista });
const okPaginaAsig = (lista, total, pagina, limite) => ({
    ok: true,
    status: 200,
    datos: {
        datos: lista,
        totalRegistros: total,
        totalPaginas: Math.ceil(total / (limite || 8)),
        pagina: pagina || 1,
        limite: limite || 8
    }
});
const error = status => ({ ok: false, status, datos: null });
const conflicto = mensaje => ({ ok: false, status: 409, datos: { status: 409, title: 'Conflicto', detail: mensaje } });

const consulta = req => new URL(req.ruta, 'https://ejemplo.test');

const horariosSemana = [
    { idHorario: 1, idEmpleado: 1, diaSemana: 1, horaInicio: '08:00:00', horaFin: '12:00:00', activo: true },
    { idHorario: 2, idEmpleado: 1, diaSemana: 1, horaInicio: '14:00:00', horaFin: '16:00:00', activo: true },
    { idHorario: 3, idEmpleado: 1, diaSemana: 6, horaInicio: '09:00:00', horaFin: '13:00:00', activo: true }
];

async function conDetalle(e, lista, horarios, asignaciones) {
    e.requests[0].resolve(okDisponibilidad(lista || registros(3)));
    await avanzar();
    e.requests[1].resolve(okHorarios(horarios || []));
    e.requests[2].resolve(okPaginaAsig(asignaciones || [], (asignaciones || []).length));
    await avanzar();
}

test('selecciona el primer registro y publica su detalle', async () => {
    const e = entorno();
    await conDetalle(e);
    assert.equal(e.get('cd-det-contenido').hidden, false);
    assert.equal(e.get('cd-det-nombre').textContent, 'Empleado 1 Apellido');
    assert.equal(e.get('cd-det-id').textContent, 'EMP-001');
    assert.equal(e.get('cd-det-fecha').textContent, '15/05/2025 10:00');
    assert.equal(e.get('cd-det-badge').hidden, false);
    assert.equal(e.get('cd-det-badge').textContent, 'Disponible');
    assert.ok(e.get('cd-cuerpo').children[0].className.includes('cd-fila--activa'));
});

test('carga el horario del empleado y arma las franjas del día', async () => {
    const e = entorno();
    await conDetalle(e, registros(1), horariosSemana);

    assert.match(e.requests[1].ruta, /^api\/disponibilidad\/empleados\/1\/horarios$/);

    const lunes = e.get('cd-horario-dias').children[0];
    assert.equal(lunes.children[0].textContent, 'Lun');
    const franjas = lunes.children.filter(c => c.className.includes('cd-franja'));
    assert.equal(franjas.length, 2);
    assert.equal(franjas[0].textContent, '08:00 - 12:00');
    assert.equal(e.get('cd-horario-vacio').hidden, true);
});

test('el carrusel avanza de lunes-viernes al fin de semana y regresa', async () => {
    const e = entorno();
    await conDetalle(e, registros(1), horariosSemana);

    assert.deepEqual(
        e.get('cd-horario-dias').children.map(c => c.children[0].textContent),
        ['Lun', 'Mar', 'Mié', 'Jue', 'Vie']
    );
    assert.equal(e.get('cd-horario-prev').disabled, true);
    assert.equal(e.get('cd-horario-next').disabled, false);

    e.get('cd-horario-next').emit('click');
    assert.deepEqual(
        e.get('cd-horario-dias').children.map(c => c.children[0].textContent),
        ['Sáb', 'Dom']
    );
    assert.equal(e.get('cd-horario-prev').disabled, false);
    assert.equal(e.get('cd-horario-next').disabled, true);

    e.get('cd-horario-prev').emit('click');
    assert.deepEqual(
        e.get('cd-horario-dias').children.map(c => c.children[0].textContent),
        ['Lun', 'Mar', 'Mié', 'Jue', 'Vie']
    );
});

test('cambia entre Horario semanal y Asignaciones sin mostrarlos a la vez', async () => {
    const e = entorno();
    await conDetalle(e);
    assert.equal(e.get('cd-sub-horario').hidden, false);
    assert.equal(e.get('cd-sub-asignaciones').hidden, true);

    e.get('cd-subtab-asignaciones').emit('click');
    assert.equal(e.get('cd-sub-horario').hidden, true);
    assert.equal(e.get('cd-sub-asignaciones').hidden, false);
    assert.equal(e.get('cd-subtab-asignaciones').getAttribute('aria-selected'), 'true');

    e.get('cd-subtab-horario').emit('click');
    assert.equal(e.get('cd-sub-horario').hidden, false);
    assert.equal(e.get('cd-sub-asignaciones').hidden, true);
});

test('consulta las asignaciones del empleado en la fecha consultada', async () => {
    const e = entorno();
    await conDetalle(e, registros(1), horariosSemana, [
        { idAsignacion: 5, idEmpleado: 1, idCodigo: 7, codigoFijo: 'SIG-0001', tipoTarea: 'Inspección', fechaInicio: '2025-05-15T09:00:00', fechaFin: '2025-05-15T11:00:00', estado: 'En Proceso', prioridad: 'Alta' }
    ]);

    const params = consulta(e.requests[2]).searchParams;
    assert.equal(params.get('idEmpleado'), '1');
    assert.equal(params.get('fecha'), '2025-05-15');
    assert.equal(params.get('pagina'), '1');

    const fila = e.get('cd-asig-cuerpo').children[0];
    assert.equal(fila.children[0].textContent, '09:00 - 11:00');
    assert.equal(fila.children[1].textContent, 'Inspección');
    assert.equal(fila.children[2].textContent, 'SIG-0001');
    assert.equal(fila.children[3].children[0].className, 'cd-badge cd-badge--proceso');
    assert.equal(e.get('cd-asig-vacio').hidden, true);
});

test('muestra "Sin ubicación asociada" cuando el código fijo es nulo', async () => {
    const e = entorno();
    await conDetalle(e, registros(1), horariosSemana, [
        { idAsignacion: 6, idEmpleado: 1, idCodigo: null, codigoFijo: null, tipoTarea: 'Cobro', fechaInicio: '2025-05-15T14:00:00', fechaFin: '2025-05-15T16:00:00', estado: 'Asignada', prioridad: 'Normal' }
    ]);
    assert.equal(e.get('cd-asig-cuerpo').children[0].children[2].textContent, 'Sin ubicación asociada');
});

test('las pestañas superiores muestran una sección a la vez', async () => {
    const e = entorno();
    await conDetalle(e);
    assert.equal(e.get('cd-panel-resumen').hidden, false);
    assert.equal(e.get('cd-panel-horarios').hidden, true);

    e.get('cd-tab-asignaciones').emit('click');
    assert.equal(e.get('cd-panel-resumen').hidden, true);
    assert.equal(e.get('cd-panel-asignaciones').hidden, false);
    assert.match(e.requests[e.requests.length - 1].ruta, /^api\/asignaciones-trabajo\?/);

    e.get('cd-tab-horarios').emit('click');
    assert.equal(e.get('cd-panel-horarios').hidden, false);
    assert.equal(e.get('cd-panel-asignaciones').hidden, true);
    assert.match(e.requests[e.requests.length - 1].ruta, /^api\/disponibilidad\/empleados\/1\/horarios$/);

    e.requests[e.requests.length - 1].resolve(okHorarios(horariosSemana));
    await avanzar();
    assert.equal(e.get('cd-hor-semana').children.length, 7);
});

test('crea una franja horaria con el contrato del backend', async () => {
    const e = entorno();
    await conDetalle(e, registros(1), horariosSemana);

    e.get('cd-tab-horarios').emit('click');
    await avanzar();
    // La pestaña recarga el horario del empleado seleccionado.
    e.requests[e.requests.length - 1].resolve(okHorarios(horariosSemana));
    await avanzar();

    const lunes = e.get('cd-hor-semana').children[0];
    const agregar = lunes.children[0].children[1];
    assert.equal(agregar.textContent, 'Agregar franja');
    agregar.emit('click');
    assert.equal(e.get('cd-modal-horario').hidden, false);
    assert.equal(e.get('cd-horario-titulo').textContent, 'Nueva franja horaria');

    e.get('cd-horario-dia').value = '2';
    e.get('cd-horario-inicio').value = '09:00';
    e.get('cd-horario-fin').value = '13:00';
    e.get('cd-horario-form').emit('submit');
    await avanzar();

    const peticion = e.requests[e.requests.length - 1];
    assert.equal(peticion.opciones.metodo, 'POST');
    assert.match(peticion.ruta, /^api\/disponibilidad\/empleados\/1\/horarios$/);
    assert.equal(peticion.opciones.cuerpo.diaSemana, 2);
    assert.equal(peticion.opciones.cuerpo.horaInicio, '09:00');
    assert.equal(peticion.opciones.cuerpo.horaFin, '13:00');

    peticion.resolve({ ok: true, status: 201, datos: { idHorario: 9 } });
    await avanzar();
    assert.equal(e.get('cd-modal-horario').hidden, true);
});

test('no crea la franja si la hora de inicio no es anterior a la de fin', async () => {
    const e = entorno();
    await conDetalle(e, registros(1), horariosSemana);

    e.get('cd-tab-horarios').emit('click');
    await avanzar();
    e.requests[e.requests.length - 1].resolve(okHorarios(horariosSemana));
    await avanzar();
    const antes = e.requests.length;

    e.get('cd-hor-semana').children[0].children[0].children[1].emit('click');
    e.get('cd-horario-inicio').value = '13:00';
    e.get('cd-horario-fin').value = '09:00';
    e.get('cd-horario-form').emit('submit');

    assert.equal(e.requests.length, antes);
    assert.match(e.get('cd-horario-error').textContent, /anterior/);
    assert.equal(e.get('cd-horario-error').hidden, false);
});

test('muestra el conflicto 409 al crear un horario solapado', async () => {
    const e = entorno();
    await conDetalle(e, registros(1), horariosSemana);

    e.get('cd-tab-horarios').emit('click');
    await avanzar();
    e.requests[e.requests.length - 1].resolve(okHorarios(horariosSemana));
    await avanzar();

    e.get('cd-hor-semana').children[0].children[0].children[1].emit('click');
    e.get('cd-horario-inicio').value = '10:00';
    e.get('cd-horario-fin').value = '11:00';
    e.get('cd-horario-form').emit('submit');
    await avanzar();

    e.requests[e.requests.length - 1].resolve(conflicto('El horario se solapa con otro existente.'));
    await avanzar();

    assert.equal(e.get('cd-modal-horario').hidden, false);
    assert.match(e.get('cd-horario-error').textContent, /solapa/);
});

test('crea una asignación con el contrato del backend', async () => {
    const e = entorno();
    await conDetalle(e);

    e.get('cd-nueva-asignacion').emit('click');
    assert.equal(e.get('cd-modal-asignacion').hidden, false);
    assert.equal(e.get('cd-asignacion-titulo').textContent, 'Nueva asignación');

    e.get('cd-asignacion-empleado').value = '2';
    e.get('cd-asignacion-codigo').value = '15';
    e.get('cd-asignacion-tipo').value = 'Corte';
    e.get('cd-asignacion-prioridad').value = 'Alta';
    e.get('cd-asignacion-inicio').value = '2025-05-15T09:00';
    e.get('cd-asignacion-fin').value = '2025-05-15T11:00';
    e.get('cd-asignacion-observaciones').value = 'Programada';
    e.get('cd-asignacion-form').emit('submit');
    await avanzar();

    const peticion = e.requests[e.requests.length - 1];
    assert.equal(peticion.opciones.metodo, 'POST');
    assert.equal(peticion.ruta, 'api/asignaciones-trabajo');
    const cuerpo = peticion.opciones.cuerpo;
    assert.equal(cuerpo.idEmpleado, 2);
    assert.equal(cuerpo.idCodigo, 15);
    assert.equal(cuerpo.tipoTarea, 'Corte');
    assert.equal(cuerpo.fechaInicio, '2025-05-15T09:00');
    assert.equal(cuerpo.fechaFin, '2025-05-15T11:00');
    assert.equal(cuerpo.estado, 'Asignada');
    assert.equal(cuerpo.prioridad, 'Alta');
    assert.equal(cuerpo.observaciones, 'Programada');

    peticion.resolve({ ok: true, status: 201, datos: { idAsignacion: 44 } });
    await avanzar();
    assert.equal(e.get('cd-modal-asignacion').hidden, true);
    assert.equal(e.get('cd-toast').children[1].textContent, 'Asignación creada correctamente');
});

test('envía idCodigo nulo cuando el código fijo se deja vacío', async () => {
    const e = entorno();
    await conDetalle(e);

    e.get('cd-nueva-asignacion').emit('click');
    e.get('cd-asignacion-codigo').value = '';
    e.get('cd-asignacion-inicio').value = '2025-05-15T09:00';
    e.get('cd-asignacion-fin').value = '2025-05-15T11:00';
    e.get('cd-asignacion-form').emit('submit');
    await avanzar();

    assert.equal(e.requests[e.requests.length - 1].opciones.cuerpo.idCodigo, null);
});

test('expone el mensaje del backend ante un conflicto 409 de asignación', async () => {
    const e = entorno();
    await conDetalle(e);

    e.get('cd-nueva-asignacion').emit('click');
    e.get('cd-asignacion-inicio').value = '2025-05-15T09:00';
    e.get('cd-asignacion-fin').value = '2025-05-15T11:00';
    e.get('cd-asignacion-form').emit('submit');
    await avanzar();

    e.requests[e.requests.length - 1].resolve(conflicto('El empleado tiene una baja parcial vigente.'));
    await avanzar();

    assert.equal(e.get('cd-modal-asignacion').hidden, false);
    assert.match(e.get('cd-asignacion-error').textContent, /Conflicto/);
    assert.match(e.get('cd-asignacion-error').textContent, /baja parcial/);
});

test('con permisos declarados de solo lectura oculta las acciones de gestión', async () => {
    const e = entorno({ permisos: 'puedeCrear=false;puedeEditar=false' });
    await conDetalle(e);

    assert.equal(e.get('cd-nueva-asignacion').hidden, true);
    assert.equal(e.get('cd-asigtab-nueva').hidden, true);
    assert.equal(e.get('cd-lectura').hidden, false);

    const antes = e.requests.length;
    e.get('cd-nueva-asignacion').emit('click');
    assert.equal(e.requests.length, antes);
    assert.equal(e.get('cd-modal-asignacion').hidden, true);
});

test('un 403 en una acción de gestión deja el módulo en solo lectura', async () => {
    const e = entorno();
    await conDetalle(e);

    e.get('cd-nueva-asignacion').emit('click');
    e.get('cd-asignacion-inicio').value = '2025-05-15T09:00';
    e.get('cd-asignacion-fin').value = '2025-05-15T11:00';
    e.get('cd-asignacion-form').emit('submit');
    await avanzar();

    e.requests[e.requests.length - 1].resolve(error(403));
    await avanzar();

    assert.equal(e.get('cd-nueva-asignacion').hidden, true);
    assert.match(e.get('cd-asigtab-aviso').textContent, /permisos/);
});

test('descarta la respuesta obsoleta cuando se encadenan consultas', async () => {
    const e = entorno();
    e.get('cd-filtros').emit('submit');

    // La segunda consulta (más reciente) responde primero.
    e.requests[1].resolve(okDisponibilidad([Object.assign({}, registros(1)[0], { nombreCompleto: 'Empleado Nueva' })]));
    await avanzar();
    e.requests[0].resolve(okDisponibilidad([Object.assign({}, registros(1)[0], { nombreCompleto: 'Empleado Antigua' })]));
    await avanzar();

    const fila = e.get('cd-cuerpo').children[0];
    assert.equal(fila.children[0].children[0].children[1].children[0].textContent, 'Empleado Nueva');
});

test('muestra el estado de carga mientras consulta la disponibilidad', async () => {
    const e = entorno();
    assert.equal(e.get('cd-cargando').hidden, false);
    assert.equal(e.get('cd-buscar').disabled, true);
    assert.equal(e.get('cd-cuerpo').children.length, 8);

    e.requests[0].resolve(okDisponibilidad(registros(2)));
    await avanzar();
    assert.equal(e.get('cd-cargando').hidden, true);
    assert.equal(e.get('cd-buscar').disabled, false);
});

test('muestra el estado vacío cuando no hay empleados para los filtros', async () => {
    const e = entorno();
    e.requests[0].resolve(okDisponibilidad([]));
    await avanzar();

    assert.equal(e.get('cd-vacio').hidden, false);
    assert.equal(e.get('cd-tabla-envoltura').hidden, true);
    assert.equal(e.get('cd-conteo').textContent, '');
    assert.equal(e.get('cd-paginacion').hidden, true);
});

test('informa el error genérico y delega el 401 al flujo central', async () => {
    const e = entorno();
    e.requests[0].resolve(error(500));
    await avanzar();
    assert.equal(e.get('cd-aviso').hidden, false);
    assert.notEqual(e.get('cd-aviso').textContent, '');

    const e2 = entorno();
    e2.requests[0].resolve(error(401));
    await avanzar();
    assert.equal(e2.get('cd-aviso').hidden, true);
});

test('un 403 en la consulta bloquea la pantalla sin mostrar datos', async () => {
    const e = entorno();
    e.requests[0].resolve(error(403));
    await avanzar();

    assert.equal(e.get('cd-aviso').hidden, false);
    assert.match(e.get('cd-aviso').textContent, /No tiene permisos/);
    assert.equal(e.get('cd-nueva-asignacion').hidden, true);
    assert.equal(e.get('cd-cuerpo').children.length, 0);
});





test('construye la consulta de disponibilidad con fecha y hora y usa la sesión protegida', () => {
    const e = entorno();
    const r = e.requests[0];
    assert.match(r.ruta, /^api\/disponibilidad\?/);
    assert.equal(r.opciones.metodo, 'GET');
    assert.equal(r.opciones.token, 'jwt-de-prueba');
    assert.equal(r.opciones.protegida, true);
    const params = consulta(r).searchParams;
    assert.equal(params.get('fecha'), '2025-05-15');
    assert.equal(params.get('hora'), '10:00');
    assert.equal(params.get('busqueda'), null);
    assert.equal(params.get('estado'), null);
});

test('omite filtros opcionales vacíos y los envía codificados cuando se informan', async () => {
    const e = entorno();
    e.requests[0].resolve(okDisponibilidad(registros(2)));
    await avanzar();

    e.get('cd-busqueda').value = ' <img onerror=alert(1)> ';
    e.get('cd-estado').value = 'Disponible';
    e.get('cd-filtros').emit('submit');

    const ultima = e.requests[e.requests.length - 1];
    const params = consulta(ultima).searchParams;
    assert.equal(params.get('busqueda'), '<img onerror=alert(1)>');
    assert.equal(params.get('estado'), 'Disponible');
    assert.equal(params.get('fecha'), '2025-05-15');
});

test('al cambiar de empleado recarga los horarios del empleado indicado', async () => {
    const e = entorno();
    e.requests[0].resolve(okDisponibilidad(registros(3)));
    await avanzar();
    e.requests[1].resolve(okHorarios([]));
    e.requests[2].resolve(okPaginaAsig([], 0));
    await avanzar();

    // Primera selección automática: empleado 1.
    assert.match(e.requests[1].ruta, /^api\/disponibilidad\/empleados\/1\/horarios$/);

    const fila = e.get('cd-cuerpo').children[1];
    fila.children[5].children[0].emit('click');
    await avanzar();

    const horariosEmpleado = e.requests.filter(r => /\/empleados\/2\/horarios$/.test(r.ruta));
    assert.equal(horariosEmpleado.length, 1);
    assert.equal(consulta(horariosEmpleado[0]).pathname, '/api/disponibilidad/empleados/2/horarios');
});

test('renderiza los estados reales del backend con su badge', async () => {
    const e = entorno();
    e.requests[0].resolve(okDisponibilidad(registros(4)));
    await avanzar();

    const filas = e.get('cd-cuerpo').children;
    const esperado = [
        ['cd-badge--disponible', 'Disponible'],
        ['cd-badge--servicio', 'En Servicio'],
        ['cd-badge--fuera', 'Fuera de horario'],
        ['cd-badge--baja', 'Baja Parcial']
    ];
    for (let i = 0; i < esperado.length; i++) {
        const badge = filas[i].children[2].children[0];
        assert.ok(badge.className.includes(esperado[i][0]), 'clase ' + esperado[i][0]);
        assert.equal(badge.textContent, esperado[i][1]);
    }
});

test('pagina de 8 en 8 sobre el resultado recibido y actualiza el conteo', async () => {
    const e = entorno();
    e.requests[0].resolve(okDisponibilidad(registros(12)));
    await avanzar();

    assert.equal(e.get('cd-cuerpo').children.length, 8);
    assert.equal(e.get('cd-conteo').textContent, 'Mostrando 1 a 8 de 12 registros');
    assert.equal(e.get('cd-paginacion').hidden, false);

    const segunda = e.get('cd-paginacion').children.find(b => b.textContent === '2');
    segunda.emit('click');

    assert.equal(e.get('cd-cuerpo').children.length, 4);
    assert.equal(e.get('cd-conteo').textContent, 'Mostrando 9 a 12 de 12 registros');
});
