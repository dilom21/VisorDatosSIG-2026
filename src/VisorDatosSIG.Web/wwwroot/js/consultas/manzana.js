// FE-CU14: consulta de Manzana (pantalla /Consultas/Manzana). Solo lectura.
//
// La Web consume exclusivamente la API con el token de la sesión:
//   GET api/manzanas?uvMza&uv&mza&pagina&limite  -> página de resultados
//   GET api/manzanas/{idManzana}                 -> detalle + geometría GeoJSON
//
// La paginación es del servidor (SQL Server): nunca se descargan todos los registros para
// paginar en el navegador. La tabla muestra solo los campos del DTO resumido real
// (idManzana, uvMza, uv, mza) y NO dispara una llamada de detalle por fila.
//
// Geometría: el backend entrega Polygon o MultiPolygon como GeoJSON conservando el orden
// [longitud, latitud]. Aquí NO se convierte a Point, NO se calcula centroide y NO se invierten
// las coordenadas: si la geometría es null se muestra "Sin geometría disponible".
//
// Autenticación: se usa exclusivamente window.VisorSIG.api.peticion(...) con el token de la
// sesión y `protegida: true`. Un 401 se deja al flujo existente (api.js limpia la sesión y
// guard.js redirige); un 403 se informa sin mostrar datos.
//
// Integración con el visor: NO implementada todavía. "Ver en el mapa" solo muestra un aviso de
// "Disponible próximamente"; no se carga Leaflet ni se modifica layers.js.
//
// Seguridad del DOM: createElement + textContent; nada que llegue del backend se interpreta
// como HTML.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var RUTA_LISTA = 'api/manzanas';
    var RUTA_DETALLE = 'api/manzanas/';

    var PAGINA_INICIAL = 1;
    var LIMITE_INICIAL = 8;
    var SIN_DATO = '—';
    var MS_TOAST = 4000;
    var MS_COPIA = 2500;

    var ID_FORM = 'mz-filtros';
    var ID_UVMZA = 'mz-uvmza';
    var ID_UV = 'mz-uv';
    var ID_MZA = 'mz-mza';
    var ID_BUSCAR = 'mz-buscar';
    var ID_LIMPIAR = 'mz-limpiar';
    var ID_LIMITE = 'mz-limite';
    var ID_CUERPO = 'mz-cuerpo';
    var ID_TABLA = 'mz-tabla-envoltura';
    var ID_VACIO = 'mz-vacio';
    var ID_TITULO = 'mz-titulo';
    var ID_CONTEO = 'mz-conteo';
    var ID_PAGINACION = 'mz-paginacion';
    var ID_CARGANDO = 'mz-cargando';
    var ID_AVISO = 'mz-aviso';
    var ID_EXITO = 'mz-exito';
    var ID_TOAST = 'mz-toast';

    var ID_DET_VACIO = 'mz-det-vacio';
    var ID_DET_CARGANDO = 'mz-det-cargando';
    var ID_DET_CONTENIDO = 'mz-det-contenido';
    var ID_DET_BADGE = 'mz-det-badge';
    var ID_DET_ID = 'mz-det-id';
    var ID_DET_ORIGEN = 'mz-det-origen';
    var ID_DET_UVMZA = 'mz-det-uvmza';
    var ID_DET_UV = 'mz-det-uv';
    var ID_DET_MZA = 'mz-det-mza';
    var ID_GEOJSON = 'mz-geojson';
    var ID_GEOJSON_NUMS = 'mz-geojson-nums';
    var ID_COPIAR = 'mz-copiar';
    var ID_COPIA_FEEDBACK = 'mz-copiar-feedback';
    var ID_VER_MAPA = 'mz-ver-mapa';

    var TEXTO_VACIO_DETALLE = 'Seleccione un registro para visualizar su detalle.';
    var TEXTO_ERROR_LISTA = 'No fue posible realizar la consulta.';
    var TEXTO_ERROR_DETALLE = 'No fue posible cargar el detalle de la Manzana.';
    var TEXTO_SIN_PERMISO = 'No tiene permisos para consultar manzanas.';
    var TEXTO_SIN_GEOMETRIA = 'Sin geometría disponible';
    var TEXTO_COPIADO = 'GeoJSON copiado';
    var TEXTO_COPIA_ERROR = 'No se pudo copiar el GeoJSON';
    var TEXTO_MAPA = 'Disponible próximamente';

    // Estado del módulo.
    var paginaActual = PAGINA_INICIAL;
    var limiteActual = LIMITE_INICIAL;
    var totalRegistros = 0;
    var totalPaginas = 0;
    var seleccionActual = null;
    var geojsonActual = '';
    var idsPagina = [];
    // Protege contra respuestas obsoletas cuando se encadenan consultas.
    var secuencia = 0;
    var secuenciaDetalle = 0;
    var temporizadorToast = null;
    var temporizadorCopia = null;

    // --- Utilidades ---------------------------------------------------------

    function nodo(id) {
        return document.getElementById(id);
    }

    function vaciar(contenedor) {
        while (contenedor && contenedor.firstChild) {
            contenedor.removeChild(contenedor.firstChild);
        }
    }

    function crear(etiqueta, clase, texto) {
        var elemento = document.createElement(etiqueta);
        if (clase) {
            elemento.className = clase;
        }
        if (typeof texto === 'string') {
            elemento.textContent = texto;
        }
        return elemento;
    }

    function esArreglo(comprobado) {
        return Object.prototype.toString.call(comprobado) === '[object Array]';
    }

    // Lectura tolerante al casing (la API responde en camelCase).
    function valor(objeto, nombre) {
        if (!objeto || typeof objeto !== 'object') {
            return undefined;
        }
        if (Object.prototype.hasOwnProperty.call(objeto, nombre)) {
            return objeto[nombre];
        }
        var buscado = nombre.toLowerCase();
        for (var clave in objeto) {
            if (Object.prototype.hasOwnProperty.call(objeto, clave)
                && clave.toLowerCase() === buscado) {
                return objeto[clave];
            }
        }
        return undefined;
    }

    function textoComprobado(comprobado) {
        return typeof comprobado === 'string' ? comprobado.trim() : '';
    }

    function mostrable(comprobado) {
        if (comprobado === null || comprobado === undefined) {
            return SIN_DATO;
        }
        var limpio = typeof comprobado === 'string' ? comprobado.trim() : String(comprobado);
        return limpio === '' ? SIN_DATO : limpio;
    }

    function aEntero(comprobado, alterno) {
        var numero = Number(comprobado);
        return isFinite(numero) ? Math.floor(numero) : alterno;
    }

    function mensajeError(resultado, alterno) {
        var cuerpo = resultado ? resultado.datos : null;

        if (cuerpo && typeof cuerpo === 'object') {
            var detalle = textoComprobado(valor(cuerpo, 'detail'));
            var titulo = textoComprobado(valor(cuerpo, 'title'));
            var errores = valor(cuerpo, 'errors');

            if (errores && typeof errores === 'object') {
                for (var campo in errores) {
                    if (!Object.prototype.hasOwnProperty.call(errores, campo)) {
                        continue;
                    }
                    if (esArreglo(errores[campo]) && errores[campo].length > 0) {
                        return String(errores[campo][0]);
                    }
                }
            }

            if (detalle !== '' && titulo !== '') {
                return titulo + ': ' + detalle;
            }
            if (detalle !== '') {
                return detalle;
            }
            if (titulo !== '') {
                return titulo;
            }
        }

        if (resultado && resultado.error === 'timeout') {
            return 'La API no respondió a tiempo. Intente nuevamente.';
        }

        return alterno;
    }

    function aviso(mensaje, tono) {
        var zona = nodo(ID_AVISO);
        if (!zona) {
            return;
        }
        zona.className = tono === 'error' ? 'mz-alerta mz-alerta--error' : 'mz-alerta mz-alerta--info';
        zona.textContent = mensaje || '';
        zona.hidden = !mensaje;
    }

    function ponerTexto(id, texto) {
        var elemento = nodo(id);
        if (elemento) {
            elemento.textContent = texto;
        }
    }

    // --- Tarjeta de éxito (header) ------------------------------------------

    function mostrarExito(cantidad) {
        var zona = nodo(ID_EXITO);
        if (!zona) {
            return;
        }
        vaciar(zona);

        var icono = crear('span', 'mz-exito__icono', null);
        icono.setAttribute('aria-hidden', 'true');

        var cuerpo = crear('div', 'mz-exito__cuerpo', null);
        cuerpo.appendChild(crear('p', 'mz-exito__titulo', 'Consulta realizada correctamente'));
        cuerpo.appendChild(crear('p', 'mz-exito__texto',
            'Se ' + (cantidad === 1
                ? 'encontró 1 registro'
                : 'encontraron ' + cantidad + ' registros') + ' de manzana.'));

        var cerrar = crear('button', 'mz-exito__cerrar', '\u00d7');
        cerrar.type = 'button';
        cerrar.setAttribute('aria-label', 'Cerrar aviso');
        cerrar.addEventListener('click', ocultarExito);

        zona.appendChild(icono);
        zona.appendChild(cuerpo);
        zona.appendChild(cerrar);
        zona.hidden = false;
    }

    function ocultarExito() {
        var zona = nodo(ID_EXITO);
        if (zona) {
            zona.hidden = true;
            vaciar(zona);
        }
    }

    // --- Aviso flotante -----------------------------------------------------

    function mostrarToast(mensaje) {
        var toast = nodo(ID_TOAST);
        if (!toast) {
            return;
        }
        vaciar(toast);

        var icono = crear('span', 'mz-toast__icono', null);
        icono.setAttribute('aria-hidden', 'true');
        toast.appendChild(icono);
        toast.appendChild(crear('span', 'mz-toast__texto', mensaje));
        toast.hidden = false;

        // Reinicia la animación de entrada cuando se reutiliza el contenedor.
        toast.style.animation = 'none';
        void toast.offsetWidth;
        toast.style.animation = '';

        if (temporizadorToast) {
            clearTimeout(temporizadorToast);
        }
        temporizadorToast = setTimeout(ocultarToast, MS_TOAST);
    }

    function ocultarToast() {
        if (temporizadorToast) {
            clearTimeout(temporizadorToast);
            temporizadorToast = null;
        }
        var toast = nodo(ID_TOAST);
        if (toast) {
            toast.hidden = true;
            vaciar(toast);
        }
    }

    // --- Indicadores de la lista --------------------------------------------

    function mostrarCargando(activo) {
        var indicador = nodo(ID_CARGANDO);
        if (indicador) {
            indicador.hidden = !activo;
        }

        var buscar = nodo(ID_BUSCAR);
        if (buscar) {
            buscar.disabled = activo;
            if (activo) {
                buscar.setAttribute('aria-busy', 'true');
            } else {
                buscar.removeAttribute('aria-busy');
            }
        }

        if (activo) {
            pintarEsqueleto();
        }
    }

    // Esqueleto: filas provisionales que no muestran datos inventados.
    function pintarEsqueleto() {
        var cuerpo = nodo(ID_CUERPO);
        if (!cuerpo) {
            return;
        }
        vaciar(cuerpo);
        for (var i = 0; i < LIMITE_INICIAL; i++) {
            var fila = crear('tr', 'mz-fila mz-fila--esqueleto', null);
            for (var j = 0; j < 6; j++) {
                fila.appendChild(crear('td', null, null));
            }
            cuerpo.appendChild(fila);
        }
    }

    function limpiarResultados() {
        vaciar(nodo(ID_CUERPO));
        idsPagina = [];
    }

    function actualizarTitulo() {
        var titulo = nodo(ID_TITULO);
        if (titulo) {
            titulo.textContent = 'Resultados (' + totalRegistros + ' registros)';
        }
    }

    function mostrarVacio(activo) {
        var zonaVacia = nodo(ID_VACIO);
        var envoltura = nodo(ID_TABLA);
        if (zonaVacia) {
            zonaVacia.hidden = !activo;
        }
        if (envoltura) {
            envoltura.hidden = activo;
        }
    }

    function actualizarConteo(mostrados) {
        var zona = nodo(ID_CONTEO);
        if (!zona) {
            return;
        }
        if (totalRegistros === 0 || !(mostrados > 0)) {
            zona.textContent = '';
            return;
        }
        var desde = (paginaActual - 1) * limiteActual + 1;
        var hasta = desde + mostrados - 1;
        zona.textContent = 'Mostrando ' + desde + ' a ' + hasta + ' de ' + totalRegistros + ' registros';
    }

    function ocultarPaginacion() {
        var contenedor = nodo(ID_PAGINACION);
        if (contenedor) {
            vaciar(contenedor);
            contenedor.hidden = true;
        }
    }

    function aplicarLimiteEnCombo() {
        var combo = nodo(ID_LIMITE);
        if (!combo) {
            return;
        }
        var objetivo = String(limiteActual);
        for (var i = 0; i < combo.options.length; i++) {
            if (combo.options[i].value === objetivo) {
                combo.value = objetivo;
                return;
            }
        }
    }

    // 403 de la API: la autorización es del servidor y no se muestra ningún dato.
    function bloquearPantalla() {
        var form = nodo(ID_FORM);
        var grid = document.querySelector('.mz-grid');
        if (form) {
            form.hidden = true;
        }
        if (grid) {
            grid.hidden = true;
        }
        limpiarResultados();
        ocultarPaginacion();
        ocultarExito();
        ocultarToast();
        detalleVacio(null);
        aviso(TEXTO_SIN_PERMISO, 'error');
    }

    function detalleCargando() {
        var cargando = nodo(ID_DET_CARGANDO);
        var vacio = nodo(ID_DET_VACIO);
        var contenido = nodo(ID_DET_CONTENIDO);
        if (cargando) {
            cargando.hidden = false;
        }
        if (vacio) {
            vacio.hidden = true;
        }
        if (contenido) {
            contenido.hidden = true;
        }
    }

    function detalleVacio(mensaje) {
        var cargando = nodo(ID_DET_CARGANDO);
        var vacio = nodo(ID_DET_VACIO);
        var contenido = nodo(ID_DET_CONTENIDO);
        var badge = nodo(ID_DET_BADGE);

        if (cargando) {
            cargando.hidden = true;
        }
        if (contenido) {
            contenido.hidden = true;
        }
        if (vacio) {
            vacio.textContent = mensaje || TEXTO_VACIO_DETALLE;
            vacio.hidden = false;
        }
        if (badge) {
            badge.hidden = true;
        }
    }

    // --- Filtros y consulta -------------------------------------------------

    function valorCampo(id) {
        var campo = nodo(id);
        return campo ? String(campo.value).trim() : '';
    }

    // Solo se envían los filtros que el backend acepta: uvMza, uv, mza.
    function construirParametros(pagina) {
        var partes = [];

        function agregar(nombre, parametro) {
            if (parametro === '' || parametro === null || parametro === undefined) {
                return;
            }
            partes.push(nombre + '=' + encodeURIComponent(String(parametro)));
        }

        agregar('pagina', String(pagina));
        agregar('limite', String(limiteActual));
        agregar('uvMza', valorCampo(ID_UVMZA));
        agregar('uv', valorCampo(ID_UV));
        agregar('mza', valorCampo(ID_MZA));

        return partes.join('&');
    }

    // Petición protegida: el 401 queda a cargo del flujo existente (api.js + guard.js).
    function llamar(ruta, parametros) {
        var token = ns.sesion ? ns.sesion.obtenerToken() : null;
        if (!token) {
            return Promise.resolve({ ok: false, status: 401, datos: null, error: null });
        }
        return ns.api.peticion(ruta + (parametros ? '?' + parametros : ''), {
            metodo: 'GET',
            token: token,
            protegida: true
        });
    }

    // Página pedida al servidor: el cliente nunca pagina ni filtra localmente.
    function consultar(pagina) {
        aviso('', null);

        var pedida = aEntero(pagina, PAGINA_INICIAL);
        paginaActual = pedida >= 1 ? pedida : PAGINA_INICIAL;

        secuencia += 1;
        var actual = secuencia;

        mostrarCargando(true);

        llamar(RUTA_LISTA, construirParametros(paginaActual)).then(function (resultado) {
            if (actual !== secuencia) {
                // Una consulta más reciente ya salió: se descarta esta respuesta.
                return;
            }

            mostrarCargando(false);

            if (resultado.status === 401) {
                return;
            }

            if (resultado.status === 403) {
                bloquearPantalla();
                return;
            }

            if (!resultado.ok || !resultado.datos) {
                limpiarResultados();
                totalRegistros = 0;
                totalPaginas = 0;
                mostrarVacio(false);
                actualizarTitulo();
                actualizarConteo(0);
                ocultarPaginacion();
                ocultarExito();
                aviso(mensajeError(resultado, TEXTO_ERROR_LISTA), 'error');
                return;
            }

            var paginaDto = resultado.datos;
            var registros = valor(paginaDto, 'datos');
            registros = esArreglo(registros) ? registros : [];

            // El total y el tamaño los informa el servidor; nunca se cuentan en el cliente.
            paginaActual = aEntero(valor(paginaDto, 'pagina'), paginaActual);
            limiteActual = aEntero(valor(paginaDto, 'limite'), limiteActual);
            totalRegistros = aEntero(valor(paginaDto, 'totalRegistros'), 0);
            totalPaginas = aEntero(valor(paginaDto, 'totalPaginas'), 0);
            if (totalPaginas < 1 && totalRegistros > 0) {
                totalPaginas = Math.ceil(totalRegistros / limiteActual);
            }

            aplicarLimiteEnCombo();

            if (registros.length === 0) {
                limpiarResultados();
                actualizarTitulo();
                mostrarVacio(true);
                actualizarConteo(0);
                ocultarPaginacion();
                ocultarExito();
                return;
            }

            mostrarVacio(false);
            renderizarFilas(registros);
            actualizarTitulo();
            actualizarConteo(registros.length);
            renderizarPaginacion();
            mostrarExito(totalRegistros);

            // Autoselección: si el registro activo ya no está en la página, se selecciona
            // el primero y se carga su detalle automáticamente.
            if (idsPagina.indexOf(seleccionActual) === -1) {
                seleccionar(idsPagina[0]);
            } else {
                marcarSeleccion();
            }
        });
    }

    // --- Render de la tabla -------------------------------------------------

    function renderizarFilas(registros) {
        var cuerpo = nodo(ID_CUERPO);
        if (!cuerpo) {
            return;
        }
        vaciar(cuerpo);
        idsPagina = [];

        for (var i = 0; i < registros.length; i++) {
            var id = aEntero(valor(registros[i], 'idManzana'), 0);
            if (id > 0) {
                idsPagina.push(id);
            }
            cuerpo.appendChild(crearFila(registros[i], i));
        }
    }

    // La fila consume el DTO resumido real: no pide el detalle de cada registro.
    function crearFila(fila, indice) {
        var idManzana = aEntero(valor(fila, 'idManzana'), 0);
        var ordinal = (paginaActual - 1) * limiteActual + indice + 1;

        var tr = crear('tr', 'mz-fila', null);
        tr.setAttribute('data-id', String(idManzana));

        tr.appendChild(crear('td', 'mz-celda--num', String(ordinal)));
        tr.appendChild(crear('td', null, idManzana > 0 ? String(idManzana) : SIN_DATO));
        tr.appendChild(crear('td', null, mostrable(valor(fila, 'uvMza'))));
        tr.appendChild(crear('td', null, mostrable(valor(fila, 'uv'))));
        tr.appendChild(crear('td', null, mostrable(valor(fila, 'mza'))));

        var celdaAccion = crear('td', 'mz-celda--accion', null);
        var boton = crear('button', 'mz-btn mz-btn--mini mz-btn--ver mz-ver', null);
        boton.type = 'button';
        boton.setAttribute('aria-label', 'Ver el detalle de la manzana ' + idManzana);
        var icono = crear('span', 'mz-ver__icono', null);
        icono.setAttribute('aria-hidden', 'true');
        boton.appendChild(icono);
        boton.appendChild(document.createTextNode('Ver detalle'));
        boton.addEventListener('click', function (evento) {
            evento.stopPropagation();
            seleccionar(idManzana);
        });
        celdaAccion.appendChild(boton);
        tr.appendChild(celdaAccion);

        tr.addEventListener('click', function () {
            seleccionar(idManzana);
        });

        return tr;
    }

    function marcarSeleccion() {
        var cuerpo = nodo(ID_CUERPO);
        if (!cuerpo) {
            return;
        }
        var filas = cuerpo.querySelectorAll('.mz-fila');
        for (var i = 0; i < filas.length; i++) {
            var activa = filas[i].getAttribute('data-id') === String(seleccionActual);
            if (activa) {
                filas[i].classList.add('mz-fila--activa');
                filas[i].setAttribute('aria-selected', 'true');
            } else {
                filas[i].classList.remove('mz-fila--activa');
                filas[i].removeAttribute('aria-selected');
            }
        }
    }

    // --- Paginación del servidor --------------------------------------------

    function botonPagina(etiqueta, destino, titulo, deshabilitado, activo) {
        var boton = crear('button',
            'mz-paginacion__boton' + (activo ? ' mz-paginacion__boton--activo' : ''),
            etiqueta);
        boton.type = 'button';
        boton.disabled = !!deshabilitado;
        if (titulo) {
            boton.title = titulo;
        }
        if (activo) {
            boton.setAttribute('aria-current', 'page');
        } else if (titulo) {
            boton.setAttribute('aria-label', titulo);
        }
        boton.addEventListener('click', function () {
            consultar(destino);
        });
        return boton;
    }

    function renderizarPaginacion() {
        var contenedor = nodo(ID_PAGINACION);
        if (!contenedor) {
            return;
        }
        vaciar(contenedor);

        if (totalPaginas <= 1) {
            contenedor.hidden = true;
            return;
        }
        contenedor.hidden = false;

        contenedor.appendChild(botonPagina('«', 1, 'Primera página', paginaActual <= 1, false));
        contenedor.appendChild(botonPagina('‹', paginaActual - 1, 'Página anterior', paginaActual <= 1, false));

        var desde = Math.max(1, paginaActual - 2);
        var hasta = Math.min(totalPaginas, desde + 4);
        desde = Math.max(1, hasta - 4);
        for (var p = desde; p <= hasta; p++) {
            contenedor.appendChild(botonPagina(String(p), p, 'Página ' + p, false, p === paginaActual));
        }

        contenedor.appendChild(botonPagina('›', paginaActual + 1, 'Página siguiente', paginaActual >= totalPaginas, false));
        contenedor.appendChild(botonPagina('»', totalPaginas, 'Última página', paginaActual >= totalPaginas, false));
    }

    // --- Selección y panel de detalle ---------------------------------------

    function seleccionar(idManzana) {
        if (!(idManzana > 0)) {
            return;
        }
        seleccionActual = idManzana;
        marcarSeleccion();
        cargarDetalle(idManzana);
    }

    function cargarDetalle(idManzana) {
        secuenciaDetalle += 1;
        var actual = secuenciaDetalle;

        detalleCargando();

        llamar(RUTA_DETALLE + idManzana, null).then(function (resultado) {
            if (actual !== secuenciaDetalle) {
                // El usuario seleccionó otro registro: se descarta esta respuesta.
                return;
            }

            if (resultado.status === 401) {
                return;
            }

            if (resultado.status === 403) {
                bloquearPantalla();
                return;
            }

            if (!resultado.ok || !resultado.datos) {
                detalleVacio(TEXTO_ERROR_DETALLE);
                aviso(mensajeError(resultado, TEXTO_ERROR_DETALLE), 'error');
                return;
            }

            pintarDetalle(resultado.datos);
        });
    }

    function pintarDetalle(dto) {
        var cargando = nodo(ID_DET_CARGANDO);
        var vacio = nodo(ID_DET_VACIO);
        var contenido = nodo(ID_DET_CONTENIDO);

        if (cargando) {
            cargando.hidden = true;
        }
        if (vacio) {
            vacio.hidden = true;
        }
        if (contenido) {
            contenido.hidden = false;
        }

        ponerTexto(ID_DET_ID, mostrable(valor(dto, 'idManzana')));
        ponerTexto(ID_DET_ORIGEN, mostrable(valor(dto, 'idOrigen')));
        ponerTexto(ID_DET_UVMZA, mostrable(valor(dto, 'uvMza')));
        ponerTexto(ID_DET_UV, mostrable(valor(dto, 'uv')));
        ponerTexto(ID_DET_MZA, mostrable(valor(dto, 'mza')));

        var geometria = valor(dto, 'geometria');
        var tipo = tipoGeometria(geometria);

        var badge = nodo(ID_DET_BADGE);
        if (badge) {
            if (tipo === null) {
                badge.hidden = true;
            } else {
                badge.textContent = tipo;
                badge.hidden = false;
            }
        }

        pintarGeojson(geometria);
    }

    // Tipo geométrico real devuelto por el backend (Polygon / MultiPolygon). Nunca se
    // convierte a Point ni se calcula centroide.
    function tipoGeometria(geometria) {
        if (!geometria || typeof geometria !== 'object') {
            return null;
        }
        var tipo = textoComprobado(valor(geometria, 'type'));
        return tipo !== '' ? tipo : null;
    }

    // --- GeoJSON ------------------------------------------------------------

    // Se conserva el orden [longitud, latitud] y la estructura Polygon/MultiPolygon tal
    // como llega del backend. Si es null se informa que no hay geometría.
    function formatearGeojson(geometria) {
        if (geometria === null || geometria === undefined) {
            return null;
        }

        if (typeof geometria === 'object') {
            try {
                return JSON.stringify(geometria, null, 2);
            } catch (e) {
                return null;
            }
        }

        if (typeof geometria === 'string') {
            var limpio = geometria.trim();
            if (limpio === '') {
                return null;
            }
            try {
                return JSON.stringify(JSON.parse(limpio), null, 2);
            } catch (e) {
                // No se muestra ningún hexadecimal de SQL Server: si no es JSON válido,
                // se muestra el texto tal cual llegó.
                return limpio;
            }
        }

        return null;
    }

    // Numeración de líneas del bloque de código (solo presentación).
    function pintarNumerosLineas(texto) {
        var gutter = nodo(ID_GEOJSON_NUMS);
        if (!gutter) {
            return;
        }
        vaciar(gutter);
        if (!texto) {
            return;
        }
        var lineas = texto.split('\n');
        for (var i = 0; i < lineas.length; i++) {
            gutter.appendChild(crear('div', null, String(i + 1)));
        }
    }

    function pintarGeojson(geometria) {
        var bloque = nodo(ID_GEOJSON);
        var boton = nodo(ID_COPIAR);
        var formateado = formatearGeojson(geometria);

        if (formateado === null) {
            geojsonActual = '';
            if (bloque) {
                bloque.textContent = TEXTO_SIN_GEOMETRIA;
            }
            pintarNumerosLineas('');
            if (boton) {
                boton.disabled = true;
            }
            return;
        }

        geojsonActual = formateado;
        if (bloque) {
            bloque.textContent = formateado;
        }
        pintarNumerosLineas(formateado);
        if (boton) {
            boton.disabled = false;
        }
    }

    function feedbackCopia(mensaje) {
        var zona = nodo(ID_COPIA_FEEDBACK);
        if (!zona) {
            return;
        }
        zona.textContent = mensaje;
        if (temporizadorCopia) {
            clearTimeout(temporizadorCopia);
        }
        temporizadorCopia = setTimeout(function () {
            zona.textContent = '';
            temporizadorCopia = null;
        }, MS_COPIA);
    }

    function copiarConFallback() {
        var area = document.createElement('textarea');
        area.value = geojsonActual;
        area.setAttribute('readonly', '');
        area.style.position = 'fixed';
        area.style.top = '-1000px';
        document.body.appendChild(area);

        var copiado = false;
        try {
            area.select();
            copiado = document.execCommand('copy');
        } catch (e) {
            copiado = false;
        }
        if (area.parentNode) {
            area.parentNode.removeChild(area);
        }

        feedbackCopia(copiado ? TEXTO_COPIADO : TEXTO_COPIA_ERROR);
    }

    function copiarGeojson() {
        if (!geojsonActual) {
            return;
        }

        if (window.navigator && navigator.clipboard
            && typeof navigator.clipboard.writeText === 'function') {
            navigator.clipboard.writeText(geojsonActual).then(function () {
                feedbackCopia(TEXTO_COPIADO);
            }).catch(function () {
                copiarConFallback();
            });
            return;
        }

        copiarConFallback();
    }

    // --- Acciones del formulario --------------------------------------------

    function limpiarFiltros() {
        var campos = [ID_UVMZA, ID_UV, ID_MZA];
        for (var i = 0; i < campos.length; i++) {
            var campo = nodo(campos[i]);
            if (campo) {
                campo.value = '';
            }
        }

        limiteActual = LIMITE_INICIAL;
        var comboLimite = nodo(ID_LIMITE);
        if (comboLimite) {
            comboLimite.value = String(LIMITE_INICIAL);
        }

        seleccionActual = null;
        geojsonActual = '';
        detalleVacio(null);
        aviso('', null);
        consultar(PAGINA_INICIAL);
    }

    function manejarCambioLimite() {
        var combo = nodo(ID_LIMITE);
        limiteActual = combo ? aEntero(combo.value, LIMITE_INICIAL) : LIMITE_INICIAL;
        consultar(PAGINA_INICIAL);
    }

    // --- Enlace e inicialización --------------------------------------------

    // Marca por elemento y tipo de evento: la inicialización es idempotente y no
    // duplica listeners si el módulo se vuelve a arrancar.
    function enlazar(id, tipoEvento, manejador) {
        var elemento = nodo(id);
        if (!elemento) {
            return;
        }
        var marca = 'data-mz-' + tipoEvento;
        if (elemento.getAttribute(marca) === '1') {
            return;
        }
        elemento.setAttribute(marca, '1');
        elemento.addEventListener(tipoEvento, manejador);
    }

    function enlazarFormulario() {
        var form = nodo(ID_FORM);
        if (form && form.getAttribute('data-mz-submit') !== '1') {
            form.setAttribute('data-mz-submit', '1');
            form.addEventListener('submit', function (evento) {
                // Enter en los inputs ejecuta la búsqueda por el submit nativo del formulario.
                evento.preventDefault();
                consultar(PAGINA_INICIAL);
            });
        }

        enlazar(ID_LIMPIAR, 'click', limpiarFiltros);
        enlazar(ID_LIMITE, 'change', manejarCambioLimite);
        enlazar(ID_COPIAR, 'click', copiarGeojson);
        enlazar(ID_VER_MAPA, 'click', function () {
            mostrarToast(TEXTO_MAPA);
        });
    }

    // Arranque del módulo: lo invoca Views/Consultas/Manzana.cshtml desde guard.protegerPagina,
    // igual que /Usuarios, /Bitacora y /Consultas/CodigoFijo. La autorización real la resuelve la
    // API con el permiso dinámico sobre la opción de menú /Consultas/Manzana de dbo.RolMenu.
    function iniciar() {
        if (!ns.api || !ns.sesion || !nodo(ID_CUERPO)) {
            return;
        }
        enlazarFormulario();
        consultar(PAGINA_INICIAL);
    }

    ns.consultaManzana = {
        iniciar: iniciar,
        consultar: consultar,
        limpiarFiltros: limpiarFiltros
    };
})(window.VisorSIG);







