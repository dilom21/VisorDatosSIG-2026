// FE-CU05: consulta de la bitácora del sistema (pantalla /Bitacora). Solo lectura.
//
// La Web consume exclusivamente la API con el token de la sesión:
//   GET api/bitacora            -> página de eventos (filtros y paginación en SQL Server)
//   GET api/bitacora/catalogos  -> valores reales de los filtros (módulos, acciones, ...)
//   GET api/bitacora/{id}       -> detalle del evento
//
// No se descarga el historial completo ni se pagina en el navegador: se piden la página, el
// tamaño de página y los filtros, y la API devuelve el total para el conteo. Los combos se
// llenan con lo que devuelve el servidor, que ya excluye el módulo del migrador: si algún valor
// apareciera, sería un error del backend y no se oculta en el cliente.
//
// Seguridad del DOM: createElement + textContent; el detalle de bitácora nunca se interpreta
// como HTML.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var RUTA_BITACORA = 'api/bitacora';
    var RUTA_CATALOGOS = 'api/bitacora/catalogos';

    var PAGINA_INICIAL = 1;
    var TAMANO_INICIAL = 25;
    var SIN_DATO = '—';

    var ID_FORM = 'form-bitacora';
    var ID_BUSCAR = 'bitacora-buscar';
    var ID_USUARIO = 'bitacora-usuario';
    var ID_MODULO = 'bitacora-modulo';
    var ID_ACCION = 'bitacora-accion';
    var ID_ENTIDAD = 'bitacora-entidad';
    var ID_RESULTADO = 'bitacora-resultado';
    var ID_DESDE = 'bitacora-desde';
    var ID_HASTA = 'bitacora-hasta';
    var ID_TAMANO = 'bitacora-tamano';
    var ID_CUERPO = 'bitacora-cuerpo';
    var ID_ESTADO = 'bitacora-estado';
    var ID_CONTEO = 'bitacora-conteo';
    var ID_PAGINACION = 'bitacora-paginacion';
    var ID_AVISO = 'bitacora-aviso';
    var ID_BOTON_LIMPIAR = 'btn-bitacora-limpiar';

    var TEXTO_CARGANDO = 'Consultando la bitácora...';
    var TEXTO_ERROR = 'No se pudo consultar la bitácora.';
    var TEXTO_VACIO = 'No hay eventos que cumplan los filtros.';
    var TEXTO_RANGO = "La fecha 'desde' no puede ser posterior a la fecha 'hasta'.";
    var TEXTO_BLOQUEO = 'No tiene permisos para acceder a esta funcionalidad.';
    var TEXTO_CATALOGO_ERROR = 'No se pudieron cargar los valores de los filtros.';

    // Estado del módulo.
    var paginaActual = PAGINA_INICIAL;
    var tamanoActual = TAMANO_INICIAL;
    var totalRegistros = 0;
    var totalPaginas = 0;
    // Protege contra respuestas obsoletas cuando se encadenan consultas.
    var secuencia = 0;

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

    function esArreglo(valorComprobado) {
        return Object.prototype.toString.call(valorComprobado) === '[object Array]';
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

    function textoComprobado(valorComprobado) {
        return typeof valorComprobado === 'string' ? valorComprobado.trim() : '';
    }

    // Todo campo vacío del backend se muestra con el guion largo, nunca como "null".
    function mostrable(valorComprobado) {
        var limpio = textoComprobado(valorComprobado);
        return limpio === '' ? SIN_DATO : limpio;
    }

    function aEntero(valorComprobado, alterno) {
        var numero = Number(valorComprobado);
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

        zona.className = tono === 'error'
            ? 'visor-aviso bitacora__aviso bitacora__aviso--error'
            : 'visor-aviso bitacora__aviso bitacora__aviso--info';
        zona.textContent = mensaje || '';
        zona.hidden = !mensaje;
    }

    function estado(mensaje, tono) {
        var zona = nodo(ID_ESTADO);
        if (!zona) {
            return;
        }

        zona.textContent = mensaje || '';
        zona.className = tono ? 'bitacora__estado bitacora__estado--' + tono : 'bitacora__estado';
    }

    // 403 de la API: la autorización es del servidor y no se muestra ningún dato.
    function mostrarBloqueo() {
        var tabla = document.querySelector('.bitacora__tabla-envoltura');
        var form = nodo(ID_FORM);
        var pie = document.querySelector('.bitacora__pie');

        if (tabla) {
            tabla.hidden = true;
        }
        if (form) {
            form.hidden = true;
        }
        if (pie) {
            pie.hidden = true;
        }

        vaciar(nodo(ID_CUERPO));
        estado('');
        aviso(TEXTO_BLOQUEO, 'error');
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

    // --- Catálogos de los filtros (GET api/bitacora/catalogos) ---------------

    function opcionVacia(etiqueta) {
        var opcion = document.createElement('option');
        opcion.value = '';
        opcion.textContent = etiqueta;
        return opcion;
    }

    // Llena un combo con los valores del servidor conservando la selección vigente.
    function llenarCombo(id, opciones, etiquetaVacia) {
        var combo = nodo(id);
        if (!combo) {
            return;
        }

        var previo = combo.value;
        vaciar(combo);
        combo.appendChild(opcionVacia(etiquetaVacia));

        for (var i = 0; i < opciones.length; i++) {
            var opcion = document.createElement('option');
            opcion.value = opciones[i].valor;
            opcion.textContent = opciones[i].etiqueta;
            combo.appendChild(opcion);
        }

        // Un valor que ya no existe deja el combo en "todos", sin inventar opciones.
        combo.value = previo;
    }

    function listaDeTextos(arreglo) {
        var opciones = [];
        if (!esArreglo(arreglo)) {
            return opciones;
        }

        for (var i = 0; i < arreglo.length; i++) {
            var item = textoComprobado(arreglo[i]);
            if (item !== '') {
                opciones.push({ valor: item, etiqueta: item });
            }
        }

        return opciones;
    }

    function listaDeUsuarios(arreglo) {
        var opciones = [];
        if (!esArreglo(arreglo)) {
            return opciones;
        }

        for (var i = 0; i < arreglo.length; i++) {
            var idUsuario = aEntero(valor(arreglo[i], 'idUsuario'), 0);
            if (idUsuario <= 0) {
                continue;
            }

            var nombre = textoComprobado(valor(arreglo[i], 'nombre'));
            var login = textoComprobado(valor(arreglo[i], 'login'));
            var etiqueta;

            if (nombre !== '' && login !== '') {
                etiqueta = nombre + ' (' + login + ')';
            } else if (nombre !== '') {
                etiqueta = nombre;
            } else if (login !== '') {
                etiqueta = login;
            } else {
                etiqueta = '#' + idUsuario;
            }

            opciones.push({ valor: String(idUsuario), etiqueta: etiqueta });
        }

        return opciones;
    }

    function cargarCatalogos() {
        return llamar(RUTA_CATALOGOS, null).then(function (resultado) {
            if (resultado.status === 401) {
                return;
            }

            if (resultado.status === 403) {
                mostrarBloqueo();
                return;
            }

            if (!resultado.ok || !resultado.datos) {
                aviso(mensajeError(resultado, TEXTO_CATALOGO_ERROR), 'error');
                return;
            }

            var catalogos = resultado.datos;

            llenarCombo(ID_MODULO, listaDeTextos(valor(catalogos, 'modulos')), 'Todos los módulos');
            llenarCombo(ID_ACCION, listaDeTextos(valor(catalogos, 'acciones')), 'Todas las acciones');
            llenarCombo(ID_ENTIDAD, listaDeTextos(valor(catalogos, 'entidades')), 'Todas las entidades');
            llenarCombo(ID_RESULTADO, listaDeTextos(valor(catalogos, 'resultados')), 'Todos los resultados');
            llenarCombo(ID_USUARIO, listaDeUsuarios(valor(catalogos, 'usuarios')), 'Todos los usuarios');
        });
    }

    // --- Filtros y consulta -------------------------------------------------

    function valorCombo(id) {
        var combo = nodo(id);
        return combo ? String(combo.value).trim() : '';
    }

    function valorCampo(id) {
        var campo = nodo(id);
        return campo ? String(campo.value).trim() : '';
    }

    // Las fechas del control datetime-local usan el formato ISO local: la comparación
    // lexicográfica equivale a la cronológica.
    function rangoInvalido() {
        var desde = valorCampo(ID_DESDE);
        var hasta = valorCampo(ID_HASTA);
        return desde !== '' && hasta !== '' && desde > hasta;
    }

    function conSegundos(valorFecha) {
        return valorFecha.length === 16 ? valorFecha + ':00' : valorFecha;
    }

    function construirParametros(pagina) {
        var partes = [];

        function agregar(nombre, parametro) {
            if (parametro === '' || parametro === null || parametro === undefined) {
                return;
            }
            partes.push(nombre + '=' + encodeURIComponent(String(parametro)));
        }

        agregar('pagina', String(pagina));
        agregar('tamano', String(tamanoActual));
        agregar('buscar', valorCampo(ID_BUSCAR));
        agregar('idUsuario', valorCombo(ID_USUARIO));
        agregar('modulo', valorCombo(ID_MODULO));
        agregar('accion', valorCombo(ID_ACCION));
        agregar('entidad', valorCombo(ID_ENTIDAD));
        agregar('resultado', valorCombo(ID_RESULTADO));
        // Las fechas viajan tal como las escribió el usuario, sin conversión de zona.
        agregar('fechaDesde', conSegundos(valorCampo(ID_DESDE)));
        agregar('fechaHasta', conSegundos(valorCampo(ID_HASTA)));

        return partes.join('&');
    }

    function limpiarTabla() {
        vaciar(nodo(ID_CUERPO));
        var conteo = nodo(ID_CONTEO);
        if (conteo) {
            conteo.textContent = '';
        }
    }

    function ocultarPaginacion() {
        var contenedor = nodo(ID_PAGINACION);
        if (contenedor) {
            vaciar(contenedor);
            contenedor.hidden = true;
        }
    }

    // Página pedida al servidor: el cliente nunca pagina ni filtra localmente.
    function consultar(pagina) {
        if (rangoInvalido()) {
            // Validación previa: no se consulta con un rango invertido.
            aviso(TEXTO_RANGO, 'error');
            return;
        }

        aviso('', null);

        var pedida = aEntero(pagina, PAGINA_INICIAL);
        paginaActual = pedida >= 1 ? pedida : PAGINA_INICIAL;

        secuencia += 1;
        var actual = secuencia;

        estado(TEXTO_CARGANDO, 'cargando');

        llamar(RUTA_BITACORA, construirParametros(paginaActual)).then(function (resultado) {
            if (actual !== secuencia) {
                // Una consulta más reciente ya salió: se descarta esta respuesta.
                return;
            }

            if (resultado.status === 401) {
                return;
            }

            if (resultado.status === 403) {
                mostrarBloqueo();
                return;
            }

            if (!resultado.ok || !resultado.datos) {
                limpiarTabla();
                ocultarPaginacion();
                estado('');
                aviso(mensajeError(resultado, TEXTO_ERROR), 'error');
                return;
            }

            var paginaDto = resultado.datos;
            var registros = valor(paginaDto, 'datos');
            registros = esArreglo(registros) ? registros : [];

            // El total y el tamaño los informa el servidor; nunca se cuentan en el cliente.
            paginaActual = aEntero(valor(paginaDto, 'pagina'), paginaActual);
            tamanoActual = aEntero(valor(paginaDto, 'tamano'), tamanoActual);
            totalRegistros = aEntero(valor(paginaDto, 'totalRegistros'), 0);
            totalPaginas = aEntero(valor(paginaDto, 'totalPaginas'), 0);

            renderizarFilas(registros);
            actualizarConteo(registros.length);
            renderizarPaginacion();

            estado(registros.length === 0 ? TEXTO_VACIO : '',
                registros.length === 0 ? 'vacio' : null);
        });
    }

    // --- Render de la tabla -------------------------------------------------

    function usuarioLegible(evento) {
        var nombre = textoComprobado(valor(evento, 'nombreUsuario'));
        if (nombre !== '') {
            return nombre;
        }

        var login = textoComprobado(valor(evento, 'loginUsuario'));
        return login !== '' ? login : 'Sistema';
    }

    function crearCelda(modificador, contenido) {
        return crear('td', 'bitacora__celda bitacora__celda--' + modificador, contenido);
    }

    function renderizarFilas(registros) {
        var cuerpo = nodo(ID_CUERPO);
        if (!cuerpo) {
            return;
        }

        vaciar(cuerpo);

        for (var i = 0; i < registros.length; i++) {
            cuerpo.appendChild(crearFila(registros[i]));
        }
    }

    function crearFila(evento) {
        var tr = crear('tr', 'bitacora__fila', null);
        var idBitacora = aEntero(valor(evento, 'idBitacora'), 0);

        var fecha = ns.fechas
            ? ns.fechas.formatearFechaHora(valor(evento, 'fechaHora'))
            : mostrable(valor(evento, 'fechaHora'));

        tr.appendChild(crearCelda('fecha', fecha));
        tr.appendChild(crearCelda('usuario', usuarioLegible(evento)));
        tr.appendChild(crearCelda('modulo', mostrable(valor(evento, 'modulo'))));
        tr.appendChild(crearCelda('accion', mostrable(valor(evento, 'accion'))));
        tr.appendChild(crearCelda('entidad', mostrable(valor(evento, 'entidad'))));
        tr.appendChild(crearCelda('resultado', mostrable(valor(evento, 'resultado'))));

        // El detalle se recorta con CSS: el título permite leerlo completo al pasar el mouse.
        var detalle = mostrable(valor(evento, 'detalle'));
        var celdaDetalle = crearCelda('detalle', detalle);
        celdaDetalle.setAttribute('title', detalle);
        tr.appendChild(celdaDetalle);

        var celdaAccion = crear('td', 'bitacora__celda bitacora__celda--accion', null);

        var boton = crear('button', 'btn-secundario bitacora__ver', 'Ver');
        boton.type = 'button';
        boton.setAttribute('aria-label', 'Ver el detalle del evento ' + idBitacora);
        boton.addEventListener('click', function () {
            abrirDetalle(idBitacora);
        });

        celdaAccion.appendChild(boton);
        tr.appendChild(celdaAccion);

        return tr;
    }

    function actualizarConteo(filas) {
        var zona = nodo(ID_CONTEO);
        if (!zona) {
            return;
        }

        if (totalRegistros === 0 || filas === 0) {
            zona.textContent = 'Sin registros para mostrar.';
            return;
        }

        // El rango se calcula con el total y el tamaño informados por el servidor.
        var desde = (paginaActual - 1) * tamanoActual + 1;
        var hasta = desde + filas - 1;

        zona.textContent = 'Mostrando ' + desde + '–' + hasta + ' de ' + totalRegistros
            + ' registros.';
    }

    function renderizarPaginacion() {
        var contenedor = nodo(ID_PAGINACION);
        if (!contenedor) {
            return;
        }

        vaciar(contenedor);

        if (!(totalPaginas > 1)) {
            contenedor.hidden = true;
            return;
        }

        contenedor.hidden = false;

        var anterior = crear('button', 'btn-secundario bitacora__pagina', 'Anterior');
        anterior.type = 'button';
        anterior.disabled = paginaActual <= 1;
        anterior.addEventListener('click', function () {
            consultar(paginaActual - 1);
        });

        var etiqueta = crear('span', 'bitacora__pagina-estado',
            'Página ' + paginaActual + ' de ' + totalPaginas);

        var siguiente = crear('button', 'btn-secundario bitacora__pagina', 'Siguiente');
        siguiente.type = 'button';
        siguiente.disabled = paginaActual >= totalPaginas;
        siguiente.addEventListener('click', function () {
            consultar(paginaActual + 1);
        });

        contenedor.appendChild(anterior);
        contenedor.appendChild(etiqueta);
        contenedor.appendChild(siguiente);
    }

    // --- Detalle de un evento (GET api/bitacora/{id}) -----------------------

    function mostrarDetalle(evento) {
        var fecha = ns.fechas
            ? ns.fechas.formatearFechaHora(valor(evento, 'fechaHora'))
            : mostrable(valor(evento, 'fechaHora'));

        var idEntidad = valor(evento, 'idEntidad');

        ns.modal.detalle({
            titulo: 'Detalle del evento',
            subtitulo: 'Evento #' + aEntero(valor(evento, 'idBitacora'), 0),
            filas: [
                { etiqueta: 'Fecha y hora', valor: fecha },
                { etiqueta: 'Usuario', valor: usuarioLegible(evento) },
                { etiqueta: 'Módulo', valor: mostrable(valor(evento, 'modulo')) },
                { etiqueta: 'Acción', valor: mostrable(valor(evento, 'accion')) },
                { etiqueta: 'Entidad', valor: mostrable(valor(evento, 'entidad')) },
                {
                    etiqueta: 'Id de entidad',
                    valor: idEntidad === null || idEntidad === undefined
                        ? SIN_DATO
                        : String(idEntidad)
                },
                { etiqueta: 'Resultado', valor: mostrable(valor(evento, 'resultado')) },
                { etiqueta: 'Detalle', valor: mostrable(valor(evento, 'detalle')) },
                { etiqueta: 'IP de origen', valor: mostrable(valor(evento, 'ip')) }
            ]
        });
    }

    // El detalle se pide al servidor: la fila no se reutiliza como fuente de verdad.
    function abrirDetalle(idBitacora) {
        if (!(idBitacora > 0) || !ns.modal) {
            return;
        }

        estado('Cargando el detalle del evento...', 'cargando');

        llamar(RUTA_BITACORA + '/' + idBitacora, null).then(function (resultado) {
            if (resultado.status === 401) {
                return;
            }

            estado('', null);

            if (resultado.status === 403) {
                mostrarBloqueo();
                return;
            }

            if (!resultado.ok || !resultado.datos) {
                aviso(mensajeError(resultado, 'No se pudo obtener el detalle del evento.'), 'error');
                return;
            }

            mostrarDetalle(resultado.datos);
        });
    }

    // --- Acciones del formulario --------------------------------------------

    function limpiarFiltros() {
        var campos = [ID_BUSCAR, ID_USUARIO, ID_MODULO, ID_ACCION, ID_ENTIDAD, ID_RESULTADO,
            ID_DESDE, ID_HASTA];

        for (var i = 0; i < campos.length; i++) {
            var campo = nodo(campos[i]);
            if (campo) {
                campo.value = '';
            }
        }

        tamanoActual = TAMANO_INICIAL;
        var combo = nodo(ID_TAMANO);
        if (combo) {
            combo.value = String(TAMANO_INICIAL);
        }

        aviso('', null);
        consultar(PAGINA_INICIAL);
    }

    // Cualquier cambio de filtro vuelve a la página 1.
    function manejarCambioFiltro() {
        consultar(PAGINA_INICIAL);
    }

    function manejarCambioTamano() {
        var combo = nodo(ID_TAMANO);
        tamanoActual = combo ? aEntero(combo.value, TAMANO_INICIAL) : TAMANO_INICIAL;
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

        var marca = 'data-bitacora-' + tipoEvento;
        if (elemento.getAttribute(marca) === '1') {
            return;
        }

        elemento.setAttribute(marca, '1');
        elemento.addEventListener(tipoEvento, manejador);
    }

    function enlazarFormulario() {
        var form = nodo(ID_FORM);
        if (form && form.getAttribute('data-bitacora-submit') !== '1') {
            form.setAttribute('data-bitacora-submit', '1');
            form.addEventListener('submit', function (evento) {
                evento.preventDefault();
                consultar(PAGINA_INICIAL);
            });
        }

        enlazar(ID_BUSCAR, 'change', manejarCambioFiltro);
        enlazar(ID_USUARIO, 'change', manejarCambioFiltro);
        enlazar(ID_MODULO, 'change', manejarCambioFiltro);
        enlazar(ID_ACCION, 'change', manejarCambioFiltro);
        enlazar(ID_ENTIDAD, 'change', manejarCambioFiltro);
        enlazar(ID_RESULTADO, 'change', manejarCambioFiltro);
        enlazar(ID_DESDE, 'change', manejarCambioFiltro);
        enlazar(ID_HASTA, 'change', manejarCambioFiltro);
        enlazar(ID_TAMANO, 'change', manejarCambioTamano);
        enlazar(ID_BOTON_LIMPIAR, 'click', limpiarFiltros);
    }

    function iniciar() {
        if (!ns.api || !ns.sesion || !document.getElementById(ID_CUERPO)) {
            return;
        }

        enlazarFormulario();

        // Catálogos y primera página en paralelo: la tabla no espera a los combos.
        cargarCatalogos();
        consultar(PAGINA_INICIAL);
    }

    ns.bitacora = {
        iniciar: iniciar,
        consultar: consultar,
        limpiarFiltros: limpiarFiltros
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
})(window.VisorSIG);