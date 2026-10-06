// Ejecutar: node --test tests/VisorDatosSIG.Web.Tests/*.test.cjs
// Pruebas de integración liviana entre los módulos CU13/CU14 y la API de Leaflet.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');

const archivos = {
    codigoFijo: path.resolve(__dirname, '../../src/VisorDatosSIG.Web/wwwroot/js/consultas/codigo-fijo.js'),
    manzana: path.resolve(__dirname, '../../src/VisorDatosSIG.Web/wwwroot/js/consultas/manzana.js')
};

class Nodo {
    constructor(tipo = 'div') {
        this.tipo = tipo;
        this.children = [];
        this.events = {};
        this.attrs = {};
        this.dataset = {};
        this.className = '';
        this.textContent = '';
        this.value = '';
        this.hidden = false;
        this.disabled = false;
        this.options = [];
        this.parentNode = null;
        this.style = {};
        this.classList = {
            add: clase => this._cambiarClase(clase, true),
            remove: clase => this._cambiarClase(clase, false),
            toggle: (clase, activo) => this._cambiarClase(clase, activo)
        };
    }

    get firstChild() { return this.children[0] || null; }
    appendChild(nodo) { nodo.parentNode = this; this.children.push(nodo); return nodo; }
    removeChild(nodo) {
        const indice = this.children.indexOf(nodo);
        if (indice >= 0) this.children.splice(indice, 1);
        nodo.parentNode = null;
        return nodo;
    }
    setAttribute(nombre, valor) { this.attrs[nombre] = String(valor); }
    getAttribute(nombre) { return Object.hasOwn(this.attrs, nombre) ? this.attrs[nombre] : null; }
    removeAttribute(nombre) { delete this.attrs[nombre]; }
    addEventListener(nombre, manejador) { this.events[nombre] = manejador; }
    emitir(nombre, extra = {}) {
        return this.events[nombre] && this.events[nombre]({
            preventDefault() {},
            stopPropagation() {},
            target: this,
            ...extra
        });
    }
    querySelector(selector) {
        if (selector === 'span:last-child') {
            return [...this.children].reverse().find(n => n.tipo === 'span') || null;
        }
        return null;
    }
    querySelectorAll(selector) {
        if (!selector.startsWith('.')) return [];
        const clase = selector.slice(1);
        return this.children.filter(n => n.className.split(/\s+/).includes(clase));
    }
    _cambiarClase(clase, activo) {
        const clases = new Set(this.className.split(/\s+/).filter(Boolean));
        if (activo) clases.add(clase); else clases.delete(clase);
        this.className = [...clases].join(' ');
    }
}

const avanzar = () => new Promise(resolve => setImmediate(resolve));

function pagina(datos, limite = 8) {
    return {
        ok: true,
        datos: { datos, pagina: 1, limite, totalRegistros: datos.length, totalPaginas: 1 }
    };
}

function crearEntorno(tipo) {
    const prefijo = tipo === 'codigoFijo' ? 'cf' : 'mz';
    const cuerpoId = prefijo + '-cuerpo';
    const mapaId = prefijo + '-mapa';
    const avisoMapaId = prefijo + '-mapa-aviso';
    const nodos = new Map();
    const nodo = id => {
        if (!nodos.has(id)) nodos.set(id, new Nodo());
        return nodos.get(id);
    };
    nodo(mapaId).hidden = true;
    nodo(prefijo + '-limite').value = '8';
    nodo(prefijo + '-limite').options = [{ value: '8' }, { value: '20' }, { value: '50' }];

    const requests = [];
    const geoJsonRecibidos = [];
    const capasRetiradas = [];
    const vistas = [];
    const ajustes = [];
    let invalidaciones = 0;
    let circulo = null;
    const bounds = {
        isValid: () => true,
        getCenter: () => ({ lat: -17.78, lng: -63.18 }),
        pad(proporcion) { this.proporcion = proporcion; return this; }
    };
    const mapa = {
        removeLayer(capa) { capasRetiradas.push(capa); },
        invalidateSize() { invalidaciones += 1; },
        setView(centro, zoom) { vistas.push({ centro, zoom }); },
        fitBounds(limites, opciones) { ajustes.push({ limites, opciones }); }
    };
    const L = {
        map: () => mapa,
        tileLayer: () => ({ on() { return this; }, addTo() { return this; } }),
        circleMarker(latlng, opciones) { circulo = { latlng, opciones }; return circulo; },
        geoJSON(geo, opciones = {}) {
            geoJsonRecibidos.push(geo);
            if (geo && geo.type === 'Point' && opciones.pointToLayer) {
                opciones.pointToLayer({}, { lat: geo.coordinates[1], lng: geo.coordinates[0] });
            }
            return {
                geo,
                addTo() { return this; },
                getBounds() { return bounds; }
            };
        }
    };
    const ns = {
        sesion: { obtenerToken: () => 'jwt-prueba' },
        api: {
            peticion(ruta, opciones) {
                return new Promise(resolve => requests.push({ ruta, opciones, resolve }));
            }
        }
    };
    const documento = {
        getElementById: nodo,
        createElement: tipoNodo => new Nodo(tipoNodo),
        createTextNode: texto => Object.assign(new Nodo('#text'), { textContent: texto }),
        querySelector: () => new Nodo()
    };
    const contexto = {
        window: { VisorSIG: ns, L },
        document: documento,
        L,
        navigator: {},
        setTimeout: () => 1,
        clearTimeout() {}
    };
    vm.runInNewContext(fs.readFileSync(archivos[tipo], 'utf8'), contexto);
    ns[tipo === 'codigoFijo' ? 'consultaCodigoFijo' : 'consultaManzana'].iniciar();

    return {
        nodo,
        cuerpo: nodo(cuerpoId),
        mapaNodo: nodo(mapaId),
        avisoMapa: nodo(avisoMapaId),
        requests,
        geoJsonRecibidos,
        capasRetiradas,
        vistas,
        ajustes,
        invalidaciones: () => invalidaciones,
        circulo: () => circulo
    };
}

