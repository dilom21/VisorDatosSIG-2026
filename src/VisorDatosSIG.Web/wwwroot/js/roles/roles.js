// FE-CU03 / FE-CU04: administración de roles y permisos (pantalla /Roles).
//
// La Web consume exclusivamente la API con el token de la sesión:
//   GET    api/roles                -> roles administrables (el migrador nunca llega)
//   GET    api/roles/{id}           -> detalle del rol seleccionado
//   POST   api/roles                -> crear rol
//   PATCH  api/roles/{id}           -> editar nombre y descripción
//   PATCH  api/roles/{id}/estado    -> habilitar o deshabilitar (nunca eliminar)
//   GET    api/roles/{id}/permisos  -> matriz vigente (filas planas con idMenuPadre)
//   PUT    api/roles/{id}/permisos  -> reemplazo total de la matriz en una sola petición
//
// Nada está hardcodeado: los roles, la jerarquía de menús y los permisos llegan del servidor,
// de modo que una fila nueva en dbo.MenuOpciones aparece aquí sin tocar Razor ni JavaScript.
// El rol del sistema (Administrador) se reconoce por `esRolSistema`, nunca por IdRol, y su
// matriz se muestra con todo concedido y en solo lectura porque la API prohíbe editarla.
//
// Seguridad del DOM: todo nodo se crea con document.createElement y los textos se asignan con
// textContent; jamás se usa innerHTML con datos del backend.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var RUTA_ROLES = 'api/roles';
    var ACCIONES = ['ver', 'crear', 'editar', 'eliminar'];
    var PROPIEDAD = {
        ver: 'puedeVer',
        crear: 'puedeCrear',
        editar: 'puedeEditar',
        eliminar: 'puedeEliminar'
    };
    var ETIQUETA_ACCION = {
        ver: 'Ver',
        crear: 'Crear',
        editar: 'Editar',
        eliminar: 'Eliminar'
    };

    var ID_LISTA = 'roles-lista';
    var ID_LISTA_ESTADO = 'roles-lista-estado';
    var ID_DETALLE = 'roles-detalle';
    var ID_MATRIZ_ROL = 'roles-matriz-rol';
    var ID_MATRIZ_NOTA = 'roles-matriz-nota';
    var ID_MATRIZ_BADGES = 'roles-matriz-badges';
    var ID_MATRIZ_ESTADO = 'roles-matriz-estado';
    var ID_MATRIZ_CUERPO = 'roles-matriz-cuerpo';
    var ID_AVISO = 'roles-aviso';
    var ID_BOTON_NUEVO = 'btn-nuevo-rol';
    var ID_BOTON_EDITAR = 'btn-editar-rol';
    var ID_BOTON_ESTADO = 'btn-estado-rol';
    var ID_BOTON_GUARDAR = 'btn-guardar-permisos';
    var ID_BOTON_DESCARTAR = 'btn-descartar-permisos';

    // Estado del módulo.
    var roles = [];
    var rolSeleccionado = null;
    var matrizOriginal = [];
    var matrizActual = [];
    var hayCambios = false;
    var ocupado = false;
    // Protege contra respuestas obsoletas si el usuario cambia de rol rápidamente.
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

    // Lectura tolerante al casing: la API usa camelCase y un cambio de configuración de
    // serialización no debe romper la pantalla.
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

    function texto(valorComprobado, alterno) {
        return typeof valorComprobado === 'string' && valorComprobado !== ''
            ? valorComprobado
            : alterno;
    }

    function aEntero(valorComprobado) {
        var numero = Number(valorComprobado);
        return isFinite(numero) ? Math.floor(numero) : 0;
    }

    function aBandera(valorComprobado) {
        return valorComprobado === true || valorComprobado === 1 || valorComprobado === 'true';
    }

    // Mensaje legible de un error de la API (ProblemDetails o ValidationProblemDetails).
    function mensajeError(resultado, alterno) {
        var cuerpo = resultado ? resultado.datos : null;

        if (cuerpo && typeof cuerpo === 'object') {
            var detalle = texto(valor(cuerpo, 'detail'), '');
            var titulo = texto(valor(cuerpo, 'title'), '');
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

        var clase = 'visor-aviso roles__aviso';
        if (tono === 'error') {
            clase += ' roles__aviso--error';
        } else if (tono === 'exito') {
            clase += ' roles__aviso--exito';
        } else {
            clase += ' roles__aviso--info';
        }

        zona.className = clase;
        zona.textContent = mensaje || '';
        zona.hidden = !mensaje;
    }

    // Petición protegida: el 401 queda a cargo del flujo existente (api.js + guard.js).
    function llamar(ruta, metodo, cuerpo) {
        var token = ns.sesion ? ns.sesion.obtenerToken() : null;
        if (!token) {
            return Promise.resolve({ ok: false, status: 401, datos: null, error: null });
        }

        return ns.api.peticion(ruta, {
            metodo: metodo || 'GET',
            cuerpo: cuerpo || null,
            token: token,
            protegida: true
        });
    }

    // --- Normalización de los contratos de la API ---------------------------

    // GET api/roles -> { idRol, nombreRol, descripcion, activo, esRolSistema,
    //                    cantidadUsuariosActivos }
    function normalizarRol(dto) {
        return {
            idRol: aEntero(valor(dto, 'idRol')),
            nombre: texto(valor(dto, 'nombreRol'), ''),
            descripcion: texto(valor(dto, 'descripcion'), ''),
            activo: valor(dto, 'activo') !== false,
            esRolSistema: aBandera(valor(dto, 'esRolSistema')),
            usuarios: aEntero(valor(dto, 'cantidadUsuariosActivos'))
        };
    }

    // GET api/roles/{id}/permisos -> filas planas con idMenuPadre, orden y los cuatro permisos.
    function normalizarFila(fila) {
        var padre = valor(fila, 'idMenuPadre');

        return {
            idMenu: aEntero(valor(fila, 'idMenu')),
            idMenuPadre: padre === null || padre === undefined ? null : aEntero(padre),
            nombre: texto(valor(fila, 'nombreMenu'), ''),
            url: texto(valor(fila, 'url'), ''),
            esAgrupador: aBandera(valor(fila, 'esAgrupador')),
            orden: aEntero(valor(fila, 'orden')),
            puedeVer: aBandera(valor(fila, 'puedeVer')),
            puedeCrear: aBandera(valor(fila, 'puedeCrear')),
            puedeEditar: aBandera(valor(fila, 'puedeEditar')),
            puedeEliminar: aBandera(valor(fila, 'puedeEliminar'))
        };
    }

    function normalizarMatriz(dto) {
        var filas = valor(dto, 'menus');
        if (!esArreglo(filas)) {
            return [];
        }

        var matriz = [];
        for (var i = 0; i < filas.length; i++) {
            matriz.push(normalizarFila(filas[i]));
        }
        return matriz;
    }

    function clonarMatriz(matriz) {
        var copia = [];
        for (var i = 0; i < matriz.length; i++) {
            copia.push({
                idMenu: matriz[i].idMenu,
                idMenuPadre: matriz[i].idMenuPadre,
                nombre: matriz[i].nombre,
                url: matriz[i].url,
                esAgrupador: matriz[i].esAgrupador,
                orden: matriz[i].orden,
                puedeVer: matriz[i].puedeVer,
                puedeCrear: matriz[i].puedeCrear,
                puedeEditar: matriz[i].puedeEditar,
                puedeEliminar: matriz[i].puedeEliminar
            });
        }
        return copia;
    }

    function tieneAlgunPermiso(fila) {
        return fila.puedeVer || fila.puedeCrear || fila.puedeEditar || fila.puedeEliminar;
    }

    // Ordena la matriz plana como árbol padre -> hijos y devuelve cada fila con su nivel.
    // La jerarquía se resuelve solo por idMenuPadre y el orden por `orden`, igual que el menú
    // lateral: así la pantalla refleja cualquier opción nueva sin cambios en el cliente.
    function ordenarJerarquia(matriz) {
        var hijos = {};
        var raices = [];
        var recorridas = [];
        var i;

        for (i = 0; i < matriz.length; i++) {
            var fila = matriz[i];
            var clave = fila.idMenuPadre === null ? null : String(fila.idMenuPadre);

            if (clave === null) {
                raices.push(fila);
                continue;
            }

            if (!Object.prototype.hasOwnProperty.call(hijos, clave)) {
                hijos[clave] = [];
            }
            hijos[clave].push(fila);
        }

        function porOrden(a, b) {
            return a.orden === b.orden ? a.idMenu - b.idMenu : a.orden - b.orden;
        }

        function recorrer(fila, nivel) {
            recorridas.push({ fila: fila, nivel: nivel });
            var descendientes = hijos[String(fila.idMenu)];
            if (!descendientes) {
                return;
            }
            descendientes.sort(porOrden);
            for (var j = 0; j < descendientes.length; j++) {
                recorrer(descendientes[j], nivel + 1);
            }
        }

        raices.sort(porOrden);
        for (i = 0; i < raices.length; i++) {
            recorrer(raices[i], 0);
        }

        // Ninguna fila del contrato puede perderse al guardar: si alguna quedara fuera del
        // árbol (dato inconsistente), se muestra al final en lugar de descartarse.
        if (recorridas.length !== matriz.length) {
            for (i = 0; i < matriz.length; i++) {
                if (!contiene(recorridas, matriz[i])) {
                    recorridas.push({ fila: matriz[i], nivel: 0 });
                }
            }
        }

        return recorridas;
    }

    function contiene(niveles, fila) {
        for (var i = 0; i < niveles.length; i++) {
            if (niveles[i].fila.idMenu === fila.idMenu) {
                return true;
            }
        }
        return false;
    }

    // --- Presentación del listado y del panel -------------------------------

    function crearBadge(textoBadge, modificador) {
        var badge = crear('span',
            'roles__badge' + (modificador ? ' roles__badge--' + modificador : ''),
            textoBadge);
        return badge;
    }

    // 403 de la API: la autorización es del servidor, así que no se muestra ningún dato.
    function mostrarBloqueo() {
        var zonaAviso = nodo(ID_AVISO);
        var zona = document.querySelector('.roles__grid');

        if (zona) {
            zona.hidden = true;
        }

        if (zonaAviso) {
            zonaAviso.className = 'visor-aviso roles__aviso roles__aviso--error';
            zonaAviso.textContent = 'No tiene permisos para acceder a esta funcionalidad.';
            zonaAviso.hidden = false;
        }
    }

    function estadoLista(mensaje) {
        var zona = nodo(ID_LISTA_ESTADO);
        if (!zona) {
            return;
        }

        zona.textContent = mensaje || '';
        zona.hidden = !mensaje;
    }

    function renderizarLista() {
        var lista = nodo(ID_LISTA);
        if (!lista) {
            return;
        }

        vaciar(lista);

        if (roles.length === 0) {
            estadoLista('No hay roles registrados.');
            return;
        }

        estadoLista('');

        for (var i = 0; i < roles.length; i++) {
            lista.appendChild(crearItemRol(roles[i]));
        }
    }

    function crearItemRol(rol) {
        var item = crear('li', 'roles__item', null);

        var boton = crear('button', 'roles__rol', null);
        boton.type = 'button';
        boton.setAttribute('data-id-rol', String(rol.idRol));
        boton.setAttribute('aria-pressed',
            rolSeleccionado && rolSeleccionado.idRol === rol.idRol ? 'true' : 'false');

        boton.appendChild(crear('span', 'roles__rol-nombre', rol.nombre));

        var meta = crear('span', 'roles__rol-meta', null);
        meta.appendChild(crearBadge(rol.activo ? 'Activo' : 'Inactivo',
            rol.activo ? 'activo' : 'inactivo'));
        if (rol.esRolSistema) {
            meta.appendChild(crearBadge('Rol del sistema', 'sistema'));
        }
        meta.appendChild(crear('span', 'roles__rol-usuarios',
            rol.usuarios === 1 ? '1 usuario activo' : rol.usuarios + ' usuarios activos'));
        boton.appendChild(meta);

        boton.addEventListener('click', function () {
            seleccionarRol(rol.idRol);
        });

        item.appendChild(boton);
        return item;
    }

    function renderizarDetalle() {
        var contenedor = nodo(ID_DETALLE);
        if (!contenedor) {
            return;
        }

        vaciar(contenedor);

        if (!rolSeleccionado) {
            contenedor.hidden = true;
            return;
        }

        contenedor.hidden = false;

        contenedor.appendChild(crear('h3', 'roles__detalle-titulo', 'Detalle del rol'));
        contenedor.appendChild(crear('p', 'roles__detalle-descripcion',
            rolSeleccionado.descripcion !== '' ? rolSeleccionado.descripcion
                : 'Sin descripción registrada.'));

        var lista = crear('dl', 'roles__detalle-lista', null);

        var estado = crear('div', 'roles__detalle-fila', null);
        estado.appendChild(crear('dt', 'roles__detalle-clave', 'Estado'));
        var valorEstado = crear('dd', 'roles__detalle-valor', null);
        valorEstado.appendChild(crearBadge(rolSeleccionado.activo ? 'Activo' : 'Inactivo',
            rolSeleccionado.activo ? 'activo' : 'inactivo'));
        estado.appendChild(valorEstado);
        lista.appendChild(estado);

        var usuarios = crear('div', 'roles__detalle-fila', null);
        usuarios.appendChild(crear('dt', 'roles__detalle-clave', 'Usuarios activos'));
        usuarios.appendChild(crear('dd', 'roles__detalle-valor',
            String(rolSeleccionado.usuarios)));
        lista.appendChild(usuarios);

        contenedor.appendChild(lista);

        if (rolSeleccionado.esRolSistema) {
            contenedor.appendChild(crear('p', 'roles__detalle-nota',
                'Rol del sistema: su acceso es total e implícito. No se renombra, '
                + 'no se deshabilita y sus permisos no se editan.'));
        }
    }

    function renderizarBadges() {
        var zona = nodo(ID_MATRIZ_BADGES);
        if (!zona) {
            return;
        }

        vaciar(zona);

        if (!rolSeleccionado) {
            return;
        }

        zona.appendChild(crearBadge(rolSeleccionado.activo ? 'Activo' : 'Inactivo',
            rolSeleccionado.activo ? 'activo' : 'inactivo'));

        if (rolSeleccionado.esRolSistema) {
            zona.appendChild(crearBadge('Rol del sistema', 'sistema'));
        }
    }

    function estadoMatriz(mensaje, tono) {
        var zona = nodo(ID_MATRIZ_ESTADO);
        if (!zona) {
            return;
        }

        zona.textContent = mensaje || '';
        zona.className = tono ? 'roles__estado roles__estado--' + tono : 'roles__estado';
    }

    // --- Matriz de permisos -------------------------------------------------

    function renderizarMatriz() {
        var cuerpo = nodo(ID_MATRIZ_CUERPO);
        var tituloRol = nodo(ID_MATRIZ_ROL);
        var nota = nodo(ID_MATRIZ_NOTA);

        if (tituloRol) {
            tituloRol.textContent = rolSeleccionado ? rolSeleccionado.nombre : '';
        }

        if (nota) {
            if (rolSeleccionado && rolSeleccionado.esRolSistema) {
                nota.textContent = 'Rol del sistema: todo concedido y en solo lectura. '
                    + 'La API no admite cambios en esta matriz.';
                nota.hidden = false;
            } else {
                nota.textContent = '';
                nota.hidden = true;
            }
        }

        if (!cuerpo) {
            return;
        }

        vaciar(cuerpo);

        var soloLectura = !rolSeleccionado || rolSeleccionado.esRolSistema;
        var niveles = ordenarJerarquia(matrizActual);

        for (var i = 0; i < niveles.length; i++) {
            cuerpo.appendChild(crearFilaMatriz(niveles[i], soloLectura));
        }

        actualizarBotones();
    }

    function crearFilaMatriz(nivel, soloLectura) {
        var fila = nivel.fila;
        var tr = crear('tr', fila.esAgrupador
            ? 'roles__fila roles__fila--agrupador'
            : 'roles__fila', null);

        var celda = crear('th', 'roles__celda-opcion', null);
        celda.setAttribute('scope', 'row');
        // La sangría se calcula con el nivel real de la jerarquía (sirve para cualquier
        // profundidad de menú, no solo para la actual).
        celda.style.setProperty('--roles-nivel', String(nivel.nivel));

        celda.appendChild(crear('span', 'roles__opcion-nombre', fila.nombre));

        if (fila.esAgrupador) {
            celda.appendChild(crear('span', 'roles__opcion-tipo', 'agrupador'));
        }
        if (fila.url !== '') {
            celda.appendChild(crear('span', 'roles__opcion-ruta', fila.url));
        }

        tr.appendChild(celda);

        for (var j = 0; j < ACCIONES.length; j++) {
            tr.appendChild(crearCeldaPermiso(fila, ACCIONES[j], soloLectura));
        }

        return tr;
    }

    function crearCeldaPermiso(fila, accion, soloLectura) {
        var td = crear('td', 'roles__celda-permiso', null);

        var casilla = document.createElement('input');
        casilla.type = 'checkbox';
        casilla.className = 'roles__casilla';
        casilla.checked = fila[PROPIEDAD[accion]] === true;
        casilla.disabled = soloLectura === true;
        casilla.setAttribute('data-id-menu', String(fila.idMenu));
        casilla.setAttribute('data-accion', accion);
        casilla.setAttribute('aria-label', ETIQUETA_ACCION[accion] + ': ' + fila.nombre);
        casilla.addEventListener('change', manejarCambioPermiso);

        td.appendChild(casilla);
        return td;
    }

    function manejarCambioPermiso(evento) {
        var casilla = evento.currentTarget;
        var idMenu = aEntero(casilla.getAttribute('data-id-menu'));
        var accion = casilla.getAttribute('data-accion');
        var fila = buscarFila(matrizActual, idMenu);

        if (!fila || !PROPIEDAD[accion]) {
            return;
        }

        fila[PROPIEDAD[accion]] = casilla.checked;

        recalcularCambios();
        estadoMatriz(hayCambios ? 'Cambios sin guardar.' : 'Sin cambios pendientes.',
            hayCambios ? 'aviso' : null);
        actualizarBotones();
    }

    function buscarFila(matriz, idMenu) {
        for (var i = 0; i < matriz.length; i++) {
            if (matriz[i].idMenu === idMenu) {
                return matriz[i];
            }
        }
        return null;
    }

    function recalcularCambios() {
        var diferente = matrizActual.length !== matrizOriginal.length;

        for (var i = 0; i < matrizActual.length && !diferente; i++) {
            var actual = matrizActual[i];
            var original = buscarFila(matrizOriginal, actual.idMenu);

            if (!original) {
                diferente = true;
                break;
            }

            for (var j = 0; j < ACCIONES.length; j++) {
                if (actual[PROPIEDAD[ACCIONES[j]]] !== original[PROPIEDAD[ACCIONES[j]]]) {
                    diferente = true;
                    break;
                }
            }
        }

        hayCambios = diferente;
        return hayCambios;
    }

    function fijarBoton(id, habilitado) {
        var boton = nodo(id);
        if (boton) {
            boton.disabled = !habilitado;
        }
    }

    function actualizarBotones() {
        var seleccion = rolSeleccionado !== null;
        var sistema = seleccion && rolSeleccionado.esRolSistema === true;
        var editable = seleccion && !sistema && !ocupado;

        fijarBoton(ID_BOTON_GUARDAR, editable && hayCambios);
        fijarBoton(ID_BOTON_DESCARTAR, editable && hayCambios);
        fijarBoton(ID_BOTON_EDITAR, editable);
        fijarBoton(ID_BOTON_ESTADO, editable);
        fijarBoton(ID_BOTON_NUEVO, !ocupado);

        var botonEstado = nodo(ID_BOTON_ESTADO);
        if (botonEstado && seleccion) {
            botonEstado.textContent = rolSeleccionado.activo ? 'Deshabilitar' : 'Habilitar';
        }
    }

    // --- Carga de datos -----------------------------------------------------

    function buscarRol(idRol) {
        for (var i = 0; i < roles.length; i++) {
            if (roles[i].idRol === idRol) {
                return roles[i];
            }
        }
        return null;
    }

    function actualizarRolEnLista(rol) {
        for (var i = 0; i < roles.length; i++) {
            if (roles[i].idRol === rol.idRol) {
                roles[i] = rol;
                return;
            }
        }
        roles.push(rol);
    }

    function limpiarPanel() {
        rolSeleccionado = null;
        matrizActual = [];
        matrizOriginal = [];
        hayCambios = false;

        renderizarDetalle();
        renderizarBadges();
        renderizarMatriz();
        estadoMatriz('', null);
    }

    // GET api/roles. `idPreferido` conserva la selección tras crear, editar o cambiar estado.
    function cargarLista(idPreferido) {
        estadoLista('Cargando roles...');

        return llamar(RUTA_ROLES, 'GET').then(function (resultado) {
            if (resultado.status === 401) {
                // Sesión expirada: api.js y guard.js ya están redirigiendo.
                return;
            }

            if (resultado.status === 403) {
                mostrarBloqueo();
                return;
            }

            if (!resultado.ok || !esArreglo(resultado.datos)) {
                estadoLista('');
                aviso(mensajeError(resultado, 'No se pudo cargar el listado de roles.'), 'error');
                return;
            }

            var lista = [];
            for (var i = 0; i < resultado.datos.length; i++) {
                lista.push(normalizarRol(resultado.datos[i]));
            }

            roles = lista;
            renderizarLista();

            if (roles.length === 0) {
                limpiarPanel();
                aviso('No hay roles administrables registrados.', null);
                return;
            }

            var objetivo = idPreferido ? buscarRol(idPreferido) : null;
            if (!objetivo && rolSeleccionado) {
                objetivo = buscarRol(rolSeleccionado.idRol);
            }

            cargarRol(objetivo ? objetivo.idRol : roles[0].idRol);
        });
    }

    // GET api/roles/{id} + GET api/roles/{id}/permisos: detalle autorizado y matriz vigente.
    function cargarRol(idRol) {
        if (!idRol) {
            return;
        }

        secuencia += 1;
        var actual = secuencia;

        estadoMatriz('Cargando permisos...', 'aviso');

        Promise.all([
            llamar(RUTA_ROLES + '/' + idRol, 'GET'),
            llamar(RUTA_ROLES + '/' + idRol + '/permisos', 'GET')
        ]).then(function (respuestas) {
            if (actual !== secuencia) {
                // El usuario ya seleccionó otro rol: la respuesta llegó tarde.
                return;
            }

            var detalle = respuestas[0];
            var permisos = respuestas[1];

            if (detalle.status === 401 || permisos.status === 401) {
                return;
            }

            if (detalle.status === 403 || permisos.status === 403) {
                mostrarBloqueo();
                return;
            }

            if (detalle.status === 404 || permisos.status === 404) {
                rolSeleccionado = null;
                limpiarPanel();
                aviso('El rol indicado ya no existe o no se administra desde la web.', 'error');
                cargarLista(null);
                return;
            }

            if (!detalle.ok || !detalle.datos || !permisos.ok || !permisos.datos) {
                estadoMatriz('', null);
                aviso(mensajeError(detalle.ok ? permisos : detalle,
                    'No se pudo cargar la matriz de permisos del rol.'), 'error');
                return;
            }

            rolSeleccionado = normalizarRol(detalle.datos);
            actualizarRolEnLista(rolSeleccionado);

            matrizActual = normalizarMatriz(permisos.datos);
            matrizOriginal = clonarMatriz(matrizActual);
            hayCambios = false;

            renderizarLista();
            renderizarDetalle();
            renderizarBadges();
            renderizarMatriz();

            estadoMatriz(rolSeleccionado.esRolSistema
                ? 'Solo lectura: rol del sistema.' : '', null);
        });
    }

    // --- Acciones del panel -------------------------------------------------

    function seleccionarRol(idRol) {
        if (rolSeleccionado && rolSeleccionado.idRol === idRol) {
            // Ya está seleccionado: no se recarga ni se pide confirmación.
            return;
        }

        if (hayCambios) {
            // Descartar cambios exige confirmación explícita (nunca window.confirm).
            ns.modal.confirmar({
                titulo: 'Cambios sin guardar',
                mensaje: 'La matriz de permisos tiene cambios sin guardar. '
                    + 'Si cambia de rol se descartarán.',
                textoConfirmar: 'Descartar y continuar',
                textoCancelar: 'Seguir editando',
                tono: 'peligro'
            }).then(function (confirmado) {
                if (confirmado !== true) {
                    return;
                }
                hayCambios = false;
                aviso('', null);
                cargarRol(idRol);
            });
            return;
        }

        aviso('', null);
        cargarRol(idRol);
    }

    function descartarPermisos() {
        if (!rolSeleccionado || rolSeleccionado.esRolSistema || !hayCambios || ocupado) {
            return;
        }

        matrizActual = clonarMatriz(matrizOriginal);
        hayCambios = false;

        renderizarMatriz();
        estadoMatriz('Cambios descartados.', null);
    }

    // Un único PUT con la matriz completa: el reemplazo del backend es total e idempotente.
    function guardarPermisos() {
        if (!rolSeleccionado || rolSeleccionado.esRolSistema || !hayCambios || ocupado) {
            return;
        }

        var permisos = [];
        for (var i = 0; i < matrizActual.length; i++) {
            var fila = matrizActual[i];
            if (!tieneAlgunPermiso(fila)) {
                // La ausencia de fila significa lo mismo en dbo.RolMenu.
                continue;
            }
            permisos.push({
                idMenu: fila.idMenu,
                puedeVer: fila.puedeVer === true,
                puedeCrear: fila.puedeCrear === true,
                puedeEditar: fila.puedeEditar === true,
                puedeEliminar: fila.puedeEliminar === true
            });
        }

        var idRol = rolSeleccionado.idRol;
        ocupado = true;
        actualizarBotones();
        estadoMatriz('Guardando permisos...', 'aviso');

        llamar(RUTA_ROLES + '/' + idRol + '/permisos', 'PUT', { permisos: permisos })
            .then(function (resultado) {
                ocupado = false;

                if (resultado.status === 401) {
                    return;
                }

                if (resultado.status === 403) {
                    mostrarBloqueo();
                    return;
                }

                if (resultado.status === 404) {
                    rolSeleccionado = null;
                    limpiarPanel();
                    aviso('El rol indicado ya no existe o no se administra desde la web.', 'error');
                    cargarLista(null);
                    return;
                }

                if (!resultado.ok) {
                    // 400 (matriz inválida) o 409 (rol protegido): se explica el motivo real.
                    actualizarBotones();
                    estadoMatriz('', null);
                    aviso(mensajeError(resultado, 'No se pudieron guardar los permisos.'), 'error');
                    return;
                }

                if (resultado.datos) {
                    matrizActual = normalizarMatriz(resultado.datos);
                    matrizOriginal = clonarMatriz(matrizActual);
                }

                hayCambios = false;
                renderizarMatriz();
                estadoMatriz('Permisos guardados.', 'exito');
                aviso('Los permisos del rol se guardaron correctamente.', 'exito');
            });
    }

    // --- Alta, edición y estado del rol -------------------------------------

    function rolDesaparecio() {
        rolSeleccionado = null;
        limpiarPanel();
        aviso('El rol indicado ya no existe o no se administra desde la web.', 'error');
        cargarLista(null);
    }

    // Traduce el resultado de POST/PATCH de rol y refresca el listado conservando la selección.
    function procesarGuardadoRol(resultado, mensajeExito) {
        if (resultado.status === 401) {
            ns.modal.cerrar();
            return false;
        }

        if (resultado.status === 403) {
            ns.modal.cerrar();
            mostrarBloqueo();
            return false;
        }

        if (!resultado.ok) {
            // 400 (datos inválidos o nombre reservado) y 409 (nombre duplicado).
            return mensajeError(resultado, 'No se pudo guardar el rol.');
        }

        var rol = resultado.datos ? normalizarRol(resultado.datos) : null;
        aviso(mensajeExito, 'exito');

        return cargarLista(rol ? rol.idRol : null).then(function () {
            return true;
        });
    }

    function abrirNuevoRol() {
        if (ocupado) {
            return;
        }

        ns.modal.formulario({
            titulo: 'Nuevo rol',
            subtitulo: 'El rol se crea habilitado y sin permisos: asígnelos después.',
            textoConfirmar: 'Crear rol',
            campos: [
                {
                    nombre: 'nombreRol',
                    etiqueta: 'Nombre',
                    valor: '',
                    requerido: true,
                    longitudMaxima: 50,
                    ayuda: 'Obligatorio, máximo 50 caracteres.'
                },
                {
                    nombre: 'descripcion',
                    etiqueta: 'Descripción',
                    valor: '',
                    tipo: 'textarea',
                    longitudMaxima: 200,
                    ayuda: 'Opcional, máximo 200 caracteres.'
                }
            ],
            alConfirmar: function (valores) {
                return llamar(RUTA_ROLES, 'POST', {
                    nombreRol: valores.nombreRol,
                    descripcion: valores.descripcion
                }).then(function (resultado) {
                    return procesarGuardadoRol(resultado, 'Rol creado correctamente.');
                });
            }
        });
    }

    function abrirEditarRol() {
        if (!rolSeleccionado || rolSeleccionado.esRolSistema || ocupado) {
            return;
        }

        var rol = rolSeleccionado;

        ns.modal.formulario({
            titulo: 'Editar rol',
            subtitulo: rol.nombre,
            textoConfirmar: 'Guardar cambios',
            campos: [
                {
                    nombre: 'nombreRol',
                    etiqueta: 'Nombre',
                    valor: rol.nombre,
                    requerido: true,
                    longitudMaxima: 50,
                    ayuda: 'Obligatorio, máximo 50 caracteres.'
                },
                {
                    nombre: 'descripcion',
                    etiqueta: 'Descripción',
                    valor: rol.descripcion,
                    tipo: 'textarea',
                    longitudMaxima: 200,
                    ayuda: 'Opcional, máximo 200 caracteres.'
                }
            ],
            alConfirmar: function (valores) {
                return llamar(RUTA_ROLES + '/' + rol.idRol, 'PATCH', {
                    nombreRol: valores.nombreRol,
                    descripcion: valores.descripcion
                }).then(function (resultado) {
                    return procesarGuardadoRol(resultado, 'Rol actualizado correctamente.');
                });
            }
        });
    }

    // PATCH api/roles/{id}/estado: baja lógica. Un rol con usuarios activos asignados
    // responde 409 y su explicación se muestra tal cual.
    function cambiarEstado() {
        if (!rolSeleccionado || rolSeleccionado.esRolSistema || ocupado) {
            return;
        }

        var rol = rolSeleccionado;
        var habilitar = !rol.activo;

        ns.modal.confirmar({
            titulo: habilitar ? 'Habilitar rol' : 'Deshabilitar rol',
            mensaje: habilitar
                ? 'El rol "' + rol.nombre + '" volverá a estar disponible para asignarse a usuarios.'
                : 'El rol "' + rol.nombre + '" quedará deshabilitado. '
                    + 'Los usuarios que solo tengan este rol perderán el acceso web. '
                    + 'El rol no se elimina: la baja es lógica.',
            textoConfirmar: habilitar ? 'Habilitar' : 'Deshabilitar',
            textoCancelar: 'Cancelar',
            tono: habilitar ? null : 'peligro'
        }).then(function (confirmado) {
            if (confirmado !== true) {
                return;
            }

            ocupado = true;
            actualizarBotones();

            llamar(RUTA_ROLES + '/' + rol.idRol + '/estado', 'PATCH', { activo: habilitar })
                .then(function (resultado) {
                    ocupado = false;

                    if (resultado.status === 401) {
                        return;
                    }

                    if (resultado.status === 403) {
                        mostrarBloqueo();
                        return;
                    }

                    if (resultado.status === 404) {
                        rolDesaparecio();
                        return;
                    }

                    if (!resultado.ok) {
                        actualizarBotones();
                        aviso(mensajeError(resultado,
                            'No se pudo cambiar el estado del rol.'), 'error');
                        return;
                    }

                    var actualizado = resultado.datos ? normalizarRol(resultado.datos) : null;
                    aviso(habilitar
                        ? 'El rol quedó habilitado.'
                        : 'El rol quedó deshabilitado.', 'exito');

                    cargarLista(actualizado ? actualizado.idRol : rol.idRol);
                });
        });
    }

    // --- Enlace e inicialización --------------------------------------------

    function enlazar(id, manejador) {
        var elemento = nodo(id);
        if (!elemento || elemento.getAttribute('data-roles-enlazado') === '1') {
            return;
        }

        elemento.setAttribute('data-roles-enlazado', '1');
        elemento.addEventListener('click', manejador);
    }

    function enlazarBotones() {
        enlazar(ID_BOTON_NUEVO, abrirNuevoRol);
        enlazar(ID_BOTON_EDITAR, abrirEditarRol);
        enlazar(ID_BOTON_ESTADO, cambiarEstado);
        enlazar(ID_BOTON_GUARDAR, guardarPermisos);
        enlazar(ID_BOTON_DESCARTAR, descartarPermisos);
    }

    function iniciar() {
        if (!ns.api || !ns.sesion || !document.getElementById(ID_LISTA)) {
            return;
        }

        enlazarBotones();
        actualizarBotones();
        cargarLista(null);
    }

    ns.roles = {
        iniciar: iniciar,
        recargar: function () {
            return cargarLista(null);
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
})(window.VisorSIG);