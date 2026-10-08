// Ejecutar: node --test tests/VisorDatosSIG.Web.Tests/consultas-entidades.test.cjs
// Pruebas del flujo HTTP y de concurrencia. El DOM falso no valida apariencia visual.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const codigo = fs.readFileSync(path.resolve(__dirname, '../../src/VisorDatosSIG.Web/wwwroot/js/consultas/entidades.js'), 'utf8');

class Nodo {
    constructor() {
        this.children = []; this.dataset = {}; this.events = {}; this.attrs = {};
        this.textContent = ''; this.disabled = false; this.hidden = true;
        this.classList = { toggle() {} };
    }
    appendChild(n) { this.children.push(n); }
    replaceChildren() { this.children = []; this.textContent = ''; }
    setAttribute(k, v) { this.attrs[k] = v; }
    addEventListener(k, f) { this.events[k] = f; }
    emitir(k) { return this.events[k]({ preventDefault() {} }); }
}

function entorno(capa = 'lotes') {
    const nodos = new Map();
    const nodo = id => {
        if (!nodos.has(id)) nodos.set(id, new Nodo());
        return nodos.get(id);
    };
    nodo('consulta-entidades').dataset.capa = capa;
    const form = nodo('consulta-filtros');
    form.fields = capa === 'lotes'
        ? { nroLote: '', idManzana: '', limite: '20' }
        : { nombre: '', tipoVia: '', osmid: '', limite: '20' };
    const requests = [], dibujos = [];
    const ns = {
        sesion: { obtenerToken: () => 'jwt-de-prueba' },
        api: { peticion(ruta, opciones) {
            return new Promise(resolve => requests.push({ ruta, opciones, resolve }));
        }}
    };
    const layer = () => ({
        addTo() { return this; },
        getBounds() { return { isValid: () => true, pad() { return this; } }; }
    });
    const L = {
        map: () => ({ removeLayer() {}, invalidateSize() {}, fitBounds() {} }),
        tileLayer: () => ({ on() { return this; }, addTo() {} }),
        geoJSON: geo => { dibujos.push(geo); return layer(); }
    };
    const context = {
        window: { VisorSIG: ns, L }, L,
        document: { getElementById: nodo, createElement: () => new Nodo() },
        URLSearchParams, FormData: class { constructor(f) { return Object.entries(f.fields); } },
        setTimeout
    };
    vm.runInNewContext(codigo, context);
    ns.consultaEntidades.iniciar();
    return { nodo, form, requests, dibujos, ns };
}
const avanzar = () => new Promise(resolve => setImmediate(resolve));
function pagina(registros, total = registros.length, numero = 1, limite = 20) {
    return { ok: true, datos: { datos: registros, totalRegistros: total, totalPaginas: Math.ceil(total / limite), pagina: numero, limite } };
}
function botonDetalle(e, i = 0) {
    const fila = e.nodo('consulta-filas').children[i];
    return fila.children[fila.children.length - 1].children[0];
}