test('CU13 entrega el Point intacto a Leaflet y centra el mini mapa', async () => {
    const e = crearEntorno('codigoFijo');
    e.requests[0].resolve(pagina([{ idCodigo: 1, codFijo: 10, estado: 1 }]));
    await avanzar();
    e.cuerpo.children[0].emitir('click');
    const point = { type: 'Point', coordinates: [-63.1801, -17.7802] };
    e.requests[1].resolve({ ok: true, datos: { idCodigo: 1, estado: 1, geometria: point } });
    await avanzar();

    assert.equal(e.geoJsonRecibidos[0], point);
    assert.deepEqual(e.circulo().latlng, { lat: -17.7802, lng: -63.1801 });
    assert.equal(e.vistas[0].zoom, 18);
    assert.equal(e.mapaNodo.hidden, false);
    assert.equal(e.invalidaciones(), 1);
});

test('CU13 reemplaza la geometría anterior al seleccionar otro registro', async () => {
    const e = crearEntorno('codigoFijo');
    e.requests[0].resolve(pagina([{ idCodigo: 1, estado: 1 }, { idCodigo: 2, estado: 1 }]));
    await avanzar();
    e.cuerpo.children[0].emitir('click');
    e.requests[1].resolve({ ok: true, datos: { idCodigo: 1, estado: 1, geometria: { type: 'Point', coordinates: [-63, -17] } } });
    await avanzar();
    e.cuerpo.children[1].emitir('click');
    e.requests[2].resolve({ ok: true, datos: { idCodigo: 2, estado: 1, geometria: { type: 'Point', coordinates: [-62, -16] } } });
    await avanzar();

    assert.equal(e.geoJsonRecibidos.length, 2);
    assert.equal(e.capasRetiradas.length, 1);
});

test('CU13 maneja geometría null sin crear una capa', async () => {
    const e = crearEntorno('codigoFijo');
    e.requests[0].resolve(pagina([{ idCodigo: 1, estado: 1 }]));
    await avanzar();
    e.cuerpo.children[0].emitir('click');
    e.requests[1].resolve({ ok: true, datos: { idCodigo: 1, estado: 1, geometria: null } });
    await avanzar();

    assert.equal(e.geoJsonRecibidos.length, 0);
    assert.equal(e.mapaNodo.hidden, true);
    assert.match(e.avisoMapa.textContent, /Sin geometría disponible/);
});

for (const tipoGeometria of ['Polygon', 'MultiPolygon']) {
    test('CU14 entrega ' + tipoGeometria + ' intacto a Leaflet y usa fitBounds', async () => {
        const e = crearEntorno('manzana');
        e.requests[0].resolve(pagina([{ idManzana: 7, uv: '30', mza: '1' }]));
        await avanzar();
        const coordinates = tipoGeometria === 'Polygon'
            ? [[[-63.2, -17.8], [-63.1, -17.8], [-63.1, -17.7], [-63.2, -17.8]]]
            : [[[[-63.2, -17.8], [-63.1, -17.8], [-63.1, -17.7], [-63.2, -17.8]]]];
        const geometria = { type: tipoGeometria, coordinates };
        e.requests[1].resolve({ ok: true, datos: { idManzana: 7, geometria } });
        await avanzar();

        assert.equal(e.geoJsonRecibidos[0], geometria);
        assert.equal(e.ajustes.length, 1);
        assert.equal(e.ajustes[0].opciones.maxZoom, 18);
        assert.equal(e.ajustes[0].limites.proporcion, 0.15);
        assert.equal(e.mapaNodo.hidden, false);
    });
}

test('CU14 reemplaza la geometría anterior al seleccionar otra manzana', async () => {
    const e = crearEntorno('manzana');
    e.requests[0].resolve(pagina([{ idManzana: 1 }, { idManzana: 2 }]));
    await avanzar();
    e.requests[1].resolve({ ok: true, datos: { idManzana: 1, geometria: { type: 'Polygon', coordinates: [] } } });
    await avanzar();
    e.cuerpo.children[1].emitir('click');
    e.requests[2].resolve({ ok: true, datos: { idManzana: 2, geometria: { type: 'Polygon', coordinates: [] } } });
    await avanzar();

    assert.equal(e.geoJsonRecibidos.length, 2);
    assert.equal(e.capasRetiradas.length, 1);
});

test('CU14 maneja geometría null sin romper el detalle', async () => {
    const e = crearEntorno('manzana');
    e.requests[0].resolve(pagina([{ idManzana: 1 }]));
    await avanzar();
    e.requests[1].resolve({ ok: true, datos: { idManzana: 1, geometria: null } });
    await avanzar();

    assert.equal(e.geoJsonRecibidos.length, 0);
    assert.equal(e.mapaNodo.hidden, true);
    assert.match(e.avisoMapa.textContent, /Sin geometría disponible/);
});
