// FE-CU19: Gestión de Disponibilidad de Personal (/Servicios/Disponibilidad).
//
// La Web consume exclusivamente la API protegida con el token de la sesión:
//   GET  api/disponibilidad?fecha&hora&busqueda&estado            -> disponibilidad efectiva
//   GET  api/disponibilidad/empleados/{id}/horarios               -> horarios del empleado
//   POST api/disponibilidad/empleados/{id}/horarios               -> crear franja
//   PUT  api/disponibilidad/horarios/{id}                          -> editar franja
//   PATCH api/disponibilidad/horarios/{id}/estado                  -> activar/desactivar franja
//   GET  api/asignaciones-trabajo                                 -> asignaciones (paginado servidor)
//   GET  api/asignaciones-trabajo/{id}                            -> detalle de asignación
//   POST api/asignaciones-trabajo                                 -> crear asignación
//   PUT  api/asignaciones-trabajo/{id}                            -> editar asignación
//   PATCH api/asignaciones-trabajo/{id}/estado                    -> cambiar estado
//
// La disponibilidad NO se recalcula aquí: el backend la resuelve (empleado activo +
// HorariosTrabajo + BajasParciales + AsignacionesTrabajo) y el frontend solo la representa.
// La lista de disponibilidad no es paginada por el servidor: se pagina en el cliente sobre el
// resultado ya obtenido (no se inventan parámetros). Las asignaciones SÍ usan paginación del
// servidor (pagina/limite).
//
// Autenticación y permisos: window.VisorSIG.api.peticion(...) con el token de la sesión y
// `protegida: true`; un 401 se deja al flujo central (api.js + guard.js) y un 403 se informa
// sin mostrar datos. Las acciones de creación/edición se ocultan cuando el backend las deniega.
//
// Seguridad del DOM: createElement + textContent; nada que llegue del backend se interpreta
// como HTML.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var RUTA_DISPONIBILIDAD = 'api/disponibilidad';
    var RUTA_HORARIOS = 'api/disponibilidad/empleados/';
    var RUTA_HORARIO = 'api/disponibilidad/horarios/';
    var RUTA_ASIGNACIONES = 'api/asignaciones-trabajo';

    var PAGINA_INICIAL = 1;
    var LIMITE_INICIAL = 8;
    var ASIG_LIMITE = 8;
    var SIN_DATO = '—';
    var MS_TOAST = 4200;

    // Estados efectivos que devuelve el backend (DisponibilidadReglas.CalcularEstado).
    var ESTADO_CSS = {
        'disponible': 'cd-badge--disponible',
        'en servicio': 'cd-badge--servicio',
        'fuera de horario': 'cd-badge--fuera',
        'baja parcial': 'cd-badge--baja',
        'inactivo': 'cd-badge--inactivo'
    };

    // Estados reales de AsignacionesTrabajo (CatalogosDisponibilidad.EstadosAsignacion).
    var ASIG_ESTADO_CSS = {
        'asignada': 'cd-badge--asignada',
        'en proceso': 'cd-badge--proceso',
        'finalizada': 'cd-badge--finalizada',
        'cancelada': 'cd-badge--cancelada'
    };

    var PRIORIDAD_CSS = {
        'alta': 'cd-prio--alta',
        'urgente': 'cd-prio--urgente'
    };

    var DIAS = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
    var DIAS_CORTOS = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];

    var TEXTO_ERROR = 'No fue posible completar la operación.';
    var TEXTO_SIN_PERMISO = 'No tiene permisos para realizar esta operación.';

    // Estado del módulo.
    var registros = [];
    var pagina = PAGINA_INICIAL;
    var limite = LIMITE_INICIAL;
    var consultaAplicada = null;
    var seleccion = null;
    var horarios = [];
    var asignacionesDia = [];
    var ventanaHorario = 0;
    var tabActivo = 'resumen';
    var subpanel = 'horario';
    var paginaAsig = PAGINA_INICIAL;
    var totalPaginasAsig = 0;
    var asignacionesTab = [];
    var empleados = [];
    var horarioEditando = null;
    var asignacionEditando = null;
    var ocupado = false;
    var puedeCrear = null;
    var puedeEditar = null;
    var secuencia = 0;
    var secuenciaHorarios = 0;
    var secuenciaAsig = 0;
    var secuenciaAsigtab = 0;
    var temporizadorToast = null;
    var iniciado = false;

    var ID = {
        raiz: 'cd-raiz',
        exito: 'cd-exito',
        toast: 'cd-toast',
        lectura: 'cd-lectura',
        tabResumen: 'cd-tab-resumen',
        tabHorarios: 'cd-tab-horarios',
        tabAsignaciones: 'cd-tab-asignaciones',
        panelResumen: 'cd-panel-resumen',
        panelHorarios: 'cd-panel-horarios',
        panelAsignaciones: 'cd-panel-asignaciones',
        filtros: 'cd-filtros',
        busqueda: 'cd-busqueda',
        estado: 'cd-estado',
        fecha: 'cd-fecha',
        hora: 'cd-hora',
        buscar: 'cd-buscar',
        limpiar: 'cd-limpiar',
        aviso: 'cd-aviso',
        titulo: 'cd-titulo',
        limite: 'cd-limite',
        cargando: 'cd-cargando',
        tablaEnvoltura: 'cd-tabla-envoltura',
        cuerpo: 'cd-cuerpo',
        vacio: 'cd-vacio',
        conteo: 'cd-conteo',
        paginacion: 'cd-paginacion',
        detVacio: 'cd-det-vacio',
        detCargando: 'cd-det-cargando',
        detContenido: 'cd-det-contenido',
        detBadge: 'cd-det-badge',
        detAvatar: 'cd-det-avatar',
        detId: 'cd-det-id',
        detNombre: 'cd-det-nombre',
        detCargo: 'cd-det-cargo',
        detArea: 'cd-det-area',
        detEstado: 'cd-det-estado',
        detFecha: 'cd-det-fecha',
        detHorario: 'cd-det-horario',
        detMotivo: 'cd-det-motivo',
        subtabHorario: 'cd-subtab-horario',
        subtabAsignaciones: 'cd-subtab-asignaciones',
        subHorario: 'cd-sub-horario',
        subAsignaciones: 'cd-sub-asignaciones',
        horarioVacio: 'cd-horario-vacio',
        horarioPrev: 'cd-horario-prev',
        horarioNext: 'cd-horario-next',
        horarioDias: 'cd-horario-dias',
        horarioPuntos: 'cd-horario-puntos',
        asigTitulo: 'cd-asig-titulo',
        asigCuerpo: 'cd-asig-cuerpo',
        asigVacio: 'cd-asig-vacio',
        nuevaAsignacion: 'cd-nueva-asignacion',
        horSelect: 'cd-hor-select',
        horRecargar: 'cd-hor-recargar',
        horAviso: 'cd-hor-aviso',
        horSemana: 'cd-hor-semana',
        horVacio: 'cd-hor-vacio',
        asigtabFiltros: 'cd-asigtab-filtros',
        asigtabEmpleado: 'cd-asigtab-empleado',
        asigtabEstado: 'cd-asigtab-estado',
        asigtabTipo: 'cd-asigtab-tipo',
        asigtabFecha: 'cd-asigtab-fecha',
        asigtabBuscar: 'cd-asigtab-buscar',
        asigtabLimpiar: 'cd-asigtab-limpiar',
        asigtabAviso: 'cd-asigtab-aviso',
        asigtabTitulo: 'cd-asigtab-titulo',
        asigtabCargando: 'cd-asigtab-cargando',
        asigtabNueva: 'cd-asigtab-nueva',
        asigtabCuerpo: 'cd-asigtab-cuerpo',
        asigtabVacio: 'cd-asigtab-vacio',
        asigtabConteo: 'cd-asigtab-conteo',
        asigtabPaginacion: 'cd-asigtab-paginacion',
        modalHorario: 'cd-modal-horario',
        horarioForm: 'cd-horario-form',
        horarioTitulo: 'cd-horario-titulo',
        horarioDia: 'cd-horario-dia',
        horarioInicio: 'cd-horario-inicio',
        horarioFin: 'cd-horario-fin',
        horarioError: 'cd-horario-error',
        horarioCerrar: 'cd-horario-cerrar',
        horarioCancelar: 'cd-horario-cancelar',
        modalAsignacion: 'cd-modal-asignacion',
        asignacionForm: 'cd-asignacion-form',
        asignacionTitulo: 'cd-asignacion-titulo',
        asignacionEmpleado: 'cd-asignacion-empleado',
        asignacionCodigo: 'cd-asignacion-codigo',
        asignacionTipo: 'cd-asignacion-tipo',
        asignacionInicio: 'cd-asignacion-inicio',
        asignacionFin: 'cd-asignacion-fin',
        asignacionPrioridad: 'cd-asignacion-prioridad',
        asignacionObservaciones: 'cd-asignacion-observaciones',
        asignacionError: 'cd-asignacion-error',
        asignacionCerrar: 'cd-asignacion-cerrar',
        asignacionCancelar: 'cd-asignacion-cancelar'
    };

    // --- Utilidades ---------------------------------------------------------

    function nodo(id) {
        return document.getElementById(id);
    }

    function vaciar(contenedor) {
        if (!contenedor) {
            return;
        }
        while (contenedor.firstChild) {
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

    function dosDigitos(valor) {
        var numero = Number(valor);
        return (numero < 10 ? '0' : '') + numero;
    }

    function hoyIso() {
        var ahora = new Date();
        return ahora.getFullYear() + '-' + dosDigitos(ahora.getMonth() + 1) + '-' + dosDigitos(ahora.getDate());
    }

    function horaActual() {
        var ahora = new Date();
        return dosDigitos(ahora.getHours()) + ':' + dosDigitos(ahora.getMinutes());
    }

    function fechaIso(fecha) {
        return fecha.getFullYear() + '-' + dosDigitos(fecha.getMonth() + 1) + '-' + dosDigitos(fecha.getDate());
    }

    // Fecha local desde un input 'YYYY-MM-DD' (sin conversiones de zona).
    function aFechaLocal(iso) {
        var partes = /^(\d{4})-(\d{2})-(\d{2})$/.exec(textoComprobado(iso));
        if (!partes) {
            return null;
        }
        var fecha = new Date(Number(partes[1]), Number(partes[2]) - 1, Number(partes[3]));
        return isNaN(fecha.getTime()) ? null : fecha;
    }

    function fechaMostrable(iso) {
        var fecha = aFechaLocal(iso);
        return fecha ? dosDigitos(fecha.getDate()) + '/' + dosDigitos(fecha.getMonth() + 1) + '/' + fecha.getFullYear() : SIN_DATO;
    }

    // Lunes de la semana a la que pertenece la fecha (semana ISO: lunes primero).
    function lunesDe(fecha) {
        var copia = new Date(fecha.getFullYear(), fecha.getMonth(), fecha.getDate());
        var dia = copia.getDay(); // 0 = domingo
        var desplazamiento = dia === 0 ? -6 : 1 - dia;
        copia.setDate(copia.getDate() + desplazamiento);
        return copia;
    }

    function mensajeError(resultado, alterno) {
        var cuerpo = resultado ? resultado.datos : null;

        if (cuerpo && typeof cuerpo === 'object') {
            var detalle = textoComprobado(valor(cuerpo, 'detail'));
            var titulo = textoComprobado(valor(cuerpo, 'title'));
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

    function iniciales(nombre) {
        var limpio = textoComprobado(nombre);
        if (limpio === '') {
            return '--';
        }
        var partes = limpio.split(/\s+/);
        var primero = partes[0].charAt(0);
        var segundo = partes.length > 1 ? partes[1].charAt(0) : '';
        return (primero + segundo).toUpperCase();
    }

    function claseEstado(estado) {
        var clave = textoComprobado(estado).toLowerCase();
        return ESTADO_CSS[clave] || 'cd-badge--inactivo';
    }

    function claseAsignacionEstado(estado) {
        var clave = textoComprobado(estado).toLowerCase();
        return ASIG_ESTADO_CSS[clave] || 'cd-badge--asignada';
    }

    // El badge nace siempre con el punto indicador (pseudo-elemento en CSS).
    function crearBadge(estado, clase) {
        var badge = crear('span', 'cd-badge ' + clase, null);
        badge.textContent = mostrable(estado);
        return badge;
    }

    function ponerTexto(id, texto) {
        var elemento = nodo(id);
        if (elemento) {
            elemento.textContent = texto;
        }
    }

    function mostrar(elemento, visible) {
        if (elemento) {
            elemento.hidden = !visible;
        }
    }

    // --- Capa de acceso a la API --------------------------------------------

    function obtenerToken() {
        return ns.sesion && typeof ns.sesion.obtenerToken === 'function'
            ? ns.sesion.obtenerToken()
            : null;
    }

    // Petición protegida. El 401 queda a cargo del flujo central (api.js + guard.js).
    function llamar(ruta, opciones) {
        opciones = opciones || {};
        var token = obtenerToken();
        if (!token) {
            return Promise.resolve({ ok: false, status: 401, datos: null, error: null });
        }
        var completa = ruta;
        if (opciones.parametros) {
            completa += '?' + opciones.parametros;
        }
        var peticion = {
            metodo: opciones.metodo || 'GET',
            token: token,
            protegida: true
        };
        if (opciones.cuerpo) {
            peticion.cuerpo = opciones.cuerpo;
        }
        return ns.api.peticion(completa, peticion);
    }

    // --- Avisos --------------------------------------------------------------

    function mostrarToast(mensaje, tono) {
        var toast = nodo(ID.toast);
        if (!toast) {
            return;
        }
        vaciar(toast);

        var icono = crear('span', 'cd-toast__icono', tono === 'error' ? '!' : (tono === 'aviso' ? '!' : '\u2713'));
        icono.setAttribute('aria-hidden', 'true');
        toast.appendChild(icono);
        toast.appendChild(crear('span', 'cd-toast__texto', mensaje));

        toast.className = 'cd-toast'
            + (tono === 'error' ? ' cd-toast--error' : (tono === 'aviso' ? ' cd-toast--aviso' : ' cd-toast--exito'));
        toast.hidden = false;

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
        var toast = nodo(ID.toast);
        if (toast) {
            toast.hidden = true;
            vaciar(toast);
        }
    }

    function mostrarExito(titulo, texto) {
        var zona = nodo(ID.exito);
        if (!zona) {
            return;
        }
        vaciar(zona);

        var icono = crear('span', 'cd-exito__icono', null);
        icono.setAttribute('aria-hidden', 'true');

        var cuerpo = crear('div', 'cd-exito__cuerpo', null);
        cuerpo.appendChild(crear('p', 'cd-exito__titulo', titulo));
        if (texto) {
            cuerpo.appendChild(crear('p', 'cd-exito__texto', texto));
        }

        var cerrar = crear('button', 'cd-exito__cerrar', '\u00d7');
        cerrar.type = 'button';
        cerrar.setAttribute('aria-label', 'Cerrar aviso');
        cerrar.addEventListener('click', ocultarExito);

        zona.appendChild(icono);
        zona.appendChild(cuerpo);
        zona.appendChild(cerrar);
        zona.hidden = false;
    }

    function ocultarExito() {
        var zona = nodo(ID.exito);
        if (zona) {
            zona.hidden = true;
            vaciar(zona);
        }
    }

    function aviso(id, mensaje, tono) {
        var zona = nodo(id);
        if (!zona) {
            return;
        }
        zona.className = tono === 'error' ? 'cd-alerta cd-alerta--error' : 'cd-alerta cd-alerta--info';
        zona.textContent = mensaje || '';
        zona.hidden = !mensaje;
    }

    // --- Permisos ------------------------------------------------------------

    // La autoridad es el backend. El frontend se apoya en los permisos que la página pueda
    // declarar (data-permisos / establecerPermisos) y, si no los conoce, actúa de forma
    // optimista y se cierra ante el primer 403 (fail-closed en la práctica).
    function establecerPermisos(opciones) {
        opciones = opciones || {};
        if (typeof opciones.puedeCrear === 'boolean') {
            puedeCrear = opciones.puedeCrear;
        }
        if (typeof opciones.puedeEditar === 'boolean') {
            puedeEditar = opciones.puedeEditar;
        }
        aplicarPermisos();
    }

    function leerPermisosDeclarados() {
        var raiz = nodo(ID.raiz);
        if (!raiz || !raiz.dataset || !raiz.dataset.permisos) {
            return;
        }
        var partes = String(raiz.dataset.permisos).split(/[;,]/);
        for (var i = 0; i < partes.length; i++) {
            var trozo = partes[i];
            var igual = trozo.indexOf('=');
            if (igual === -1) {
                continue;
            }
            var clave = trozo.slice(0, igual).trim().toLowerCase();
            var valorCrudo = trozo.slice(igual + 1).trim().toLowerCase();
            var bandera = valorCrudo === 'true' || valorCrudo === '1';
            if (clave === 'puedecrear') {
                puedeCrear = bandera;
            } else if (clave === 'puedeeditar') {
                puedeEditar = bandera;
            }
        }
        aplicarPermisos();
    }

    function permiteCrear() {
        return puedeCrear !== false;
    }

    function permiteEditar() {
        return puedeEditar !== false;
    }

    function aplicarPermisos() {
        mostrar(nodo(ID.nuevaAsignacion), permiteCrear());
        mostrar(nodo(ID.asigtabNueva), permiteCrear());
        mostrar(nodo(ID.lectura), !permiteCrear() && !permiteEditar());
    }

    // Un 403 sobre una acción de gestión deja el módulo en modo solo lectura.
    function marcarSoloLectura() {
        puedeCrear = false;
        puedeEditar = false;
        aplicarPermisos();
        aplicarPermisosFilas();
    }

    // 403 en una consulta: no se muestran datos y el módulo pasa a solo lectura.
    function bloquearPantalla(idAviso, mensaje) {
        limpiarLista();
        registros = [];
        totalPaginasAsig = 0;
        ocultarPaginacion();
        detalleVacio(null);
        ocultarExito();
        marcarSoloLectura();
        aviso(idAviso || ID.aviso, mensaje || TEXTO_SIN_PERMISO, 'error');
    }

    // Botones de edición/estado presentes en las tablas ya dibujadas.
    function aplicarPermisosFilas() {
        var contenedores = [nodo(ID.asigCuerpo), nodo(ID.asigtabCuerpo), nodo(ID.horSemana)];
        for (var i = 0; i < contenedores.length; i++) {
            var botones = contenedores[i] ? contenedores[i].children : [];
            marcarBotones(botones);
        }
    }

    function marcarBotones(nodos) {
        for (var i = 0; i < nodos.length; i++) {
            var actual = nodos[i];
            if (actual && actual.dataset && actual.dataset.permiso === 'editar') {
                actual.hidden = !permiteEditar();
                actual.disabled = !permiteEditar();
            }
            if (actual && actual.children) {
                marcarBotones(actual.children);
            }
        }
    }

    // --- Utilidades de la lista ---------------------------------------------

    function limpiarLista() {
        vaciar(nodo(ID.cuerpo));
    }

    function ocultarPaginacion() {
        var contenedor = nodo(ID.paginacion);
        if (contenedor) {
            vaciar(contenedor);
            contenedor.hidden = true;
        }
    }

    function detalleVacio(mensaje) {
        mostrar(nodo(ID.detCargando), false);
        mostrar(nodo(ID.detContenido), false);
        var vacio = nodo(ID.detVacio);
        if (vacio) {
            vacio.textContent = mensaje || 'Seleccione un empleado para visualizar su detalle.';
            vacio.hidden = false;
        }
        mostrar(nodo(ID.detBadge), false);
    }

    function detalleCargando() {
        mostrar(nodo(ID.detVacio), false);
        mostrar(nodo(ID.detContenido), false);
        mostrar(nodo(ID.detCargando), true);
    }

    function mostrarCargando(activo) {
        mostrar(nodo(ID.cargando), activo);
        var buscar = nodo(ID.buscar);
        if (buscar) {
            buscar.disabled = activo;
        }
        if (activo) {
            esqueleto(nodo(ID.cuerpo), limite, 6);
        }
    }

    function esqueleto(contenedor, filas, columnas) {
        if (!contenedor) {
            return;
        }
        vaciar(contenedor);
        for (var i = 0; i < filas; i++) {
            var fila = crear('tr', 'cd-fila cd-fila--esqueleto', null);
            for (var j = 0; j < columnas; j++) {
                fila.appendChild(crear('td', null, null));
            }
            contenedor.appendChild(fila);
        }
    }

    // --- Pestañas principales ------------------------------------------------

    function activarTab(nombre) {
        tabActivo = nombre;
        var mapa = [
            { boton: ID.tabResumen, panel: ID.panelResumen, clave: 'resumen' },
            { boton: ID.tabHorarios, panel: ID.panelHorarios, clave: 'horarios' },
            { boton: ID.tabAsignaciones, panel: ID.panelAsignaciones, clave: 'asignaciones' }
        ];
        for (var i = 0; i < mapa.length; i++) {
            var activo = mapa[i].clave === nombre;
            var boton = nodo(mapa[i].boton);
            if (boton) {
                boton.setAttribute('aria-selected', activo ? 'true' : 'false');
                boton.tabIndex = activo ? 0 : -1;
            }
            mostrar(nodo(mapa[i].panel), activo);
        }

        if (nombre === 'horarios') {
            cargarEmpleados();
            cargarSemanaHorarios();
        } else if (nombre === 'asignaciones') {
            cargarEmpleados();
            consultarAsignaciones(PAGINA_INICIAL);
        }
    }

    function activarSubpanel(nombre) {
        subpanel = nombre;
        var esHorario = nombre === 'horario';
        var botonHorario = nodo(ID.subtabHorario);
        var botonAsig = nodo(ID.subtabAsignaciones);
        if (botonHorario) {
            botonHorario.setAttribute('aria-selected', esHorario ? 'true' : 'false');
            botonHorario.tabIndex = esHorario ? 0 : -1;
        }
        if (botonAsig) {
            botonAsig.setAttribute('aria-selected', esHorario ? 'false' : 'true');
            botonAsig.tabIndex = esHorario ? -1 : 0;
        }
        mostrar(nodo(ID.subHorario), esHorario);
        mostrar(nodo(ID.subAsignaciones), !esHorario);
    }

    // --- Consulta de disponibilidad -----------------------------------------

    function valorCampo(id) {
        var campo = nodo(id);
        return campo ? textoComprobado(campo.value) : '';
    }

    function parametrosDisponibilidad() {
        var fecha = valorCampo(ID.fecha) || hoyIso();
        var hora = valorCampo(ID.hora) || horaActual();
        var partes = ['fecha=' + encodeURIComponent(fecha), 'hora=' + encodeURIComponent(hora)];
        var busqueda = valorCampo(ID.busqueda);
        if (busqueda !== '') {
            partes.push('busqueda=' + encodeURIComponent(busqueda));
        }
        var estado = valorCampo(ID.estado);
        if (estado !== '') {
            partes.push('estado=' + encodeURIComponent(estado));
        }
        return partes.join('&');
    }

    function consultarDisponibilidad() {
        aviso(ID.aviso, '', null);
        ocultarExito();

        var fecha = valorCampo(ID.fecha) || hoyIso();
        var hora = valorCampo(ID.hora) || horaActual();
        consultaAplicada = { fecha: fecha, hora: hora };

        secuencia += 1;
        var actual = secuencia;

        mostrarCargando(true);
        mostrar(nodo(ID.vacio), false);

        llamar(RUTA_DISPONIBILIDAD, { parametros: parametrosDisponibilidad() }).then(function (resultado) {
            if (actual !== secuencia) {
                return;
            }

            mostrarCargando(false);

            if (resultado.status === 401) {
                return;
            }

            if (resultado.status === 403) {
                bloquearPantalla(ID.aviso, 'No tiene permisos para consultar la disponibilidad de personal.');
                return;
            }

            if (!resultado.ok || !resultado.datos) {
                limpiarLista();
                registros = [];
                empleados = [];
                actualizarTitulo();
                actualizarConteo(0, 0);
                ocultarPaginacion();
                aviso(ID.aviso, mensajeError(resultado, TEXTO_ERROR), 'error');
                return;
            }

            registros = esArreglo(resultado.datos) ? resultado.datos : [];
            empleados = registros.slice();
            pagina = PAGINA_INICIAL;

            sincronizarEmpleados();
            actualizarKpis();
            renderizarLista();

            if (registros.length === 0) {
                mostrar(nodo(ID.vacio), true);
                mostrar(nodo(ID.tablaEnvoltura), false);
                actualizarTitulo();
                actualizarConteo(0, 0);
                ocultarPaginacion();
                detalleVacio(null);
                return;
            }

            mostrar(nodo(ID.vacio), false);
            mostrar(nodo(ID.tablaEnvoltura), true);
            mostrarExito('Disponibilidad actualizada correctamente',
                'Los datos del personal fueron sincronizados.');

            // Se selecciona el primer registro de la página para publicar su detalle.
            if (seleccion && !buscarRegistro(aEntero(valor(seleccion, 'idEmpleado'), 0))) {
                seleccion = null;
            }
            if (!seleccion) {
                seleccionar(aEntero(valor(registros[0], 'idEmpleado'), 0));
            } else {
                marcarSeleccion();
                pintarDetalle(seleccion);
            }
        });
    }

    // --- KPIs ----------------------------------------------------------------

    function contarEstado(estado, totales) {
        var clave = textoComprobado(estado).toLowerCase();
        if (Object.prototype.hasOwnProperty.call(totales, clave)) {
            totales[clave] += 1;
        }
    }

    function actualizarKpi(clave, cantidad, total) {
        var pct = total > 0 ? Math.round((cantidad / total) * 100) : 0;
        ponerTexto('cd-kpi-' + clave + '-valor', String(cantidad));
        ponerTexto('cd-kpi-' + clave + '-meta', 'de ' + total + ' empleados');

        var pctNodo = nodo('cd-kpi-' + clave + '-pct');
        if (pctNodo) {
            pctNodo.textContent = pct + '%';
        }

        var circulo = nodo('cd-kpi-' + clave + '-circulo');
        if (circulo) {
            var longitud = 97.4;
            circulo.setAttribute('stroke-dashoffset', String(Math.round((longitud * (100 - pct)) / 100 * 10) / 10));
        }

        var anillo = nodo('cd-kpi-' + clave + '-anillo');
        if (anillo) {
            anillo.setAttribute('aria-label', pct + '%');
        }
    }

    function actualizarKpis() {
        var total = registros.length;
        var totales = { 'disponible': 0, 'en servicio': 0, 'fuera de horario': 0, 'baja parcial': 0 };
        for (var i = 0; i < registros.length; i++) {
            contarEstado(valor(registros[i], 'estadoDisponibilidad'), totales);
        }
        actualizarKpi('disponible', totales['disponible'], total);
        actualizarKpi('servicio', totales['en servicio'], total);
        actualizarKpi('fuera', totales['fuera de horario'], total);
        actualizarKpi('baja', totales['baja parcial'], total);
    }

    // --- Listado de personal (paginación en cliente) -------------------------

    function actualizarTitulo() {
        ponerTexto(ID.titulo, 'Listado de Personal (' + registros.length + ' registros)');
    }

    function actualizarConteo(desde, hasta) {
        var zona = nodo(ID.conteo);
        if (!zona) {
            return;
        }
        if (registros.length === 0) {
            zona.textContent = '';
            return;
        }
        zona.textContent = 'Mostrando ' + desde + ' a ' + hasta + ' de ' + registros.length + ' registros';
    }

    function renderizarLista() {
        var cuerpo = nodo(ID.cuerpo);
        if (!cuerpo) {
            return;
        }
        vaciar(cuerpo);

        var inicio = (pagina - 1) * limite;
        var fin = Math.min(inicio + limite, registros.length);

        for (var i = inicio; i < fin; i++) {
            cuerpo.appendChild(crearFila(registros[i]));
        }

        actualizarTitulo();
        actualizarConteo(inicio + 1, fin);
        renderizarPaginacion();
        marcarSeleccion();
    }

    function crearFila(registro) {
        var id = aEntero(valor(registro, 'idEmpleado'), 0);
        var nombre = textoComprobado(valor(registro, 'nombreCompleto'));
        var codigo = textoComprobado(valor(registro, 'codigo'));

        var tr = crear('tr', 'cd-fila', null);
        tr.dataset.id = String(id);

        var celdaEmpleado = crear('td', null, null);
        var contenedor = crear('div', 'cd-celda--empleado', null);
        contenedor.appendChild(crear('span', 'cd-avatar cd-avatar--fila', iniciales(nombre)));
        var texto = crear('div', 'cd-celda--empleado-texto', null);
        texto.appendChild(crear('span', 'cd-celda__nombre', mostrable(nombre)));
        texto.appendChild(crear('span', 'cd-celda__codigo', mostrable(codigo)));
        contenedor.appendChild(texto);
        celdaEmpleado.appendChild(contenedor);
        tr.appendChild(celdaEmpleado);

        tr.appendChild(crear('td', null, mostrable(valor(registro, 'cargo'))));

        var celdaEstado = crear('td', null, null);
        celdaEstado.appendChild(crearBadge(valor(registro, 'estadoDisponibilidad'),
            claseEstado(valor(registro, 'estadoDisponibilidad'))));
        tr.appendChild(celdaEstado);

        var horario = valor(registro, 'horarioAplicable');
        tr.appendChild(crear('td', null, horario ? mostrable(horario) : 'Sin horario'));

        var asignacion = valor(registro, 'asignacionActual');
        var textoAsig = asignacion ? mostrable(valor(asignacion, 'tipoTarea')) : 'Sin asignación';
        tr.appendChild(crear('td', null, textoAsig));

        var celdaAccion = crear('td', 'cd-celda--accion', null);
        var boton = crear('button', 'cd-btn cd-btn--mini cd-btn--ver', 'Ver detalle');
        boton.type = 'button';
        boton.setAttribute('aria-label', 'Ver detalle del empleado ' + (codigo || id));
        boton.addEventListener('click', function () {
            seleccionar(id);
        });
        celdaAccion.appendChild(boton);
        tr.appendChild(celdaAccion);

        tr.addEventListener('click', function () {
            seleccionar(id);
        });

        return tr;
    }

    function marcarSeleccion() {
        var cuerpo = nodo(ID.cuerpo);
        if (!cuerpo) {
            return;
        }
        var filas = cuerpo.children;
        for (var i = 0; i < filas.length; i++) {
            var activa = seleccion && filas[i].dataset.id === String(valor(seleccion, 'idEmpleado'));
            filas[i].classList.toggle('cd-fila--activa', !!activa);
            filas[i].setAttribute('aria-selected', activa ? 'true' : 'false');
        }
    }

    function botonPagina(etiqueta, destino, titulo, deshabilitado, activo) {
        var boton = crear('button',
            'cd-paginacion__boton' + (activo ? ' cd-paginacion__boton--activo' : ''),
            etiqueta);
        boton.type = 'button';
        boton.disabled = !!deshabilitado;
        boton.setAttribute('title', titulo);
        boton.setAttribute('aria-label', titulo);
        if (activo) {
            boton.setAttribute('aria-current', 'page');
        }
        boton.addEventListener('click', function () {
            irAPagina(destino);
        });
        return boton;
    }

    function renderizarPaginacion() {
        var contenedor = nodo(ID.paginacion);
        if (!contenedor) {
            return;
        }
        vaciar(contenedor);

        var totalPaginas = Math.ceil(registros.length / limite);
        if (totalPaginas <= 1) {
            contenedor.hidden = true;
            return;
        }
        contenedor.hidden = false;

        contenedor.appendChild(botonPagina('\u00ab', 1, 'Primera página', pagina <= 1, false));
        contenedor.appendChild(botonPagina('\u2039', pagina - 1, 'Página anterior', pagina <= 1, false));

        var desde = Math.max(1, pagina - 2);
        var hasta = Math.min(totalPaginas, desde + 4);
        desde = Math.max(1, hasta - 4);
        for (var p = desde; p <= hasta; p++) {
            contenedor.appendChild(botonPagina(String(p), p, 'Página ' + p, false, p === pagina));
        }

        contenedor.appendChild(botonPagina('\u203a', pagina + 1, 'Página siguiente', pagina >= totalPaginas, false));
        contenedor.appendChild(botonPagina('\u00bb', totalPaginas, 'Última página', pagina >= totalPaginas, false));
    }

    function irAPagina(destino) {
        var totalPaginas = Math.ceil(registros.length / limite) || 1;
        var objetivo = aEntero(destino, 1);
        if (objetivo < 1 || objetivo > totalPaginas) {
            return;
        }
        pagina = objetivo;
        renderizarLista();
    }

    function cambiarLimite() {
        var combo = nodo(ID.limite);
        limite = combo ? aEntero(combo.value, LIMITE_INICIAL) : LIMITE_INICIAL;
        if (limite < 1) {
            limite = LIMITE_INICIAL;
        }
        pagina = PAGINA_INICIAL;
        renderizarLista();
    }

    // --- Detalle del empleado ------------------------------------------------

    function buscarRegistro(idEmpleado) {
        for (var i = 0; i < registros.length; i++) {
            if (aEntero(valor(registros[i], 'idEmpleado'), 0) === idEmpleado) {
                return registros[i];
            }
        }
        return null;
    }

    function seleccionar(idEmpleado) {
        var registro = buscarRegistro(idEmpleado);
        if (!registro) {
            return;
        }
        seleccion = registro;
        marcarSeleccion();
        pintarDetalle(registro);

        ventanaHorario = 0;
        horarios = [];
        asignacionesDia = [];
        activarSubpanel('horario');
        cargarHorarios(idEmpleado);
        cargarAsignacionesDia(idEmpleado);
    }

    function pintarDetalle(registro) {
        mostrar(nodo(ID.detVacio), false);
        mostrar(nodo(ID.detCargando), false);
        mostrar(nodo(ID.detContenido), true);

        var estado = valor(registro, 'estadoDisponibilidad');
        var nombre = textoComprobado(valor(registro, 'nombreCompleto'));

        var avatar = nodo(ID.detAvatar);
        if (avatar) {
            avatar.textContent = iniciales(nombre);
        }

        ponerTexto(ID.detId, mostrable(valor(registro, 'codigo')));
        ponerTexto(ID.detNombre, mostrable(nombre));
        ponerTexto(ID.detCargo, mostrable(valor(registro, 'cargo')));
        ponerTexto(ID.detArea, mostrable(valor(registro, 'area')));
        ponerTexto(ID.detHorario, mostrable(valor(registro, 'horarioAplicable')));
        ponerTexto(ID.detMotivo, mostrable(valor(registro, 'motivo')));

        var detalleEstado = nodo(ID.detEstado);
        if (detalleEstado) {
            detalleEstado.textContent = '';
            detalleEstado.appendChild(crearBadge(estado, claseEstado(estado)));
        }

        ponerTexto(ID.detFecha, consultaAplicada
            ? fechaMostrable(consultaAplicada.fecha) + ' ' + consultaAplicada.hora
            : SIN_DATO);

        var badge = nodo(ID.detBadge);
        if (badge) {
            badge.className = 'cd-badge ' + claseEstado(estado);
            badge.textContent = mostrable(estado);
            badge.hidden = false;
        }

        ponerTexto(ID.asigTitulo, 'Asignaciones del día ('
            + (consultaAplicada ? fechaMostrable(consultaAplicada.fecha) : SIN_DATO) + ')');
    }

    // --- Horario semanal (subpanel del detalle) ------------------------------

    function cargarHorarios(idEmpleado) {
        secuenciaHorarios += 1;
        var actual = secuenciaHorarios;

        llamar(RUTA_HORARIOS + idEmpleado + '/horarios', {}).then(function (resultado) {
            if (actual !== secuenciaHorarios) {
                return;
            }
            if (resultado.status === 401) {
                return;
            }
            if (resultado.status === 403) {
                marcarSoloLectura();
                horarios = [];
                renderizarCarrusel();
                return;
            }
            if (!resultado.ok || !resultado.datos) {
                horarios = [];
                renderizarCarrusel();
                return;
            }
            horarios = esArreglo(resultado.datos) ? resultado.datos : [];
            renderizarCarrusel();
        });
    }

    function horariosDeDia(dia) {
        var lista = [];
        for (var i = 0; i < horarios.length; i++) {
            if (aEntero(valor(horarios[i], 'diaSemana'), 0) === dia) {
                lista.push(horarios[i]);
            }
        }
        return lista;
    }

    function horaCorta(hora) {
        var texto = textoComprobado(hora);
        return texto.length >= 5 ? texto.slice(0, 5) : texto;
    }

    function crearFranja(horario) {
        var activo = valor(horario, 'activo') !== false;
        var franja = crear('span', 'cd-franja' + (activo ? '' : ' cd-franja--inactiva'),
            horaCorta(valor(horario, 'horaInicio')) + ' - ' + horaCorta(valor(horario, 'horaFin')));
        franja.setAttribute('title', activo ? 'Franja activa' : 'Franja inactiva');
        return franja;
    }

    function renderizarCarrusel() {
        var contenedor = nodo(ID.horarioDias);
        if (!contenedor) {
            return;
        }
        vaciar(contenedor);

        var hayHorarios = horarios.length > 0;
        mostrar(nodo(ID.horarioVacio), !hayHorarios);
        mostrar(nodo(ID.horarioPuntos), hayHorarios);

        var base = lunesDe(aFechaLocal(consultaAplicada ? consultaAplicada.fecha : hoyIso()) || new Date());
        var indices = ventanaHorario === 0 ? [0, 1, 2, 3, 4] : [5, 6];

        for (var k = 0; k < indices.length; k++) {
            var indice = indices[k];
            var fecha = new Date(base.getFullYear(), base.getMonth(), base.getDate() + indice);
            var tarjeta = crear('div', 'cd-dia', null);
            if (consultaAplicada && fechaIso(fecha) === consultaAplicada.fecha) {
                tarjeta.className = 'cd-dia cd-dia--activo';
            }

            tarjeta.appendChild(crear('span', 'cd-dia__nombre', DIAS_CORTOS[indice]));
            tarjeta.appendChild(crear('span', 'cd-dia__fecha',
                dosDigitos(fecha.getDate()) + '/' + dosDigitos(fecha.getMonth() + 1)));

            var franjas = horariosDeDia(indice + 1);
            if (franjas.length === 0) {
                tarjeta.appendChild(crear('span', 'cd-dia__sin', 'Sin horario'));
            } else {
                for (var f = 0; f < franjas.length; f++) {
                    tarjeta.appendChild(crearFranja(franjas[f]));
                }
            }

            contenedor.appendChild(tarjeta);
        }

        moverPuntos();
        var prev = nodo(ID.horarioPrev);
        var next = nodo(ID.horarioNext);
        if (prev) {
            prev.disabled = ventanaHorario <= 0;
        }
        if (next) {
            next.disabled = ventanaHorario >= 1;
        }
    }

    function moverPuntos() {
        var puntos = nodo(ID.horarioPuntos);
        if (!puntos) {
            return;
        }
        vaciar(puntos);
        for (var i = 0; i < 2; i++) {
            puntos.appendChild(crear('span',
                'cd-carrusel__punto' + (i === ventanaHorario ? ' cd-carrusel__punto--activo' : ''), null));
        }
    }

    // Carrusel: 0 = lunes a viernes, 1 = fin de semana.
    function moverHorario(delta) {
        var destino = ventanaHorario + delta;
        if (destino < 0 || destino > 1) {
            return;
        }
        ventanaHorario = destino;
        renderizarCarrusel();
    }

    // --- Asignaciones del día (subpanel del detalle) -------------------------

    function cargarAsignacionesDia(idEmpleado) {
        secuenciaAsig += 1;
        var actual = secuenciaAsig;
        var parametros = 'idEmpleado=' + encodeURIComponent(idEmpleado)
            + '&fecha=' + encodeURIComponent(consultaAplicada ? consultaAplicada.fecha : hoyIso())
            + '&pagina=1&limite=50';

        llamar(RUTA_ASIGNACIONES, { parametros: parametros }).then(function (resultado) {
            if (actual !== secuenciaAsig) {
                return;
            }
            if (resultado.status === 401) {
                return;
            }
            if (resultado.status === 403) {
                marcarSoloLectura();
                asignacionesDia = [];
                renderizarAsignacionesDia();
                return;
            }
            if (!resultado.ok || !resultado.datos) {
                asignacionesDia = [];
                renderizarAsignacionesDia();
                return;
            }
            var lista = valor(resultado.datos, 'datos');
            asignacionesDia = esArreglo(lista) ? lista : [];
            renderizarAsignacionesDia();
        });
    }

    function horaDeFecha(iso) {
        var texto = textoComprobado(iso);
        var match = /T(\d{2}):(\d{2})/.exec(texto);
        if (match) {
            return match[1] + ':' + match[2];
        }
        return SIN_DATO;
    }

    function renderizarAsignacionesDia() {
        var cuerpo = nodo(ID.asigCuerpo);
        if (!cuerpo) {
            return;
        }
        vaciar(cuerpo);

        mostrar(nodo(ID.asigVacio), asignacionesDia.length === 0);

        for (var i = 0; i < asignacionesDia.length; i++) {
            var asignacion = asignacionesDia[i];
            var tr = crear('tr', 'cd-fila', null);

            tr.appendChild(crear('td', null, horaDeFecha(valor(asignacion, 'fechaInicio'))
                + ' - ' + horaDeFecha(valor(asignacion, 'fechaFin'))));
            tr.appendChild(crear('td', null, mostrable(valor(asignacion, 'tipoTarea'))));

            var codigoFijo = valor(asignacion, 'codigoFijo');
            tr.appendChild(crear('td', null, codigoFijo ? mostrable(codigoFijo) : 'Sin ubicación asociada'));

            var celdaEstado = crear('td', null, null);
            celdaEstado.appendChild(crearBadge(valor(asignacion, 'estado'),
                claseAsignacionEstado(valor(asignacion, 'estado'))));
            tr.appendChild(celdaEstado);

            var celdaAccion = crear('td', 'cd-celda--accion', null);
            var boton = crear('button', 'cd-btn cd-btn--mini cd-btn--secundario', 'Editar');
            boton.type = 'button';
            boton.dataset.permiso = 'editar';
            boton.setAttribute('aria-label', 'Editar la asignación ' + valor(asignacion, 'idAsignacion'));
            boton.asignacion = asignacion;
            boton.addEventListener('click', function (evento) {
                evento.stopPropagation();
                abrirModalAsignacion(this.asignacion);
            });
            boton.hidden = !permiteEditar();
            boton.disabled = !permiteEditar();
            celdaAccion.appendChild(boton);
            tr.appendChild(celdaAccion);

            cuerpo.appendChild(tr);
        }
    }

    // --- Empleados para los selectores --------------------------------------

    function cargarEmpleados() {
        if (empleados.length > 0) {
            sincronizarEmpleados();
            return;
        }
        var parametros = 'fecha=' + encodeURIComponent(valorCampo(ID.fecha) || hoyIso())
            + '&hora=' + encodeURIComponent(valorCampo(ID.hora) || horaActual());
        llamar(RUTA_DISPONIBILIDAD, { parametros: parametros }).then(function (resultado) {
            if (resultado.status === 401) {
                return;
            }
            if (resultado.status === 403) {
                marcarSoloLectura();
                return;
            }
            if (!resultado.ok || !resultado.datos) {
                return;
            }
            empleados = esArreglo(resultado.datos) ? resultado.datos : [];
            sincronizarEmpleados();
        });
    }

    function etiquetaEmpleado(empleado) {
        var nombre = textoComprobado(valor(empleado, 'nombreCompleto'));
        var codigo = textoComprobado(valor(empleado, 'codigo'));
        if (nombre !== '' && codigo !== '') {
            return nombre + ' (' + codigo + ')';
        }
        return nombre !== '' ? nombre : codigo;
    }

    function llenarSelect(select, conVacio) {
        if (!select) {
            return;
        }
        var previo = select.value;
        var opciones = [];
        if (conVacio) {
            var vacia = document.createElement('option');
            vacia.value = '';
            vacia.textContent = 'Todos los empleados';
            opciones.push(vacia);
        }
        for (var i = 0; i < empleados.length; i++) {
            var opcion = document.createElement('option');
            opcion.value = String(aEntero(valor(empleados[i], 'idEmpleado'), 0));
            opcion.textContent = etiquetaEmpleado(empleados[i]);
            opciones.push(opcion);
        }
        select.replaceChildren.apply(select, opciones);
        if (previo) {
            select.value = previo;
        }
    }

    function sincronizarEmpleados() {
        llenarSelect(nodo(ID.horSelect), false);
        llenarSelect(nodo(ID.asigtabEmpleado), true);
        llenarSelect(nodo(ID.asignacionEmpleado), false);
    }

    // --- Pestaña Horarios (configuración semanal) ----------------------------

    function cargarSemanaHorarios() {
        var select = nodo(ID.horSelect);
        var idEmpleado = select ? aEntero(select.value, 0) : 0;
        if (!(idEmpleado > 0) && seleccion) {
            idEmpleado = aEntero(valor(seleccion, 'idEmpleado'), 0);
            if (select && idEmpleado > 0 && !select.value) {
                select.value = String(idEmpleado);
            }
        }
        if (!(idEmpleado > 0)) {
            horarios = [];
            pintarSemana();
            return;
        }

        aviso(ID.horAviso, '', null);
        secuenciaHorarios += 1;
        var actual = secuenciaHorarios;

        llamar(RUTA_HORARIOS + idEmpleado + '/horarios', {}).then(function (resultado) {
            if (actual !== secuenciaHorarios) {
                return;
            }
            if (resultado.status === 401) {
                return;
            }
            if (resultado.status === 403) {
                marcarSoloLectura();
                aviso(ID.horAviso, TEXTO_SIN_PERMISO, 'error');
                horarios = [];
                pintarSemana();
                return;
            }
            if (resultado.status === 404) {
                horarios = [];
                pintarSemana();
                aviso(ID.horAviso, 'El empleado indicado no existe.', 'error');
                return;
            }
            if (!resultado.ok || !resultado.datos) {
                horarios = [];
                pintarSemana();
                aviso(ID.horAviso, mensajeError(resultado, TEXTO_ERROR), 'error');
                return;
            }
            horarios = esArreglo(resultado.datos) ? resultado.datos : [];
            pintarSemana();
        });
    }

    function pintarSemana() {
        var contenedor = nodo(ID.horSemana);
        if (!contenedor) {
            return;
        }
        vaciar(contenedor);

        var hay = horarios.length > 0;
        mostrar(nodo(ID.horVacio), !hay);

        for (var dia = 1; dia <= 7; dia++) {
            var tarjeta = crear('div', 'cd-semana__dia', null);

            var cabecera = crear('div', 'cd-semana__cabecera', null);
            cabecera.appendChild(crear('h3', 'cd-semana__nombre', DIAS[dia - 1]));
            var agregar = crear('button', 'cd-btn cd-btn--mini cd-btn--primario cd-semana__agregar', 'Agregar franja');
            agregar.type = 'button';
            agregar.dataset.permiso = 'editar';
            agregar.setAttribute('aria-label', 'Agregar franja para el ' + DIAS[dia - 1]);
            agregar.dia = dia;
            agregar.addEventListener('click', function () {
                abrirModalHorario(this.dia, null);
            });
            agregar.hidden = !permiteCrear();
            agregar.disabled = !permiteCrear();
            cabecera.appendChild(agregar);
            tarjeta.appendChild(cabecera);

            var franjas = horariosDeDia(dia);
            var lista = crear('div', 'cd-semana__franjas', null);
            if (franjas.length === 0) {
                lista.appendChild(crear('span', 'cd-semana__sin', 'Sin horario'));
            } else {
                for (var i = 0; i < franjas.length; i++) {
                    lista.appendChild(crearFranjaSemana(franjas[i]));
                }
            }
            tarjeta.appendChild(lista);
            contenedor.appendChild(tarjeta);
        }
    }

    function crearFranjaSemana(horario) {
        var activo = valor(horario, 'activo') !== false;
        var fila = crear('div', 'cd-semana__franja' + (activo ? '' : ' cd-semana__franja--inactiva'), null);
        var idHorario = aEntero(valor(horario, 'idHorario'), 0);
        var dia = aEntero(valor(horario, 'diaSemana'), 1);

        fila.appendChild(crear('span', 'cd-semana__horas',
            horaCorta(valor(horario, 'horaInicio')) + ' - ' + horaCorta(valor(horario, 'horaFin'))));

        var acciones = crear('div', 'cd-semana__acciones', null);

        var editar = crear('button', 'cd-btn cd-btn--mini cd-btn--secundario', 'Editar');
        editar.type = 'button';
        editar.dataset.permiso = 'editar';
        editar.setAttribute('aria-label', 'Editar la franja de las ' + horaCorta(valor(horario, 'horaInicio')));
        editar.horario = horario;
        editar.dia = dia;
        editar.addEventListener('click', function () {
            abrirModalHorario(this.dia, this.horario);
        });
        editar.hidden = !permiteEditar();
        editar.disabled = !permiteEditar();
        acciones.appendChild(editar);

        var alternar = crear('button', 'cd-btn cd-btn--mini cd-btn--secundario', activo ? 'Desactivar' : 'Activar');
        alternar.type = 'button';
        alternar.dataset.permiso = 'editar';
        alternar.setAttribute('aria-label', (activo ? 'Desactivar' : 'Activar') + ' la franja');
        alternar.estadoObjetivo = !activo;
        alternar.addEventListener('click', function () {
            cambiarEstadoHorario(idHorario, this.estadoObjetivo);
        });
        alternar.hidden = !permiteEditar();
        alternar.disabled = !permiteEditar();
        acciones.appendChild(alternar);

        fila.appendChild(acciones);
        return fila;
    }

    function cambiarEstadoHorario(idHorario, activo) {
        if (!permiteEditar() || ocupado) {
            return;
        }
        ocupado = true;
        llamar(RUTA_HORARIO + encodeURIComponent(idHorario) + '/estado', {
            metodo: 'PATCH',
            cuerpo: { activo: !!activo }
        }).then(function (resultado) {
            ocupado = false;
            if (resultado.status === 401) {
                return;
            }
            if (resultado.status === 403) {
                marcarSoloLectura();
                aviso(ID.horAviso, TEXTO_SIN_PERMISO, 'error');
                return;
            }
            if (!resultado.ok) {
                aviso(ID.horAviso, mensajeError(resultado, TEXTO_ERROR), 'error');
                return;
            }
            mostrarToast(activo ? 'Horario activado correctamente' : 'Horario desactivado correctamente', 'exito');
            cargarSemanaHorarios();
        });
    }

    // --- Modal de franja horaria ---------------------------------------------

    function abrirModalHorario(dia, horario) {
        if (horario ? !permiteEditar() : !permiteCrear()) {
            aviso(ID.horAviso, TEXTO_SIN_PERMISO, 'error');
            return;
        }
        horarioEditando = horario || null;

        ponerTexto(ID.horarioTitulo, horario ? 'Editar franja horaria' : 'Nueva franja horaria');

        var comboDia = nodo(ID.horarioDia);
        if (comboDia) {
            comboDia.value = String(horario ? aEntero(valor(horario, 'diaSemana'), 1) : (dia || 1));
        }
        var inicio = nodo(ID.horarioInicio);
        var fin = nodo(ID.horarioFin);
        if (inicio) {
            inicio.value = horario ? horaCorta(valor(horario, 'horaInicio')) : '';
        }
        if (fin) {
            fin.value = horario ? horaCorta(valor(horario, 'horaFin')) : '';
        }

        aviso(ID.horarioError, '', 'error');
        mostrar(nodo(ID.modalHorario), true);
    }

    function cerrarModalHorario() {
        mostrar(nodo(ID.modalHorario), false);
        horarioEditando = null;
    }

    function guardarHorario(evento) {
        if (evento && typeof evento.preventDefault === 'function') {
            evento.preventDefault();
        }
        if (ocupado) {
            return;
        }

        var comboDia = nodo(ID.horarioDia);
        var dia = comboDia ? aEntero(comboDia.value, 0) : 0;
        var inicio = valorCampo(ID.horarioInicio);
        var fin = valorCampo(ID.horarioFin);

        if (dia < 1 || dia > 7) {
            aviso(ID.horarioError, 'Seleccione un día válido.', 'error');
            return;
        }
        if (inicio === '' || fin === '') {
            aviso(ID.horarioError, 'Indique la hora de inicio y de fin.', 'error');
            return;
        }
        if (inicio >= fin) {
            aviso(ID.horarioError, 'La hora de inicio debe ser anterior a la hora de fin.', 'error');
            return;
        }

        var idEmpleado = 0;
        if (horarioEditando) {
            idEmpleado = aEntero(valor(horarioEditando, 'idEmpleado'), 0);
        } else {
            var select = nodo(ID.horSelect);
            idEmpleado = select ? aEntero(select.value, 0) : 0;
        }
        if (!(idEmpleado > 0)) {
            aviso(ID.horarioError, 'Seleccione un empleado.', 'error');
            return;
        }

        aviso(ID.horarioError, '', 'error');
        ocupado = true;

        var cuerpo = { diaSemana: dia, horaInicio: inicio, horaFin: fin };
        var esEdicion = !!horarioEditando;
        var ruta = esEdicion
            ? RUTA_HORARIO + encodeURIComponent(aEntero(valor(horarioEditando, 'idHorario'), 0))
            : RUTA_HORARIOS + idEmpleado + '/horarios';

        llamar(ruta, { metodo: esEdicion ? 'PUT' : 'POST', cuerpo: cuerpo }).then(function (resultado) {
            ocupado = false;

            if (resultado.status === 401) {
                return;
            }
            if (resultado.status === 403) {
                mostrar(nodo(ID.modalHorario), false);
                marcarSoloLectura();
                aviso(ID.horAviso, TEXTO_SIN_PERMISO, 'error');
                mostrarToast(TEXTO_SIN_PERMISO, 'error');
                return;
            }
            if (!resultado.ok) {
                var mensaje = mensajeError(resultado, TEXTO_ERROR);
                if (resultado.status === 409) {
                    aviso(ID.horarioError, 'Conflicto: ' + mensaje, 'error');
                    mostrarToast('Horario incompatible: ' + mensaje, 'aviso');
                    return;
                }
                aviso(ID.horarioError, mensaje, 'error');
                return;
            }

            cerrarModalHorario();
            mostrarToast(esEdicion ? 'Horario actualizado correctamente' : 'Horario registrado correctamente', 'exito');
            horarios = [];
            cargarSemanaHorarios();
            if (seleccion) {
                cargarHorarios(aEntero(valor(seleccion, 'idEmpleado'), 0));
            }
        });
    }

    // --- Pestaña Asignaciones (gestión general, paginado servidor) -----------

    function parametrosAsignaciones(paginaNueva) {
        var partes = [
            'pagina=' + encodeURIComponent(paginaNueva),
            'limite=' + encodeURIComponent(ASIG_LIMITE)
        ];
        var empleado = valorCampo(ID.asigtabEmpleado);
        if (empleado !== '') {
            partes.push('idEmpleado=' + encodeURIComponent(empleado));
        }
        var estado = valorCampo(ID.asigtabEstado);
        if (estado !== '') {
            partes.push('estado=' + encodeURIComponent(estado));
        }
        var tipo = valorCampo(ID.asigtabTipo);
        if (tipo !== '') {
            partes.push('tipoTarea=' + encodeURIComponent(tipo));
        }
        var fecha = valorCampo(ID.asigtabFecha);
        if (fecha !== '') {
            partes.push('fecha=' + encodeURIComponent(fecha));
        }
        return partes.join('&');
    }

    function consultarAsignaciones(paginaNueva) {
        aviso(ID.asigtabAviso, '', null);
        secuenciaAsigtab += 1;
        var actual = secuenciaAsigtab;
        var destino = aEntero(paginaNueva, PAGINA_INICIAL);
        if (destino < 1) {
            destino = PAGINA_INICIAL;
        }

        mostrar(nodo(ID.asigtabCargando), true);
        esqueleto(nodo(ID.asigtabCuerpo), ASIG_LIMITE, 8);

        llamar(RUTA_ASIGNACIONES, { parametros: parametrosAsignaciones(destino) }).then(function (resultado) {
            if (actual !== secuenciaAsigtab) {
                return;
            }
            mostrar(nodo(ID.asigtabCargando), false);

            if (resultado.status === 401) {
                return;
            }
            if (resultado.status === 403) {
                limpiarAsignacionesTab();
                marcarSoloLectura();
                aviso(ID.asigtabAviso, 'No tiene permisos para consultar las asignaciones de trabajo.', 'error');
                return;
            }
            if (!resultado.ok || !resultado.datos) {
                limpiarAsignacionesTab();
                aviso(ID.asigtabAviso, mensajeError(resultado, TEXTO_ERROR), 'error');
                return;
            }

            paginaAsig = aEntero(valor(resultado.datos, 'pagina'), destino);
            var lista = valor(resultado.datos, 'datos');
            asignacionesTab = esArreglo(lista) ? lista : [];
            var total = aEntero(valor(resultado.datos, 'totalRegistros'), asignacionesTab.length);
            totalPaginasAsig = aEntero(valor(resultado.datos, 'totalPaginas'), 0);
            if (totalPaginasAsig < 1 && total > 0) {
                totalPaginasAsig = Math.ceil(total / ASIG_LIMITE);
            }

            renderizarAsignacionesTab(total);
        });
    }

    function limpiarAsignacionesTab() {
        asignacionesTab = [];
        vaciar(nodo(ID.asigtabCuerpo));
        mostrar(nodo(ID.asigtabVacio), false);
        ponerTexto(ID.asigtabTitulo, 'Asignaciones (0 registros)');
        ponerTexto(ID.asigtabConteo, '');
        var paginacion = nodo(ID.asigtabPaginacion);
        if (paginacion) {
            vaciar(paginacion);
            paginacion.hidden = true;
        }
    }

    function renderizarAsignacionesTab(total) {
        var cuerpo = nodo(ID.asigtabCuerpo);
        if (!cuerpo) {
            return;
        }
        vaciar(cuerpo);

        ponerTexto(ID.asigtabTitulo, 'Asignaciones (' + total + ' registros)');
        mostrar(nodo(ID.asigtabVacio), asignacionesTab.length === 0);

        var inicio = (paginaAsig - 1) * ASIG_LIMITE;
        var fin = inicio + asignacionesTab.length;
        ponerTexto(ID.asigtabConteo, asignacionesTab.length === 0 ? ''
            : 'Mostrando ' + (inicio + 1) + ' a ' + fin + ' de ' + total + ' registros');

        for (var i = 0; i < asignacionesTab.length; i++) {
            cuerpo.appendChild(crearFilaAsignacion(asignacionesTab[i]));
        }

        renderizarPaginacionAsig();
    }

    function crearFilaAsignacion(asignacion) {
        var id = aEntero(valor(asignacion, 'idAsignacion'), 0);
        var tr = crear('tr', 'cd-fila', null);

        var nombre = textoComprobado(valor(asignacion, 'nombreEmpleado'));
        var codigo = textoComprobado(valor(asignacion, 'codigoEmpleado'));
        tr.appendChild(crear('td', null, codigo !== '' ? nombre + ' (' + codigo + ')' : mostrable(nombre)));
        tr.appendChild(crear('td', null, mostrable(valor(asignacion, 'tipoTarea'))));

        var codigoFijo = valor(asignacion, 'codigoFijo');
        tr.appendChild(crear('td', null, codigoFijo ? mostrable(codigoFijo) : SIN_DATO));

        tr.appendChild(crear('td', null, fechaCorta(valor(asignacion, 'fechaInicio'))));
        tr.appendChild(crear('td', null, fechaCorta(valor(asignacion, 'fechaFin'))));

        var celdaEstado = crear('td', null, null);
        celdaEstado.appendChild(crearBadge(valor(asignacion, 'estado'),
            claseAsignacionEstado(valor(asignacion, 'estado'))));
        tr.appendChild(celdaEstado);

        var celdaPrioridad = crear('td', null, null);
        var prioridad = textoComprobado(valor(asignacion, 'prioridad'));
        var clasePrioridad = PRIORIDAD_CSS[prioridad.toLowerCase()];
        celdaPrioridad.appendChild(crear('span',
            'cd-prio' + (clasePrioridad ? ' ' + clasePrioridad : ''), mostrable(prioridad)));
        tr.appendChild(celdaPrioridad);

        var celdaAccion = crear('td', 'cd-celda--accion', null);

        var editar = crear('button', 'cd-btn cd-btn--mini cd-btn--secundario', 'Editar');
        editar.type = 'button';
        editar.dataset.permiso = 'editar';
        editar.asignacion = asignacion;
        editar.setAttribute('aria-label', 'Editar la asignación ' + id);
        editar.addEventListener('click', function () {
            abrirModalAsignacion(this.asignacion);
        });
        editar.hidden = !permiteEditar();
        editar.disabled = !permiteEditar();
        celdaAccion.appendChild(editar);

        var estado = crear('button', 'cd-btn cd-btn--mini cd-btn--secundario', 'Cambiar estado');
        estado.type = 'button';
        estado.dataset.permiso = 'editar';
        estado.asignacion = asignacion;
        estado.setAttribute('aria-label', 'Cambiar el estado de la asignación ' + id);
        estado.addEventListener('click', function () {
            cambiarEstadoAsignacion(this.asignacion);
        });
        estado.hidden = !permiteEditar();
        estado.disabled = !permiteEditar();
        celdaAccion.appendChild(estado);

        tr.appendChild(celdaAccion);
        return tr;
    }

    function fechaCorta(iso) {
        var texto = textoComprobado(iso);
        var match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(texto);
        if (match) {
            return match[3] + '/' + match[2] + ' ' + match[4] + ':' + match[5];
        }
        return mostrable(texto);
    }

    function renderizarPaginacionAsig() {
        var contenedor = nodo(ID.asigtabPaginacion);
        if (!contenedor) {
            return;
        }
        vaciar(contenedor);

        if (totalPaginasAsig <= 1) {
            contenedor.hidden = true;
            return;
        }
        contenedor.hidden = false;

        contenedor.appendChild(botonPaginaAsig('\u00ab', 1, 'Primera página', paginaAsig <= 1, false));
        contenedor.appendChild(botonPaginaAsig('\u2039', paginaAsig - 1, 'Página anterior', paginaAsig <= 1, false));

        var desde = Math.max(1, paginaAsig - 2);
        var hasta = Math.min(totalPaginasAsig, desde + 4);
        desde = Math.max(1, hasta - 4);
        for (var p = desde; p <= hasta; p++) {
            contenedor.appendChild(botonPaginaAsig(String(p), p, 'Página ' + p, false, p === paginaAsig));
        }

        contenedor.appendChild(botonPaginaAsig('\u203a', paginaAsig + 1, 'Página siguiente', paginaAsig >= totalPaginasAsig, false));
        contenedor.appendChild(botonPaginaAsig('\u00bb', totalPaginasAsig, 'Última página', paginaAsig >= totalPaginasAsig, false));
    }

    // --- Modal de asignación -------------------------------------------------

    function paraInputFechaHora(iso) {
        var texto = textoComprobado(iso);
        return texto.length >= 16 ? texto.slice(0, 16) : texto;
    }

    function abrirModalAsignacion(asignacion) {
        if (asignacion ? !permiteEditar() : !permiteCrear()) {
            aviso(ID.asigtabAviso, TEXTO_SIN_PERMISO, 'error');
            return;
        }
        asignacionEditando = asignacion || null;

        ponerTexto(ID.asignacionTitulo, asignacion ? 'Editar asignación' : 'Nueva asignación');

        sincronizarEmpleados();

        var empleado = nodo(ID.asignacionEmpleado);
        var codigo = nodo(ID.asignacionCodigo);
        var tipo = nodo(ID.asignacionTipo);
        var prioridad = nodo(ID.asignacionPrioridad);
        var inicio = nodo(ID.asignacionInicio);
        var fin = nodo(ID.asignacionFin);
        var observaciones = nodo(ID.asignacionObservaciones);

        if (asignacion) {
            if (empleado) {
                empleado.value = String(aEntero(valor(asignacion, 'idEmpleado'), 0));
            }
            if (codigo) {
                var idCodigo = valor(asignacion, 'idCodigo');
                codigo.value = idCodigo === null || idCodigo === undefined ? '' : String(idCodigo);
            }
            if (tipo) {
                tipo.value = textoComprobado(valor(asignacion, 'tipoTarea'));
            }
            if (prioridad) {
                prioridad.value = textoComprobado(valor(asignacion, 'prioridad'));
            }
            if (inicio) {
                inicio.value = paraInputFechaHora(valor(asignacion, 'fechaInicio'));
            }
            if (fin) {
                fin.value = paraInputFechaHora(valor(asignacion, 'fechaFin'));
            }
        } else {
            if (empleado && seleccion) {
                empleado.value = String(aEntero(valor(seleccion, 'idEmpleado'), 0));
            }
            if (codigo) {
                codigo.value = '';
            }
            if (inicio) {
                inicio.value = '';
            }
            if (fin) {
                fin.value = '';
            }
        }
        if (observaciones) {
            observaciones.value = '';
        }

        aviso(ID.asignacionError, '', 'error');
        mostrar(nodo(ID.modalAsignacion), true);
    }

    function cerrarModalAsignacion() {
        mostrar(nodo(ID.modalAsignacion), false);
        asignacionEditando = null;
    }

    function guardarAsignacion(evento) {
        if (evento && typeof evento.preventDefault === 'function') {
            evento.preventDefault();
        }
        if (ocupado) {
            return;
        }

        var empleado = aEntero(valorCampo(ID.asignacionEmpleado), 0);
        var tipo = valorCampo(ID.asignacionTipo);
        var prioridad = valorCampo(ID.asignacionPrioridad);
        var inicio = valorCampo(ID.asignacionInicio);
        var fin = valorCampo(ID.asignacionFin);
        var codigo = valorCampo(ID.asignacionCodigo);
        var observaciones = valorCampo(ID.asignacionObservaciones);

        if (!(empleado > 0)) {
            aviso(ID.asignacionError, 'Seleccione un empleado.', 'error');
            return;
        }
        if (tipo === '') {
            aviso(ID.asignacionError, 'Seleccione el tipo de tarea.', 'error');
            return;
        }
        if (inicio === '' || fin === '') {
            aviso(ID.asignacionError, 'Indique la fecha de inicio y de fin.', 'error');
            return;
        }
        if (inicio >= fin) {
            aviso(ID.asignacionError, 'La fecha de inicio debe ser anterior a la fecha de fin.', 'error');
            return;
        }
        if (codigo !== '' && !/^\d+$/.test(codigo)) {
            aviso(ID.asignacionError, 'El código fijo debe ser un número entero.', 'error');
            return;
        }

        // Estado inicial exigido por el backend para un alta; en edición se conserva el actual.
        var estado = asignacionEditando
            ? (textoComprobado(valor(asignacionEditando, 'estado')) || 'Asignada')
            : 'Asignada';

        var cuerpo = {
            idEmpleado: empleado,
            idCodigo: codigo === '' ? null : Number(codigo),
            tipoTarea: tipo,
            fechaInicio: inicio,
            fechaFin: fin,
            estado: estado,
            prioridad: prioridad,
            observaciones: observaciones === '' ? null : observaciones
        };

        var esEdicion = !!asignacionEditando;
        var ruta = esEdicion
            ? RUTA_ASIGNACIONES + '/' + encodeURIComponent(aEntero(valor(asignacionEditando, 'idAsignacion'), 0))
            : RUTA_ASIGNACIONES;

        aviso(ID.asignacionError, '', 'error');
        ocupado = true;

        llamar(ruta, { metodo: esEdicion ? 'PUT' : 'POST', cuerpo: cuerpo }).then(function (resultado) {
            ocupado = false;

            if (resultado.status === 401) {
                return;
            }
            if (resultado.status === 403) {
                mostrar(nodo(ID.modalAsignacion), false);
                marcarSoloLectura();
                aviso(ID.asigtabAviso, TEXTO_SIN_PERMISO, 'error');
                mostrarToast(TEXTO_SIN_PERMISO, 'error');
                return;
            }
            if (!resultado.ok) {
                var mensaje = mensajeError(resultado, TEXTO_ERROR);
                if (resultado.status === 409) {
                    aviso(ID.asignacionError, 'Conflicto: ' + mensaje, 'error');
                    mostrarToast('Asignación en conflicto: ' + mensaje, 'aviso');
                    return;
                }
                aviso(ID.asignacionError, mensaje, 'error');
                return;
            }

            cerrarModalAsignacion();
            mostrarToast(esEdicion ? 'Asignación actualizada correctamente' : 'Asignación creada correctamente', 'exito');
            if (tabActivo === 'asignaciones') {
                consultarAsignaciones(paginaAsig);
            }
            if (seleccion) {
                cargarAsignacionesDia(aEntero(valor(seleccion, 'idEmpleado'), 0));
            }
        });
    }

    // Ciclo de estados permitido por el backend: Asignada -> En Proceso -> Finalizada -> Asignada.
    var CICLO_ESTADO = { 'asignada': 'En Proceso', 'en proceso': 'Finalizada', 'finalizada': 'Asignada', 'cancelada': 'Asignada' };

    function cambiarEstadoAsignacion(asignacion) {
        if (!permiteEditar() || !asignacion || ocupado) {
            return;
        }
        var actual = textoComprobado(valor(asignacion, 'estado')).toLowerCase();
        var siguiente = CICLO_ESTADO[actual] || 'Asignada';
        var id = aEntero(valor(asignacion, 'idAsignacion'), 0);

        ocupado = true;
        llamar(RUTA_ASIGNACIONES + '/' + encodeURIComponent(id) + '/estado', {
            metodo: 'PATCH',
            cuerpo: { estado: siguiente }
        }).then(function (resultado) {
            ocupado = false;
            if (resultado.status === 401) {
                return;
            }
            if (resultado.status === 403) {
                marcarSoloLectura();
                aviso(ID.asigtabAviso, TEXTO_SIN_PERMISO, 'error');
                return;
            }
            if (!resultado.ok) {
                aviso(ID.asigtabAviso, mensajeError(resultado, TEXTO_ERROR), 'error');
                return;
            }
            mostrarToast('Estado actualizado a ' + siguiente, 'exito');
            if (tabActivo === 'asignaciones') {
                consultarAsignaciones(paginaAsig);
            }
            if (seleccion) {
                cargarAsignacionesDia(aEntero(valor(seleccion, 'idEmpleado'), 0));
            }
        });
    }

    // --- Enlace de eventos e inicialización ----------------------------------

    function enlazar(id, tipoEvento, manejador) {
        var elemento = nodo(id);
        if (!elemento) {
            return;
        }
        var marca = 'data-cd-' + tipoEvento;
        if (elemento.getAttribute(marca) === '1') {
            return;
        }
        elemento.setAttribute(marca, '1');
        elemento.addEventListener(tipoEvento, manejador);
    }

    function limpiarFiltros() {
        var busqueda = nodo(ID.busqueda);
        if (busqueda) {
            busqueda.value = '';
        }
        var estado = nodo(ID.estado);
        if (estado) {
            estado.value = '';
        }
        var fecha = nodo(ID.fecha);
        if (fecha) {
            fecha.value = hoyIso();
        }
        var hora = nodo(ID.hora);
        if (hora) {
            hora.value = horaActual();
        }
        limite = LIMITE_INICIAL;
        var combo = nodo(ID.limite);
        if (combo) {
            combo.value = String(LIMITE_INICIAL);
        }
        seleccion = null;
        detalleVacio(null);
        consultarDisponibilidad();
    }

    function limpiarFiltrosAsignaciones() {
        var campos = [ID.asigtabEmpleado, ID.asigtabEstado, ID.asigtabTipo, ID.asigtabFecha];
        for (var i = 0; i < campos.length; i++) {
            var campo = nodo(campos[i]);
            if (campo) {
                campo.value = '';
            }
        }
        consultarAsignaciones(PAGINA_INICIAL);
    }

    function enlazarVistas() {
        enlazar(ID.tabResumen, 'click', function () { activarTab('resumen'); });
        enlazar(ID.tabHorarios, 'click', function () { activarTab('horarios'); });
        enlazar(ID.tabAsignaciones, 'click', function () { activarTab('asignaciones'); });

        enlazar(ID.subtabHorario, 'click', function () { activarSubpanel('horario'); });
        enlazar(ID.subtabAsignaciones, 'click', function () { activarSubpanel('asignaciones'); });

        var filtros = nodo(ID.filtros);
        if (filtros && filtros.getAttribute('data-cd-submit') !== '1') {
            filtros.setAttribute('data-cd-submit', '1');
            filtros.addEventListener('submit', function (evento) {
                evento.preventDefault();
                consultarDisponibilidad();
            });
        }
        enlazar(ID.limpiar, 'click', limpiarFiltros);
        enlazar(ID.limite, 'change', cambiarLimite);

        enlazar(ID.horarioPrev, 'click', function () { moverHorario(-1); });
        enlazar(ID.horarioNext, 'click', function () { moverHorario(1); });

        enlazar(ID.nuevaAsignacion, 'click', function () { abrirModalAsignacion(null); });

        enlazar(ID.horSelect, 'change', cargarSemanaHorarios);
        enlazar(ID.horRecargar, 'click', cargarSemanaHorarios);

        var filtrosAsig = nodo(ID.asigtabFiltros);
        if (filtrosAsig && filtrosAsig.getAttribute('data-cd-submit') !== '1') {
            filtrosAsig.setAttribute('data-cd-submit', '1');
            filtrosAsig.addEventListener('submit', function (evento) {
                evento.preventDefault();
                consultarAsignaciones(PAGINA_INICIAL);
            });
        }
        enlazar(ID.asigtabLimpiar, 'click', limpiarFiltrosAsignaciones);
        enlazar(ID.asigtabNueva, 'click', function () { abrirModalAsignacion(null); });

        var formHorario = nodo(ID.horarioForm);
        if (formHorario && formHorario.getAttribute('data-cd-submit') !== '1') {
            formHorario.setAttribute('data-cd-submit', '1');
            formHorario.addEventListener('submit', guardarHorario);
        }
        enlazar(ID.horarioCancelar, 'click', cerrarModalHorario);
        enlazar(ID.horarioCerrar, 'click', cerrarModalHorario);

        var formAsignacion = nodo(ID.asignacionForm);
        if (formAsignacion && formAsignacion.getAttribute('data-cd-submit') !== '1') {
            formAsignacion.setAttribute('data-cd-submit', '1');
            formAsignacion.addEventListener('submit', guardarAsignacion);
        }
        enlazar(ID.asignacionCancelar, 'click', cerrarModalAsignacion);
        enlazar(ID.asignacionCerrar, 'click', cerrarModalAsignacion);
    }

    function prepararFechas() {
        var fecha = nodo(ID.fecha);
        if (fecha && !fecha.value) {
            fecha.value = hoyIso();
        }
        var hora = nodo(ID.hora);
        if (hora && !hora.value) {
            hora.value = horaActual();
        }
    }

    function iniciar(opciones) {
        if (iniciado) {
            return;
        }
        iniciado = true;

        if (opciones) {
            establecerPermisos(opciones);
        }
        leerPermisosDeclarados();
        aplicarPermisos();

        activarTab('resumen');
        activarSubpanel('horario');
        prepararFechas();
        enlazarVistas();
        consultarDisponibilidad();
    }

    ns.disponibilidadPersonal = {
        iniciar: iniciar,
        establecerPermisos: establecerPermisos,
        activarTab: activarTab,
        activarSubpanel: activarSubpanel,
        consultarDisponibilidad: consultarDisponibilidad,
        consultarAsignaciones: consultarAsignaciones,
        seleccionar: seleccionar,
        moverHorario: moverHorario,
        irAPagina: irAPagina,
        cambiarLimite: cambiarLimite,
        limpiarFiltros: limpiarFiltros
    };
})(window.VisorSIG);
