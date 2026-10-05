
window.VisorSIG = window.VisorSIG || {};
(function (ns) {
    'use strict';
    var configuraciones = {
        lotes: { id: 'idLote', columnas: [['idLote', 'ID'], ['nroLote', 'Número de lote'], ['idManzana', 'Manzana']], extra: [['idOrigen', 'ID de origen']], color: '#2563eb' },
        vias: { id: 'idVia', columnas: [['idVia', 'ID'], ['nombre', 'Nombre'], ['tipoVia', 'Tipo de vía'], ['osmid', 'OSMID']], extra: [['objectid', 'OBJECTID']], color: '#d97706' }
    };
    function iniciar() {
        var raiz = document.getElementById('consulta-entidades');
        if (!raiz || raiz.dataset.iniciada) return;
        raiz.dataset.iniciada = 'true';
        var capa = raiz.dataset.capa, config = configuraciones[capa];
        var form = document.getElementById('consulta-filtros');
        var filas = document.getElementById('consulta-filas');
        var estado = document.getElementById('consulta-estado');
        var detalleEstado = document.getElementById('consulta-detalle-estado');
        var atributos = document.getElementById('consulta-atributos');
        var anterior = document.getElementById('consulta-anterior'), siguiente = document.getElementById('consulta-siguiente');
        var mapaNodo = document.getElementById('consulta-mapa'), mapaAviso = document.getElementById('consulta-mapa-aviso');
        var pagina = 1, totalPaginas = 0, secuencia = 0, secuenciaDetalle = 0;
        var mapa = null, geometria = null, filtros = new URLSearchParams(new FormData(form));
        function elemento(tipo, texto) {
            var n = document.createElement(tipo);
            n.textContent = texto == null || texto === '' ? '—' : String(texto);
            return n;
        }
        var cabecera = document.createElement('tr');
        config.columnas.concat([['accion', 'Detalle']]).forEach(function (c) {
            var th = elemento('th', c[1]); th.scope = 'col'; cabecera.appendChild(th);
        });
        document.getElementById('consulta-cabecera').appendChild(cabecera);
        function mensaje(n, texto, error) {
            n.textContent = texto; n.classList.toggle('consulta-error', !!error);
        }
        function errorRespuesta(r) {
            if (r.status === 403) return 'No tiene permiso para consultar estos registros.';
            if (r.status === 401) return 'La sesión ha expirado. Inicie sesión nuevamente.';
            if (r.status === 404) return 'El registro ya no está disponible.';
            return r.datos && r.datos.detail ? r.datos.detail : 'No se pudo completar la consulta. Intente nuevamente.';
        }
        function pedir(ruta) {
            var token = ns.sesion.obtenerToken();
            if (!token) return Promise.resolve({ok:false,status:401});
            return ns.api.peticion(ruta, { metodo:'GET', token:token, protegida:true });
        }
        function limpiarDetalle() {
            secuenciaDetalle++;
            Array.from(filas.children).forEach(function (fila) { fila.setAttribute('aria-selected', 'false'); });
            atributos.replaceChildren(); mapaNodo.hidden = true; mapaAviso.textContent = '';
            if (geometria && mapa) { mapa.removeLayer(geometria); geometria = null; }
            mensaje(detalleEstado, 'Seleccione un registro.', false);
        }
        function dibujar(geo) {
            if (!geo) { mensaje(mapaAviso, 'Este registro no tiene geometría disponible.', false); return; }
            mapaNodo.hidden = false;
            try {
                if (!window.L) throw new Error('Leaflet no disponible');
                if (!mapa) {
                    mapa = L.map(mapaNodo);
                    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                        maxZoom:19, attribution:'&copy; OpenStreetMap'
                    }).on('tileerror', function () {
                        mensaje(mapaAviso, 'El mapa base no está disponible. La geometría del registro sigue visible.', true);
                    }).addTo(mapa);
                }
                geometria = L.geoJSON(geo, {style:{color:config.color,weight:4,fillOpacity:0.2}}).addTo(mapa);
                mapa.invalidateSize();
                var bounds = geometria.getBounds();
                if (!bounds.isValid()) throw new Error('Geometría vacía');
                mapa.fitBounds(bounds.pad(0.15), {maxZoom:18});
            } catch (e) {
                if (geometria && mapa) { mapa.removeLayer(geometria); geometria = null; }
                mapaNodo.hidden = true;
                mensaje(mapaAviso, 'No se pudo representar la geometría de este registro.', true);
            }
        }
        async function cargarDetalle(id) {
            limpiarDetalle();
            Array.from(filas.children).forEach(function (fila) {
                fila.setAttribute('aria-selected', String(fila.dataset.id === String(id)));
            });
            var pedido = ++secuenciaDetalle;
            mensaje(detalleEstado, 'Cargando detalle…', false);
            var r = await pedir('api/' + capa + '/' + encodeURIComponent(id));
            if (pedido !== secuenciaDetalle) return;
            if (!r.ok) { mensaje(detalleEstado, errorRespuesta(r), true); return; }
            config.columnas.concat(config.extra).forEach(function (c) {
                atributos.appendChild(elemento('dt', c[1]));
                atributos.appendChild(elemento('dd', r.datos[c[0]]));
            });
            mensaje(detalleEstado, 'Registro seleccionado: ' + id, false);
            dibujar(r.datos.geometria);
        }
        async function buscar(numero) {
            var pedido = ++secuencia;
            pagina = numero;
            limpiarDetalle(); filas.replaceChildren();
            anterior.disabled = siguiente.disabled = true;
            document.getElementById('consulta-conteo').textContent = 'Resultados';
            document.getElementById('consulta-pagina').textContent = '';
            mensaje(estado, 'Buscando…', false);
            var parametros = new URLSearchParams(filtros);
            Array.from(parametros.keys()).forEach(function (key) {
                var value = parametros.get(key).trim();
                if (!value) parametros.delete(key); else parametros.set(key, value);
            });
            parametros.set('pagina', numero);
            var r = await pedir('api/' + capa + '?' + parametros.toString());
            if (pedido !== secuencia) return;
            if (!r.ok) { mensaje(estado, errorRespuesta(r), true); return; }
            totalPaginas = r.datos.totalPaginas;
            r.datos.datos.forEach(function (registro) {
                var fila = document.createElement('tr');
                fila.dataset.id = registro[config.id];
                config.columnas.forEach(function (c) { fila.appendChild(elemento('td', registro[c[0]])); });
                var celda = document.createElement('td'), boton = elemento('button', 'Ver detalle');
                boton.type = 'button';
                boton.setAttribute('aria-label', 'Ver detalle del registro ' + registro[config.id]);
                boton.addEventListener('click', function () { cargarDetalle(registro[config.id]); });
                celda.appendChild(boton); fila.appendChild(celda); filas.appendChild(fila);
            });
            document.getElementById('consulta-conteo').textContent = 'Resultados: ' + r.datos.totalRegistros + ' registros';
            document.getElementById('consulta-pagina').textContent = totalPaginas ? 'Página ' + pagina + ' de ' + totalPaginas : 'Sin resultados';
            anterior.disabled = pagina <= 1;
            siguiente.disabled = pagina >= totalPaginas;
            mensaje(estado, r.datos.totalRegistros ? '' : 'No se encontraron registros con estos filtros.', false);
        }
        form.addEventListener('submit', function (e) {
            e.preventDefault(); filtros = new URLSearchParams(new FormData(form)); buscar(1);
        });
        form.addEventListener('reset', function () {
            setTimeout(function () { filtros = new URLSearchParams(new FormData(form)); buscar(1); }, 0);
        });
        anterior.addEventListener('click', function () { if (pagina > 1) buscar(pagina - 1); });
        siguiente.addEventListener('click', function () { if (pagina < totalPaginas) buscar(pagina + 1); });
        buscar(1);
    }
    ns.consultaEntidades = { iniciar: iniciar };
})(window.VisorSIG);
