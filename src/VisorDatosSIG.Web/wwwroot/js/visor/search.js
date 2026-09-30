// FE-SIG 5: búsqueda alfanumérica real sobre las capas catastrales.
// Consume el endpoint protegido GET api/catastro/buscar (SQL Server) con JWT.
// No hay mocks, ni filtrado local que sustituya al backend, ni datos JSON
// locales, ni SQL desde JavaScript.
//
// La búsqueda NO depende de que una capa esté activa: los resultados llegan del
// servidor. La única mejora en el mapa («Ver en mapa») se ofrece solo cuando la
// entidad ya está cargada en la capa Leaflet actual; el endpoint de búsqueda no
// entrega geometría ni extensión por resultado, por lo que no se inventan
// coordenadas.
//
// Autenticación: se usa exclusivamente window.VisorSIG.api.peticion(...) con
// token de sesión y `protegida: true`. Un 401 se deja al flujo de FE-AUTH
// (api.js limpia la sesión y guard.js redirige); este módulo no crea
// redirecciones propias.
//
// Seguridad del DOM: todo se construye con document.createElement y
// textContent; nunca se usa innerHTML con datos del servidor.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var LIMITE = 20;
    var PAGINA_INICIAL = 1;
    var RUTA_BUSQUEDA = 'api/catastro/buscar';

    var ID_FORM = 'form-busqueda';
    var ID_TEXTO = 'texto-busqueda';
    var ID_FILTRO = 'filtro-capa';
    var ID_BOTON_LIMPIAR = 'btn-limpiar-busqueda';
    var ID_ESTADO = 'estado-busqueda';
    var ID_RESULTADOS = 'resultados-busqueda';
    var ID_PAGINACION = 'paginacion-busqueda';

    var TEXTO_TERMINO_VACIO = 'Escriba un término de búsqueda.';
    var TEXTO_CARGANDO = 'Buscando...';
    var TEXTO_SIN_RESULTADOS = 'No se encontraron resultados.';
    var TEXTO_ERROR = 'No fue posible realizar la búsqueda.';
    var TEXTO_VER_MAPA = 'Ver en mapa';
    var TEXTO_FUERA_VISTA = 'Fuera de la vista actual';

    // Mapeo Capa (valor real que devuelve el backend) -> clave interna de
    // layers.js. El backend devuelve 'Manzanas', 'Lotes', 'CodigosFijos' y
    // 'Vias' exactamente.
    var CLAVES_POR_CAPA = {
        'manzanas': 'manzanas',
        'lotes': 'lotes',
        'codigosfijos': 'codigosfijos',
        'vias': 'vias'
    };

    // Etiquetas legibles para el encabezado de cada tarjeta.
    var ETIQUETAS_CAPA = {
        'manzanas': 'Manzanas',
        'lotes': 'Lotes',
        'codigosfijos': 'Códigos Fijos',
        'vias': 'Vías'
    };

    // Estado del módulo. `secuencia` protege contra respuestas obsoletas: solo
    // se pinta la respuesta cuyo número coincide con la última búsqueda emitida.
    var secuencia = 0;
    var paginaActual = PAGINA_INICIAL;
    var terminoActual = '';
    var capaActual = '';

    // --- Utilidades de DOM y de valores -------------------------------------

    function obtenerNodo(id) {
        return document.getElementById(id);
    }

    // Tolerante a mayúsculas/minúsculas y a acentos, igual que el backend.
    function normalizar(texto) {
        if (typeof texto !== 'string') {
            return '';
        }
        var base = texto;
        if (typeof base.normalize === 'function') {
            base = base.normalize('NFD').replace(/[\u0300-\u036f]/g, '');
        }
        return base.toLowerCase().trim();
    }

    function esArreglo(valor) {
        return Object.prototype.toString.call(valor) === '[object Array]';
    }

    // Lectura de campo tolerante al casing: el contrato observado usa camelCase
    // en el nivel superior (pagina, limite, totalRegistros, totalPaginas, datos)
    // y PascalCase en cada item (Capa, Clave, Titulo, Subtitulo).
    function valorCampo(obj, nombre) {
        if (!obj || typeof obj !== 'object') {
            return null;
        }
        if (Object.prototype.hasOwnProperty.call(obj, nombre)) {
            return obj[nombre];
        }
        var buscado = String(nombre).toLowerCase();
        var claves = Object.keys(obj);
        for (var i = 0; i < claves.length; i++) {
            if (claves[i].toLowerCase() === buscado) {
                return obj[claves[i]];
            }
        }
        return null;
    }

    function numeroEntero(valor, porDefecto) {
        if (valor === null || valor === undefined || valor === '') {
            return porDefecto;
        }
        var numero = Number(valor);
        if (!isFinite(numero)) {
            return porDefecto;
        }
        return Math.floor(numero);
    }

    // Null, indefinido o cadena vacía se muestran como guion largo. Los guiones
    // que ya envía el backend (contrato) se conservan tal cual.
    function mostrarValor(valor) {
        if (valor === null || valor === undefined) {
            return '—';
        }
        var texto = String(valor);
        if (texto.trim() === '') {
            return '—';
        }
        return texto;
    }

    function limpiarNodo(nodo) {
        if (!nodo) {
            return;
        }
        while (nodo.firstChild) {
            nodo.removeChild(nodo.firstChild);
        }
    }

    function limpiarResultados() {
        limpiarNodo(obtenerNodo(ID_RESULTADOS));
    }

    function fijarEstado(texto, modificador) {
        var nodo = obtenerNodo(ID_ESTADO);
        if (!nodo) {
            return;
        }
        nodo.textContent = texto;
        nodo.className = 'visor-busqueda__estado'
            + (modificador ? ' visor-busqueda__estado--' + modificador : '');
    }

    function ocultarPaginacion() {
        var nodo = obtenerNodo(ID_PAGINACION);
        if (!nodo) {
            return;
        }
        limpiarNodo(nodo);
        nodo.hidden = true;
    }

    function claveInterna(capa) {
        var buscado = normalizar(capa);
        if (Object.prototype.hasOwnProperty.call(CLAVES_POR_CAPA, buscado)) {
            return CLAVES_POR_CAPA[buscado];
        }
        return null;
    }

    function etiquetaCapa(capa) {
        var clave = claveInterna(capa);
        if (clave && ETIQUETAS_CAPA[clave]) {
            return ETIQUETAS_CAPA[clave];
        }
        return mostrarValor(capa);
    }

    function obtenerToken() {
        if (ns.sesion && typeof ns.sesion.obtenerToken === 'function') {
            return ns.sesion.obtenerToken();
        }
        return null;
    }

    // Devuelve el valor de `capa` del selector solo si es uno de los valores
    // reales del backend; cualquier otro valor se trata como «Todas» (sin capa).
    function obtenerCapaSeleccionada() {
        var nodo = obtenerNodo(ID_FILTRO);
        var valor = nodo ? String(nodo.value) : '';
        if (Object.prototype.hasOwnProperty.call(CLAVES_POR_CAPA, normalizar(valor))) {
            return valor;
        }
        return '';
    }

    function textoConteo(total) {
        return total + (total === 1 ? ' resultado encontrado' : ' resultados encontrados');
    }

    // --- Llamada al endpoint real -------------------------------------------

    function construirRuta(termino, capa, pagina) {
        var ruta = RUTA_BUSQUEDA
            + '?q=' + encodeURIComponent(termino)
            + '&pagina=' + encodeURIComponent(pagina)
            + '&limite=' + encodeURIComponent(LIMITE);
        if (capa) {
            // Los nombres reales de capa conservan su casing: Manzanas, Lotes,
            // CodigosFijos, Vias.
            ruta += '&capa=' + encodeURIComponent(capa);
        }
        return ruta;
    }

    function normalizarPaged(datos) {
        var items = valorCampo(datos, 'datos');
        var lista = [];
        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            if (!item || typeof item !== 'object') {
                continue;
            }
            lista.push({
                capa: valorCampo(item, 'Capa'),
                clave: valorCampo(item, 'Clave'),
                titulo: valorCampo(item, 'Titulo'),
                subtitulo: valorCampo(item, 'Subtitulo')
            });
        }
        return {
            pagina: numeroEntero(valorCampo(datos, 'pagina'), PAGINA_INICIAL),
            limite: numeroEntero(valorCampo(datos, 'limite'), LIMITE),
            totalRegistros: numeroEntero(valorCampo(datos, 'totalRegistros'), lista.length),
            totalPaginas: numeroEntero(valorCampo(datos, 'totalPaginas'), 0),
            items: lista
        };
    }

    // Nunca rechaza: siempre resuelve con un objeto normalizado.
    function consultar(termino, capa, pagina) {
        return new Promise(function (resolver) {
            if (!ns.api || typeof ns.api.peticion !== 'function') {
                resolver({ ok: false, status: 0, error: 'contrato', paged: null });
                return;
            }
            try {
                ns.api.peticion(construirRuta(termino, capa, pagina), {
                    metodo: 'GET',
                    token: obtenerToken(),
                    protegida: true
                }).then(function (resultado) {
                    var datos = (resultado && resultado.datos) ? resultado.datos : null;
                    if (resultado && resultado.ok && resultado.status === 200
                        && datos && esArreglo(valorCampo(datos, 'datos'))) {
                        resolver({
                            ok: true,
                            status: resultado.status,
                            error: null,
                            paged: normalizarPaged(datos)
                        });
                        return;
                    }
                    if (resultado && resultado.ok && resultado.status === 200) {
                        resolver({ ok: false, status: resultado.status, error: 'contrato', paged: null });
                        return;
                    }
                    resolver({
                        ok: false,
                        status: resultado ? resultado.status : 0,
                        error: (resultado && resultado.error) ? resultado.error : 'contrato',
                        paged: null
                    });
                }).catch(function () {
                    resolver({ ok: false, status: 0, error: 'contrato', paged: null });
                });
            } catch (e) {
                resolver({ ok: false, status: 0, error: 'contrato', paged: null });
            }
        });
    }

    // --- Resultados en el mapa (solo si la feature ya está cargada) ---------

    function buscarCapaCargada(clave, id) {
        if (!clave || !ns.capas || typeof ns.capas.buscarFeatureCargada !== 'function') {
            return null;
        }
        return ns.capas.buscarFeatureCargada(clave, id);
    }

    function abrirPopup(capa) {
        if (capa && typeof capa.openPopup === 'function') {
            capa.openPopup();
        }
    }

    // Tras localizar, el movimiento del mapa dispara el `moveend` de FE-SIG 3,
    // que recarga las capas activas y recrea sus geometrías: la instancia
    // anterior se destruye y con ella su popup. Para que el popup local
    // permanezca, se vuelve a abrir en la NUEVA instancia de la misma entidad a
    // través del evento `layeradd` del grupo de la capa. El manejador se retira
    // al primer acierto y, como máximo, a los 4 s (limpieza acotada en el
    // tiempo), de modo que nunca queda un listener colgado.
    function programarReaperturaPopup(clave, id) {
        var grupo = (ns.capas && typeof ns.capas.obtenerCapa === 'function')
            ? ns.capas.obtenerCapa(clave)
            : null;
        if (!grupo || typeof grupo.on !== 'function' || typeof grupo.off !== 'function') {
            return;
        }
        var buscado = String(id);
        var pendiente = true;
        var temporizador = null;

        function limpiar() {
            pendiente = false;
            grupo.off('layeradd', alAgregar);
            if (temporizador) {
                clearTimeout(temporizador);
                temporizador = null;
            }
        }

        function alAgregar(evento) {
            var capa = evento ? evento.layer : null;
            if (!pendiente || !capa || !capa.feature) {
                return;
            }
            if (String(capa.feature.id) !== buscado) {
                return;
            }
            limpiar();
            abrirPopup(capa);
        }

        grupo.on('layeradd', alAgregar);
        temporizador = setTimeout(limpiar, 4000);
    }

    function localizar(item, clave) {
        var mapa = (ns.mapa && typeof ns.mapa.obtener === 'function') ? ns.mapa.obtener() : null;
        if (!mapa) {
            return;
        }
        // Se vuelve a resolver la feature al momento del clic: si la capa se
        // desactivó o el mapa se movió, la entidad podría ya no estar cargada.
        var capa = buscarCapaCargada(clave, item.clave);
        if (!capa) {
            return;
        }

        // Si el modo Identificar está activo, se desactiva antes de abrir el
        // popup local, para no ofrecer una UX contradictoria.
        if (ns.identify && typeof ns.identify.estaActivo === 'function'
            && ns.identify.estaActivo()
            && typeof ns.identify.desactivar === 'function') {
            ns.identify.desactivar();
        }

        if (typeof capa.getLatLng === 'function') {
            mapa.panTo(capa.getLatLng());
        } else if (typeof capa.getBounds === 'function') {
            mapa.fitBounds(capa.getBounds(), { padding: [30, 30], maxZoom: 18 });
        }

        abrirPopup(capa);
        programarReaperturaPopup(clave, item.clave);
    }

    function crearAccionLocalizacion(item) {
        var contenedor = document.createElement('div');
        contenedor.className = 'visor-busqueda__accion';

        var clave = claveInterna(item.capa);
        var capa = buscarCapaCargada(clave, item.clave);

        if (capa) {
            var boton = document.createElement('button');
            boton.type = 'button';
            boton.className = 'btn-secundario visor-busqueda__ver';
            boton.textContent = TEXTO_VER_MAPA;
            boton.addEventListener('click', function () {
                localizar(item, clave);
            });
            contenedor.appendChild(boton);
        } else {
            var aviso = document.createElement('span');
            aviso.className = 'visor-busqueda__fuera';
            aviso.textContent = TEXTO_FUERA_VISTA;
            contenedor.appendChild(aviso);
        }
        return contenedor;
    }

    // --- Render de resultados -----------------------------------------------

    function crearMeta(etiqueta, valor) {
        var fila = document.createElement('div');
        fila.className = 'visor-busqueda__meta-fila';

        var termino = document.createElement('dt');
        termino.className = 'visor-busqueda__meta-clave';
        termino.textContent = etiqueta;

        var definicion = document.createElement('dd');
        definicion.className = 'visor-busqueda__meta-valor';
        definicion.textContent = valor;

        fila.appendChild(termino);
        fila.appendChild(definicion);
        return fila;
    }

    function crearTarjeta(item) {
        var tarjeta = document.createElement('article');
        tarjeta.className = 'visor-busqueda__resultado';

        var capa = document.createElement('p');
        capa.className = 'visor-busqueda__resultado-capa';
        capa.textContent = etiquetaCapa(item.capa);
        tarjeta.appendChild(capa);

        var titulo = document.createElement('h4');
        titulo.className = 'visor-busqueda__resultado-titulo';
        titulo.textContent = mostrarValor(item.titulo);
        tarjeta.appendChild(titulo);

        var subtitulo = document.createElement('p');
        subtitulo.className = 'visor-busqueda__resultado-subtitulo';
        subtitulo.textContent = mostrarValor(item.subtitulo);
        tarjeta.appendChild(subtitulo);

        var meta = document.createElement('dl');
        meta.className = 'visor-busqueda__meta';
        meta.appendChild(crearMeta('Clave', mostrarValor(item.clave)));
        meta.appendChild(crearMeta('Capa', mostrarValor(item.capa)));
        tarjeta.appendChild(meta);

        tarjeta.appendChild(crearAccionLocalizacion(item));
        return tarjeta;
    }

    function renderizarResultados(items) {
        var contenedor = obtenerNodo(ID_RESULTADOS);
        if (!contenedor) {
            return;
        }
        limpiarResultados();
        for (var i = 0; i < items.length; i++) {
            contenedor.appendChild(crearTarjeta(items[i]));
        }
        // Al cambiar de página o término, los resultados vuelven al inicio.
        contenedor.scrollTop = 0;
    }

    function renderizarPaginacion(paged) {
        var contenedor = obtenerNodo(ID_PAGINACION);
        if (!contenedor) {
            return;
        }
        limpiarNodo(contenedor);

        var totalPaginas = paged.totalPaginas;
        if (!(totalPaginas > 1)) {
            contenedor.hidden = true;
            return;
        }
        contenedor.hidden = false;

        var pagina = Math.min(Math.max(PAGINA_INICIAL, paged.pagina), totalPaginas);

        var anterior = document.createElement('button');
        anterior.type = 'button';
        anterior.className = 'btn-secundario visor-busqueda__pagina-boton';
        anterior.textContent = 'Anterior';
        anterior.disabled = pagina <= 1;
        anterior.addEventListener('click', function () {
            buscar(pagina - 1);
        });

        var etiqueta = document.createElement('span');
        etiqueta.className = 'visor-busqueda__pagina-estado';
        etiqueta.textContent = 'Página ' + pagina + ' de ' + totalPaginas;

        var siguiente = document.createElement('button');
        siguiente.type = 'button';
        siguiente.className = 'btn-secundario visor-busqueda__pagina-boton';
        siguiente.textContent = 'Siguiente';
        siguiente.disabled = pagina >= totalPaginas;
        siguiente.addEventListener('click', function () {
            buscar(pagina + 1);
        });

        contenedor.appendChild(anterior);
        contenedor.appendChild(etiqueta);
        contenedor.appendChild(siguiente);
    }

    function aplicarRespuesta(resultado) {
        if (resultado.status === 401) {
            // Sesión expirada: api.js limpia y guard.js redirige; no se pisa
            // ese flujo con un mensaje de error propio.
            return;
        }
        if (!resultado.ok || !resultado.paged) {
            fijarEstado(TEXTO_ERROR, 'error');
            limpiarResultados();
            ocultarPaginacion();
            return;
        }

        var paged = resultado.paged;
        paginaActual = paged.pagina;

        if (paged.totalRegistros === 0 || paged.items.length === 0) {
            fijarEstado(TEXTO_SIN_RESULTADOS, 'vacio');
            limpiarResultados();
            ocultarPaginacion();
            return;
        }

        // El conteo viene de totalRegistros, nunca de datos.length.
        fijarEstado(textoConteo(paged.totalRegistros), 'exito');
        renderizarResultados(paged.items);
        renderizarPaginacion(paged);
    }

    // --- API pública --------------------------------------------------------

    function buscar(pagina) {
        var nodoTexto = obtenerNodo(ID_TEXTO);
        var termino = nodoTexto ? String(nodoTexto.value).trim() : '';

        if (termino === '') {
            // No se envía una búsqueda vacía: no se llama a la API.
            fijarEstado(TEXTO_TERMINO_VACIO, 'aviso');
            limpiarResultados();
            ocultarPaginacion();
            return;
        }

        terminoActual = termino;
        capaActual = obtenerCapaSeleccionada();
        paginaActual = (typeof pagina === 'number' && isFinite(pagina) && pagina >= 1)
            ? Math.floor(pagina)
            : PAGINA_INICIAL;

        secuencia += 1;
        var actual = secuencia;

        fijarEstado(TEXTO_CARGANDO, 'cargando');

        consultar(terminoActual, capaActual, paginaActual).then(function (resultado) {
            // Respuesta obsoleta: una búsqueda más reciente ya salió.
            if (actual !== secuencia) {
                return;
            }
            aplicarRespuesta(resultado);
        });
    }

    function limpiar() {
        // Invalida cualquier respuesta en vuelo.
        secuencia += 1;
        paginaActual = PAGINA_INICIAL;
        terminoActual = '';
        capaActual = '';

        var nodoTexto = obtenerNodo(ID_TEXTO);
        if (nodoTexto) {
            nodoTexto.value = '';
        }

        limpiarResultados();
        ocultarPaginacion();
        fijarEstado('', null);
    }

    function manejarSubmit(evento) {
        evento.preventDefault();
        buscar(PAGINA_INICIAL);
    }

    function manejarCambioFiltro() {
        var nodoTexto = obtenerNodo(ID_TEXTO);
        var termino = nodoTexto ? String(nodoTexto.value).trim() : '';
        // Con el campo vacío no se dispara ninguna búsqueda.
        if (termino === '') {
            return;
        }
        buscar(PAGINA_INICIAL);
    }

    function inicializar() {
        var form = obtenerNodo(ID_FORM);
        if (form && form.getAttribute('data-busqueda-enlazada') !== '1') {
            form.setAttribute('data-busqueda-enlazada', '1');
            form.addEventListener('submit', manejarSubmit);
        }

        var botonLimpiar = obtenerNodo(ID_BOTON_LIMPIAR);
        if (botonLimpiar && botonLimpiar.getAttribute('data-busqueda-enlazada') !== '1') {
            botonLimpiar.setAttribute('data-busqueda-enlazada', '1');
            botonLimpiar.addEventListener('click', limpiar);
        }

        var filtro = obtenerNodo(ID_FILTRO);
        if (filtro && filtro.getAttribute('data-busqueda-enlazada') !== '1') {
            filtro.setAttribute('data-busqueda-enlazada', '1');
            filtro.addEventListener('change', manejarCambioFiltro);
        }

        // Estado inicial: sin mensaje obligatorio.
        fijarEstado('', null);
        ocultarPaginacion();
    }

    ns.search = {
        inicializar: inicializar,
        buscar: buscar,
        limpiar: limpiar
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', inicializar);
    } else {
        inicializar();
    }
})(window.VisorSIG);
