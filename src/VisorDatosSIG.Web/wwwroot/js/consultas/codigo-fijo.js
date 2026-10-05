// FE-CU13: consulta de Código Fijo (pantalla /Consultas/CodigoFijo). Solo lectura.
//
// La Web consume exclusivamente la API con el token de la sesión:
//   GET api/codigos-fijos?codFSig&codFijo&nombre&estado&pagina&limite -> página de resultados
//   GET api/codigos-fijos/{idCodigo}                                  -> detalle + geometría GeoJSON
//
// La paginación es del servidor (SQL Server): nunca se descargan todos los registros para
// paginar en el navegador. La tabla muestra solo los campos del DTO resumido real
// (idCodigo, codFSig, codFijo, nombre, estado) y NO dispara una llamada de detalle por fila.
//
// Autenticación: se usa exclusivamente window.VisorSIG.api.peticion(...) con el token de la
// sesión y `protegida: true`. Un 401 se deja al flujo existente (api.js limpia la sesión y
// guard.js redirige); un 403 se informa sin mostrar datos.
//
// Integración con el visor: NO implementada todavía. El botón "Ver en el mapa" solo muestra
// un aviso de "Disponible próximamente"; no se carga Leaflet ni se modifica layers.js.
//
// Seguridad del DOM: createElement + textContent; nada que llegue del backend se interpreta
// como HTML.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var RUTA_LISTA = 'api/codigos-fijos';
    var RUTA_DETALLE = 'api/codigos-fijos/';

    var PAGINA_INICIAL = 1;
    // Ocho filas visibles; la paginación continúa ejecutándose en SQL Server.
    var LIMITE_FIJO = 8;
    var SIN_DATO = '—';
    var SIN_LOTE = 'Sin lote asociado';
    var MS_TOAST = 5000;
    var MS_COPIA = 2500;

    // Estados reales de dbo.CodigosFijos (los describe el backend y los confirma aquí).
    var ESTADOS = {
        1: { texto: 'Normal', clase: 'cf-badge--normal' },
        2: { texto: 'Para Corte', clase: 'cf-badge--para-corte' },
        3: { texto: 'Cortado', clase: 'cf-badge--cortado' },
        4: { texto: 'Baja Parcial', clase: 'cf-badge--baja-parcial' },
        5: { texto: 'Baja Total', clase: 'cf-badge--baja-total' }
    };
    var ESTADO_DESCONOCIDO = { texto: 'Desconocido', clase: 'cf-badge--desconocido' };

    var ID_FORM = 'cf-filtros';
    var ID_CODFIJO = 'cf-codfijo';
    var ID_NOMBRE = 'cf-nombre';
    var ID_CODFSIG = 'cf-codfsig';
    var ID_ESTADO = 'cf-estado';
    var ID_BUSCAR = 'cf-buscar';
    var ID_LIMPIAR = 'cf-limpiar';
    var ID_CUERPO = 'cf-cuerpo';
    var ID_TABLA = 'cf-tabla-envoltura';
    var ID_VACIO = 'cf-vacio';
    var ID_TITULO = 'cf-titulo';
    var ID_CONTEO = 'cf-conteo';
    var ID_PAGINACION = 'cf-paginacion';
    var ID_CARGANDO = 'cf-cargando';
    var ID_AVISO = 'cf-aviso';
    var ID_TOAST = 'cf-toast';

    var ID_DET_VACIO = 'cf-det-vacio';
    var ID_DET_CARGANDO = 'cf-det-cargando';
    var ID_DET_CONTENIDO = 'cf-det-contenido';
    var ID_DET_BADGE = 'cf-det-badge';
    var ID_DET_ID = 'cf-det-id';
    var ID_DET_CODFSQL = 'cf-det-codfsql';
    var ID_DET_CODFSIG = 'cf-det-codfsig';
    var ID_DET_CODFIJO = 'cf-det-codfijo';
    var ID_DET_NOMBRE = 'cf-det-nombre';
    var ID_DET_ESTADO = 'cf-det-estado';
    var ID_DET_FECHA = 'cf-det-fecha';
    var ID_DET_LOTE = 'cf-det-lote';
    var ID_DET_COORDS = 'cf-det-coords';
    var ID_DET_LONGITUD = 'cf-det-longitud';
    var ID_DET_LATITUD = 'cf-det-latitud';
    var ID_DET_SIN_COORDS = 'cf-det-sin-coords';
    var ID_VER_MAPA = 'cf-ver-mapa';
    var ID_GEOJSON = 'cf-geojson';
    var ID_COPIAR = 'cf-copiar';
    var ID_COPIA_FEEDBACK = 'cf-copiar-feedback';

    var TEXTO_VACIO_DETALLE = 'Seleccione un registro para visualizar su detalle.';
    var TEXTO_ERROR_LISTA = 'No fue posible realizar la consulta.';
    var TEXTO_ERROR_DETALLE = 'No fue posible cargar el detalle del Código Fijo.';
    var TEXTO_SIN_PERMISO = 'No tiene permisos para consultar códigos fijos.';
    var TEXTO_CODFIJO_INVALIDO = 'El código fijo debe ser un número entero.';
    var TEXTO_SIN_GEOMETRIA = 'Sin geometría disponible';
    var TEXTO_COPIADO = 'GeoJSON copiado';
    var TEXTO_COPIA_ERROR = 'No se pudo copiar el GeoJSON';
    var TEXTO_MAPA = 'Disponible próximamente';
    var TEXTO_MAPA_DETALLE = 'La integración con el visor cartográfico estará disponible próximamente.';

    // Estado del módulo.
    var paginaActual = PAGINA_INICIAL;
    var limiteActual = LIMITE_FIJO;
    var totalRegistros = 0;
    var totalPaginas = 0;
    var seleccionActual = null;
    var geojsonActual = '';
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

    // Todo campo vacío del backend se muestra con el guion largo, nunca como "null".
    function mostrable(comprobado) {
        var limpio = textoComprobado(comprobado);
        return limpio === '' ? SIN_DATO : limpio;
    }

    function numeroMostrable(comprobado) {
        if (comprobado === null || comprobado === undefined || comprobado === '') {
            return SIN_DATO;
        }
        return String(comprobado);
    }

    function aEntero(comprobado, alterno) {
        var numero = Number(comprobado);
        return isFinite(numero) ? Math.floor(numero) : alterno;
    }

    function aNumero(comprobado) {
        if (comprobado === null || comprobado === undefined || comprobado === '') {
            return null;
        }
        var numero = Number(comprobado);
        return isFinite(numero) ? numero : null;
    }

    function formatearCantidad(comprobado) {
        var numero = aEntero(comprobado, 0);
        try {
            return new Intl.NumberFormat('es-BO').format(numero);
        } catch (e) {
            return String(numero);
        }
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
        zona.className = tono === 'error'
            ? 'visor-aviso cf-aviso cf-aviso--error'
            : 'visor-aviso cf-aviso cf-aviso--info';
        zona.textContent = mensaje || '';
        zona.hidden = !mensaje;
    }

    function ponerTexto(id, texto) {
        var elemento = nodo(id);
        if (elemento) {
            elemento.textContent = texto;
        }
    }

    // La fecha se formatea con el helper central del workspace (js/comun/fechas.js):
    // la API entrega DateTime sin zona y se conserva su semántica.
    function fechaMostrable(comprobado) {
        if (ns.fechas && typeof ns.fechas.formatearFechaHora === 'function') {
            var formateada = ns.fechas.formatearFechaHora(comprobado);
            if (formateada && formateada !== SIN_DATO) {
                return formateada;
            }
        }
        return SIN_DATO;
    }

    // --- Estados y badges ---------------------------------------------------

    function datosEstado(estado, descripcion) {
        var info = ESTADOS[estado] || ESTADO_DESCONOCIDO;
        var nombre = textoComprobado(descripcion);
        return {
            texto: nombre !== '' ? nombre : info.texto,
            clase: info.clase
        };
    }

    function pintarBadge(elemento, estado, descripcion) {
        if (!elemento) {
            return;
        }
        var info = datosEstado(estado, descripcion);
        elemento.className = 'cf-badge ' + info.clase;
        elemento.textContent = info.texto;
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
        for (var i = 0; i < LIMITE_FIJO; i++) {
            var fila = crear('tr', 'cf-fila cf-fila--esqueleto', null);
            for (var j = 0; j < 6; j++) {
                fila.appendChild(crear('td', null, null));
            }
            cuerpo.appendChild(fila);
        }
    }

    function limpiarResultados() {
        vaciar(nodo(ID_CUERPO));
    }

    function actualizarTitulo() {
        var titulo = nodo(ID_TITULO);
        if (titulo) {
            var texto = titulo.querySelector('span:last-child');
            (texto || titulo).textContent = 'Resultados (' + formatearCantidad(totalRegistros) + ' registros)';
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
        zona.textContent = 'Mostrando ' + formatearCantidad(desde) + ' a '
            + formatearCantidad(hasta) + ' de ' + formatearCantidad(totalRegistros) + ' registros';
    }

    function ocultarPaginacion() {
        var contenedor = nodo(ID_PAGINACION);
        if (contenedor) {
            vaciar(contenedor);
            contenedor.hidden = true;
        }
    }

    // 403 de la API: la autorización es del servidor y no se muestra ningún dato.
    function bloquearPantalla() {
        var form = nodo(ID_FORM);
        var grid = document.querySelector('.cf-grid');
        if (form) {
            form.hidden = true;
        }
        if (grid) {
            grid.hidden = true;
        }
        limpiarResultados();
        ocultarPaginacion();
        detalleVacio(null);
        ocultarToast();
        aviso(TEXTO_SIN_PERMISO, 'error');
    }

    // --- Filtros y consulta -------------------------------------------------

    function valorCampo(id) {
        var campo = nodo(id);
        return campo ? String(campo.value).trim() : '';
    }

    function valorCombo(id) {
        var combo = nodo(id);
        return combo ? String(combo.value).trim() : '';
    }

    // Solo se envían los filtros que el backend acepta: codFSig, codFijo, nombre, estado.
    function construirParametros(pagina) {
        var partes = [];

        function agregar(nombre, parametro) {
            if (parametro === '' || parametro === null || parametro === undefined) {
                return;
            }
            partes.push(nombre + '=' + encodeURIComponent(String(parametro)));
        }

        agregar('pagina', String(pagina));
        agregar('limite', String(LIMITE_FIJO));
        agregar('codFSig', valorCampo(ID_CODFSIG));
        agregar('nombre', valorCampo(ID_NOMBRE));
        agregar('codFijo', valorCampo(ID_CODFIJO));
        agregar('estado', valorCombo(ID_ESTADO));

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
    function consultar(pagina, esBusqueda) {
        aviso('', null);

        var codFijo = valorCampo(ID_CODFIJO);
        if (codFijo !== '' && !/^\d+$/.test(codFijo)) {
            aviso(TEXTO_CODFIJO_INVALIDO, 'error');
            return;
        }

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
                aviso(mensajeError(resultado, TEXTO_ERROR_LISTA), 'error');
                return;
            }

            var paginaDto = resultado.datos;
            var registros = valor(paginaDto, 'datos');
            registros = esArreglo(registros) ? registros : [];

            // El total y el tamaño los informa el servidor; nunca se cuentan en el cliente.
            paginaActual = aEntero(valor(paginaDto, 'pagina'), paginaActual);
            limiteActual = LIMITE_FIJO;
            totalRegistros = aEntero(valor(paginaDto, 'totalRegistros'), 0);
            totalPaginas = aEntero(valor(paginaDto, 'totalPaginas'), 0);
            if (totalPaginas < 1 && totalRegistros > 0) {
                totalPaginas = Math.ceil(totalRegistros / limiteActual);
            }

            if (registros.length === 0) {
                limpiarResultados();
                actualizarTitulo();
                mostrarVacio(true);
                actualizarConteo(0);
                ocultarPaginacion();
                return;
            }

            mostrarVacio(false);
            renderizarFilas(registros);
            actualizarTitulo();
            actualizarConteo(registros.length);
            renderizarPaginacion();

            // El toast se muestra solo en búsquedas del usuario y con resultados.
            if (esBusqueda) {
                mostrarToast('Consulta realizada correctamente',
                    'Se ' + (totalRegistros === 1
                        ? 'encontró 1 registro'
                        : 'encontraron ' + formatearCantidad(totalRegistros) + ' registros')
                    + ' de código fijo.');
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
        for (var i = 0; i < registros.length; i++) {
            cuerpo.appendChild(crearFila(registros[i]));
        }
        marcarSeleccion();
    }

    // La fila consume el DTO resumido real: no pide el detalle de cada registro.
    function crearFila(fila) {
        var idCodigo = aEntero(valor(fila, 'idCodigo'), 0);
        var tr = crear('tr', 'cf-fila', null);
        tr.setAttribute('data-id', String(idCodigo));
        tr.tabIndex = 0;
        tr.setAttribute('aria-label', 'Seleccionar código fijo ' + idCodigo);

        tr.appendChild(crear('td', null, idCodigo > 0 ? String(idCodigo) : SIN_DATO));
        tr.appendChild(crear('td', null, mostrable(valor(fila, 'codFSig'))));
        tr.appendChild(crear('td', null, numeroMostrable(valor(fila, 'codFijo'))));
        var nombre = mostrable(valor(fila, 'nombre'));
        var celdaNombre = crear('td', 'cf-celda--nombre', nombre);
        celdaNombre.title = nombre;
        tr.appendChild(celdaNombre);

        var celdaEstado = crear('td', 'cf-celda--estado', null);
        var badge = crear('span', 'cf-badge', null);
        pintarBadge(badge, aEntero(valor(fila, 'estado'), 0), valor(fila, 'estadoDescripcion'));
        celdaEstado.appendChild(badge);
        tr.appendChild(celdaEstado);

        var celdaAccion = crear('td', 'cf-celda--accion', null);
        var boton = crear('button', 'btn-secundario cf-ver', 'Ver');
        boton.type = 'button';
        boton.title = 'Ver detalle';
        boton.setAttribute('aria-label', 'Ver el detalle del código fijo ' + idCodigo);
        boton.addEventListener('click', function (evento) {
            evento.stopPropagation();
            seleccionar(idCodigo);
        });
        celdaAccion.appendChild(boton);
        tr.appendChild(celdaAccion);

        tr.addEventListener('click', function () {
            seleccionar(idCodigo);
        });
        tr.addEventListener('keydown', function (evento) {
            if (evento.target === tr && (evento.key === 'Enter' || evento.key === ' ')) {
                evento.preventDefault();
                seleccionar(idCodigo);
            }
        });

        return tr;
    }

    function marcarSeleccion() {
        var cuerpo = nodo(ID_CUERPO);
        if (!cuerpo) {
            return;
        }
        var filas = cuerpo.querySelectorAll('.cf-fila');
        for (var i = 0; i < filas.length; i++) {
            var activa = filas[i].getAttribute('data-id') === String(seleccionActual);
            if (activa) {
                filas[i].classList.add('cf-fila--activa');
                filas[i].setAttribute('aria-selected', 'true');
            } else {
                filas[i].classList.remove('cf-fila--activa');
                filas[i].removeAttribute('aria-selected');
            }
        }
    }

    // --- Paginación del servidor --------------------------------------------

    function botonPagina(etiqueta, destino, titulo, deshabilitado, activo) {
        var boton = crear('button',
            'cf-paginacion__boton' + (activo ? ' cf-paginacion__boton--activo' : ''),
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
            consultar(destino, false);
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

    function seleccionar(idCodigo) {
        if (!(idCodigo > 0)) {
            return;
        }
        seleccionActual = idCodigo;
        marcarSeleccion();
        cargarDetalle(idCodigo);
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
        var badgeCabecera = nodo(ID_DET_BADGE);

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
        if (badgeCabecera) {
            badgeCabecera.hidden = true;
        }
    }

    function cargarDetalle(idCodigo) {
        secuenciaDetalle += 1;
        var actual = secuenciaDetalle;

        detalleCargando();

        llamar(RUTA_DETALLE + idCodigo, null).then(function (resultado) {
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

        var estado = aEntero(valor(dto, 'estado'), 0);
        var descripcion = valor(dto, 'estadoDescripcion');

        ponerTexto(ID_DET_ID, numeroMostrable(valor(dto, 'idCodigo')));
        ponerTexto(ID_DET_CODFSQL, numeroMostrable(valor(dto, 'codFSql')));
        ponerTexto(ID_DET_CODFSIG, mostrable(valor(dto, 'codFSig')));
        ponerTexto(ID_DET_CODFIJO, numeroMostrable(valor(dto, 'codFijo')));
        ponerTexto(ID_DET_NOMBRE, mostrable(valor(dto, 'nombre')));
        ponerTexto(ID_DET_FECHA, fechaMostrable(valor(dto, 'fechaCambioEstado')));

        var lote = valor(dto, 'idLote');
        ponerTexto(ID_DET_LOTE, (lote === null || lote === undefined) ? SIN_LOTE : String(lote));

        pintarBadge(nodo(ID_DET_ESTADO), estado, descripcion);

        var badgeCabecera = nodo(ID_DET_BADGE);
        if (badgeCabecera) {
            pintarBadge(badgeCabecera, estado, descripcion);
            badgeCabecera.hidden = false;
        }

        pintarCoordenadas(valor(dto, 'longitud'), valor(dto, 'latitud'));
        pintarGeojson(valor(dto, 'geometria'));
    }

    // Coordenadas reales del registro: los NULL se muestran con el guion largo y, si no
    // hay ninguna, se informa que el registro no dispone de información espacial.
    function pintarCoordenadas(longitud, latitud) {
        var lng = aNumero(longitud);
        var lat = aNumero(latitud);
        var hayAlguna = lng !== null || lat !== null;

        var coords = nodo(ID_DET_COORDS);
        var sinCoords = nodo(ID_DET_SIN_COORDS);
        if (coords) {
            coords.hidden = !hayAlguna;
        }
        if (sinCoords) {
            sinCoords.hidden = hayAlguna;
        }

        ponerTexto(ID_DET_LONGITUD, lng === null ? SIN_DATO : String(lng));
        ponerTexto(ID_DET_LATITUD, lat === null ? SIN_DATO : String(lat));
    }

    // GeoJSON tal como lo devuelve el backend (objeto con type y coordinates). NO se
    // invierten las coordenadas: para un Point, coordinates[0] = longitud y [1] = latitud.
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

    function pintarGeojson(geometria) {
        var bloque = nodo(ID_GEOJSON);
        var boton = nodo(ID_COPIAR);
        var formateado = formatearGeojson(geometria);

        if (formateado === null) {
            geojsonActual = '';
            if (bloque) {
                bloque.textContent = TEXTO_SIN_GEOMETRIA;
            }
            if (boton) {
                boton.disabled = true;
            }
            return;
        }

        geojsonActual = formateado;
        if (bloque) {
            bloque.textContent = formateado;
        }
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

    // --- Toast de éxito -----------------------------------------------------

    function mostrarToast(titulo, detalle) {
        var toast = nodo(ID_TOAST);
        if (!toast) {
            return;
        }
        vaciar(toast);

        var icono = crear('span', 'cf-toast__icono', null);
        icono.setAttribute('aria-hidden', 'true');

        var cuerpo = crear('div', 'cf-toast__cuerpo', null);
        cuerpo.appendChild(crear('p', 'cf-toast__titulo', titulo));
        if (detalle) {
            cuerpo.appendChild(crear('p', 'cf-toast__texto', detalle));
        }

        var cerrar = crear('button', 'cf-toast__cerrar', '\u00d7');
        cerrar.type = 'button';
        cerrar.setAttribute('aria-label', 'Cerrar aviso');
        cerrar.addEventListener('click', ocultarToast);

        toast.appendChild(icono);
        toast.appendChild(cuerpo);
        toast.appendChild(cerrar);
        toast.hidden = false;

        // Reinicia la animación de entrada cuando se reutiliza el mismo contenedor.
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

    // --- Acciones del formulario --------------------------------------------

    function limpiarFiltros() {
        var campos = [ID_CODFIJO, ID_NOMBRE, ID_CODFSIG];
        for (var i = 0; i < campos.length; i++) {
            var campo = nodo(campos[i]);
            if (campo) {
                campo.value = '';
            }
        }

        var estado = nodo(ID_ESTADO);
        if (estado) {
            estado.value = '';
        }

        limiteActual = LIMITE_FIJO;

        seleccionActual = null;
        detalleVacio(null);
        aviso('', null);
        consultar(PAGINA_INICIAL, true);
    }

    // --- Enlace e inicialización --------------------------------------------

    // Marca por elemento y tipo de evento: la inicialización es idempotente y no
    // duplica listeners si el módulo se vuelve a arrancar.
    function enlazar(id, tipoEvento, manejador) {
        var elemento = nodo(id);
        if (!elemento) {
            return;
        }
        var marca = 'data-cf-' + tipoEvento;
        if (elemento.getAttribute(marca) === '1') {
            return;
        }
        elemento.setAttribute(marca, '1');
        elemento.addEventListener(tipoEvento, manejador);
    }

    function enlazarFormulario() {
        var form = nodo(ID_FORM);
        if (form && form.getAttribute('data-cf-submit') !== '1') {
            form.setAttribute('data-cf-submit', '1');
            form.addEventListener('submit', function (evento) {
                // Enter en los inputs ejecuta la búsqueda por el submit nativo del formulario.
                evento.preventDefault();
                consultar(PAGINA_INICIAL, true);
            });
        }

        enlazar(ID_LIMPIAR, 'click', limpiarFiltros);
        enlazar(ID_VER_MAPA, 'click', function () {
            mostrarToast(TEXTO_MAPA, TEXTO_MAPA_DETALLE);
        });
        enlazar(ID_COPIAR, 'click', copiarGeojson);
    }

    // Arranque del módulo: lo invoca Views/Consultas/CodigoFijo.cshtml desde guard.protegerPagina,
    // igual que /Usuarios y /Bitacora. La autorización real la resuelve la API con el permiso
    // dinámico sobre la opción de menú /Consultas/CodigoFijo de dbo.RolMenu.
    function iniciar() {
        if (!ns.api || !ns.sesion || !nodo(ID_CUERPO)) {
            return;
        }
        enlazarFormulario();
        consultar(PAGINA_INICIAL, false);
    }

    ns.consultaCodigoFijo = {
        iniciar: iniciar,
        consultar: consultar,
        limpiarFiltros: limpiarFiltros
    };
})(window.VisorSIG);