test('omite filtros numericos vacios y usa la sesion protegida', () => {
    const e = entorno();
    const r = e.requests[0];
    assert.equal(r.ruta, 'api/lotes?limite=20&pagina=1');
    assert.equal(r.opciones.token, 'jwt-de-prueba');
    assert.equal(r.opciones.protegida, true);
});
test('combina y codifica filtros de vias sin interpretarlos como HTML', async () => {
    const e = entorno('vias');
    e.form.fields.nombre = ' <img onerror=alert(1)> ';
    e.form.fields.tipoVia = ' Principal ';
    e.form.fields.osmid = '123';
    e.form.emitir('submit');
    const url = new URL(e.requests[1].ruta, 'https://ejemplo.test/');
    assert.equal(url.searchParams.get('nombre'), '<img onerror=alert(1)>');
    assert.equal(url.searchParams.get('tipoVia'), 'Principal');
    assert.equal(url.searchParams.get('osmid'), '123');
    e.requests[1].resolve(pagina([{ idVia: 7, nombre: '<img onerror=alert(1)>', tipoVia: 'Principal', osmid: '123' }]));
    await avanzar();
    assert.equal(e.nodo('consulta-filas').children[0].children[1].textContent, '<img onerror=alert(1)>');
});
test('descarta una respuesta de busqueda antigua', async () => {
    const e = entorno();
    e.form.fields.nroLote = 'nuevo'; e.form.emitir('submit');
    e.requests[1].resolve(pagina([{ idLote: 2, nroLote: 'nuevo' }]));
    await avanzar();
    e.requests[0].resolve(pagina([{ idLote: 1, nroLote: 'antiguo' }]));
    await avanzar();
    assert.equal(e.nodo('consulta-filas').children[0].children[0].textContent, '2');
});
test('paginar conserva los filtros de la busqueda aunque cambie el formulario', async () => {
    const e = entorno();
    e.form.fields.nroLote = '12'; e.form.emitir('submit');
    e.requests[1].resolve(pagina([{ idLote: 1 }], 30));
    await avanzar();
    e.form.fields.nroLote = 'sin aplicar';
    e.nodo('consulta-siguiente').emitir('click');
    const query = new URL(e.requests[2].ruta, 'https://ejemplo.test').searchParams;
    assert.equal(query.get('pagina'), '2'); assert.equal(query.get('nroLote'), '12');
});
test('descarta un detalle anterior al seleccionar otro registro', async () => {
    const e = entorno();
    e.requests[0].resolve(pagina([{ idLote: 1 }, { idLote: 2 }]));
    await avanzar();
    botonDetalle(e, 0).emitir('click'); botonDetalle(e, 1).emitir('click');
    e.requests[2].resolve({ ok:true, datos:{ idLote:2,nroLote:'actual',geometria:null } });
    await avanzar();
    e.requests[1].resolve({ ok:true, datos:{ idLote:1,nroLote:'antiguo',geometria:null } });
    await avanzar();
    assert.equal(e.nodo('consulta-atributos').children[1].textContent, '2');
    assert.equal(e.nodo('consulta-mapa').hidden, true);
    assert.match(e.nodo('consulta-mapa-aviso').textContent, /no tiene geometr/);
});
test('nueva busqueda invalida el detalle pendiente', async () => {
    const e = entorno();
    e.requests[0].resolve(pagina([{ idLote: 1 }]));
    await avanzar(); botonDetalle(e).emitir('click'); e.form.emitir('submit');
    e.requests[1].resolve({ ok:true, datos:{ idLote:1,geometria:{ type:'Polygon' } } });
    await avanzar();
    assert.equal(e.nodo('consulta-atributos').children.length, 0);
    assert.equal(e.dibujos.length, 0);
});
for (const [capa, id, tipo, coordenadas] of [
    ['lotes','idLote','Polygon',[[[-63,-17],[-62.9,-17],[-62.9,-16.9],[-63,-17]]]],
    ['vias','idVia','LineString',[[-63,-17],[-62.9,-16.9]]]
]) {
    test('envia geometria real de ' + capa + ' a Leaflet sin invertir coordenadas', async () => {
        const e = entorno(capa);
        e.requests[0].resolve(pagina([{ [id]:7 }]));
        await avanzar(); botonDetalle(e).emitir('click');
        const geo = { type:tipo,coordinates:coordenadas };
        e.requests[1].resolve({ ok:true, datos:{ [id]:7,geometria:geo } });
        await avanzar();
        assert.equal(e.dibujos[0], geo); assert.equal(e.nodo('consulta-mapa').hidden, false);
    });
}
test('sin resultados desactiva la paginacion y muestra aviso', async () => {
    const e = entorno();
    e.requests[0].resolve(pagina([])); await avanzar();
    assert.match(e.nodo('consulta-estado').textContent, /No se encontraron/);
    assert.equal(e.nodo('consulta-anterior').disabled, true);
    assert.equal(e.nodo('consulta-siguiente').disabled, true);
});
for (const status of [401,403,500]) {
    test('error HTTP ' + status + ' permite repetir la busqueda', async () => {
        const e = entorno();
        e.requests[0].resolve({ ok:false,status }); await avanzar();
        assert.notEqual(e.nodo('consulta-estado').textContent, '');
        e.form.emitir('submit');
        e.requests[1].resolve(pagina([{ idLote:7 }])); await avanzar();
        assert.equal(e.nodo('consulta-filas').children.length, 1);
    });
}
