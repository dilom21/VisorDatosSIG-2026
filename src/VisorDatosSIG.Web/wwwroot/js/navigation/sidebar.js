// Navegación lateral dinámica del workspace autenticado.
//
// El menú NO está hardcodeado en el frontend: se obtiene de GET /api/menu, que lee
// dbo.MenuOpciones. Aquí solo se sabe cómo dibujar una colección de opciones, de modo
// que agregar un menú nuevo a la base de datos no requiere tocar Razor ni este módulo.
//
// Seguridad del renderizado: nunca se usa innerHTML. Todo nodo se crea con
// createElement / createElementNS, los textos se asignan con textContent y los
// atributos con setAttribute. El valor Icono de la base de datos se resuelve contra
// un catálogo controlado; un valor desconocido cae en el icono genérico.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var RUTA_MENU = 'api/menu';
    var SVG_NS = 'http://www.w3.org/2000/svg';
    var XLINK_NS = 'http://www.w3.org/1999/xlink';

    // Marca de enlace en los nodos ya enlazados (misma convención que guard.js):
    // hace idempotente la inicialización y evita listeners duplicados.
    var ENLAZADO = 'data-visor-sig-enlazado';

    // Catálogo controlado de iconos. La base de datos guarda únicamente la clave.
    var ICONOS = {
        'shield': 'icono-shield',
        'users': 'icono-users',
        'user-shield': 'icono-user-shield',
        'history': 'icono-history',
        'map': 'icono-map',
        'search': 'icono-search',
        'map-pin': 'icono-map-pin',
        'grid': 'icono-grid',
        'dashboard': 'icono-grid',
        'layers': 'icono-layers',
        'route': 'icono-route',
        'chart': 'icono-chart',
        'file-chart': 'icono-file-chart'
    };
    var ICONO_GENERICO = 'icono-generico';
    var ICONO_CHEVRON = 'icono-chevron';

    var MENSAJE_ERROR = 'No se pudo cargar el menú de navegación. La aplicación sigue disponible.';
    var MENSAJE_VACIO = 'No hay opciones de menú disponibles.';
    var MENSAJE_SIN_SESION = 'Inicie sesión para ver el menú.';

    // ------------------------------------------------------------------------
    // Utilidades DOM (sin innerHTML)
    // ------------------------------------------------------------------------

    function crearSvg(simbolo, clase) {
        var svg = document.createElementNS(SVG_NS, 'svg');
        svg.setAttribute('class', clase);
        svg.setAttribute('viewBox', '0 0 24 24');
        svg.setAttribute('aria-hidden', 'true');
        svg.setAttribute('focusable', 'false');

        var use = document.createElementNS(SVG_NS, 'use');
        use.setAttribute('href', '#' + simbolo);
        // Se fija también xlink:href por compatibilidad con navegadores antiguos.
        use.setAttributeNS(XLINK_NS, 'xlink:href', '#' + simbolo);
        svg.appendChild(use);

        return svg;
    }

    function crearIcono(icono) {
        var clave = typeof icono === 'string' ? icono.trim().toLowerCase() : '';
        var simbolo = Object.prototype.hasOwnProperty.call(ICONOS, clave)
            ? ICONOS[clave]
            : ICONO_GENERICO;

        return crearSvg(simbolo, 'sidebar__icono');
    }

    function crearTexto(nombre) {
        var span = document.createElement('span');
        span.className = 'sidebar__texto';
        span.textContent = nombre;
        return span;
    }

    function vaciar(nodo) {
        while (nodo && nodo.firstChild) {
            nodo.removeChild(nodo.firstChild);
        }
    }

    // ------------------------------------------------------------------------
    // Rutas y estado activo
    // ------------------------------------------------------------------------

    function normalizarRuta(valor) {
        if (typeof valor !== 'string') {
            return '';
        }

        var texto = valor.trim();
        if (texto === '') {
            return '';
        }

        var corte = texto.indexOf('?');
        if (corte !== -1) {
            texto = texto.substring(0, corte);
        }
        corte = texto.indexOf('#');
        if (corte !== -1) {
            texto = texto.substring(0, corte);
        }

        if (texto.charAt(0) !== '/') {
            texto = '/' + texto;
        }
        if (texto.length > 1 && texto.charAt(texto.length - 1) === '/') {
            texto = texto.substring(0, texto.length - 1);
        }

        return texto.toLowerCase();
    }

    function esRutaActiva(url) {
        var objetivo = normalizarRuta(url);
        if (objetivo === '') {
            return false;
        }

        return normalizarRuta(window.location.pathname) === objetivo;
    }

    // ------------------------------------------------------------------------
    // Render de una opción (recursivo: soporta cualquier cantidad de niveles)
    // ------------------------------------------------------------------------

    function alternarGrupo(boton, sublista) {
        var expandido = boton.getAttribute('aria-expanded') === 'true';
        boton.setAttribute('aria-expanded', expandido ? 'false' : 'true');
        sublista.hidden = expandido;
    }

    function construirOpcion(opcion) {
        var nombre = typeof opcion.nombreMenu === 'string' ? opcion.nombreMenu : '';
        var url = typeof opcion.url === 'string' && opcion.url.trim() !== ''
            ? opcion.url.trim()
            : null;
        var hijas = Object.prototype.toString.call(opcion.opciones) === '[object Array]'
            ? opcion.opciones
            : [];

        var item = document.createElement('li');
        item.className = 'sidebar__item';

        // --- Agrupador: sin Url y con opciones. No navega; expande/contrae. ---
        if (hijas.length > 0) {
            var sublista = document.createElement('ul');
            sublista.className = 'sidebar__sublista';
            sublista.hidden = true;
            sublista.id = 'sidebar-submenu-' + opcion.idMenu;

            var resultadosHijos = [];
            var permiteActivo = false;

            for (var i = 0; i < hijas.length; i++) {
                var resultado = construirOpcion(hijas[i]);
                resultadosHijos.push(resultado);
                sublista.appendChild(resultado.elemento);
                if (resultado.activo || resultado.contieneActivo) {
                    permiteActivo = true;
                }
            }

            var boton = document.createElement('button');
            boton.type = 'button';
            boton.className = 'sidebar__enlace sidebar__enlace--grupo';
            boton.setAttribute('aria-expanded', 'false');
            boton.setAttribute('aria-controls', sublista.id);
            boton.setAttribute('title', nombre);
            boton.appendChild(crearIcono(opcion.icono));
            boton.appendChild(crearTexto(nombre));
            boton.appendChild(crearSvg(ICONO_CHEVRON, 'sidebar__chevron'));

            // Si algún descendiente está activo, la rama queda abierta y marcada.
            if (permiteActivo) {
                boton.setAttribute('aria-expanded', 'true');
                sublista.hidden = false;
                item.classList.add('sidebar__item--rama-activa');
            }

            boton.addEventListener('click', function () {
                alternarGrupo(boton, sublista);
            });

            item.appendChild(boton);
            item.appendChild(sublista);

            return { elemento: item, activo: false, contieneActivo: permiteActivo };
        }

        // --- Enlace navegable: Url definida y sin opciones. ---
        if (url) {
            var enlace = document.createElement('a');
            enlace.className = 'sidebar__enlace';
            enlace.setAttribute('href', url);
            enlace.setAttribute('title', nombre);
            enlace.appendChild(crearIcono(opcion.icono));
            enlace.appendChild(crearTexto(nombre));

            var activo = esRutaActiva(url);
            if (activo) {
                item.classList.add('sidebar__item--activo');
                enlace.setAttribute('aria-current', 'page');
            }

            item.appendChild(enlace);
            return { elemento: item, activo: activo, contieneActivo: false };
        }

        // --- Opción sin Url y sin hijos: no navegable (dato inconsistente). ---
        var inerte = document.createElement('span');
        inerte.className = 'sidebar__enlace sidebar__enlace--inerte';
        inerte.setAttribute('title', nombre);
        inerte.appendChild(crearIcono(opcion.icono));
        inerte.appendChild(crearTexto(nombre));

        item.appendChild(inerte);
        return { elemento: item, activo: false, contieneActivo: false };
    }

    // ------------------------------------------------------------------------
    // Estados del sidebar (cargando / error / vacío)
    // ------------------------------------------------------------------------

    function mostrarEstado(mensaje) {
        var estado = document.getElementById('sidebar-estado');
        if (!estado) {
            return;
        }

        estado.textContent = mensaje || '';
        estado.hidden = !mensaje;
    }

    function mostrarCargando() {
        var lista = document.getElementById('sidebar-lista');
        if (!lista) {
            return;
        }

        vaciar(lista);
        for (var i = 0; i < 4; i++) {
            var item = document.createElement('li');
            item.className = 'sidebar__esqueleto';
            item.setAttribute('aria-hidden', 'true');
            lista.appendChild(item);
        }
    }

    function renderizar(menu) {
        var lista = document.getElementById('sidebar-lista');
        if (!lista) {
            return;
        }

        vaciar(lista);

        if (menu.length === 0) {
            mostrarEstado(MENSAJE_VACIO);
            return;
        }

        mostrarEstado('');

        var activo = null;

        for (var i = 0; i < menu.length; i++) {
            var resultado = construirOpcion(menu[i]);
            lista.appendChild(resultado.elemento);

            if (!activo && (resultado.activo || resultado.contieneActivo)) {
                activo = resultado.elemento;
            }
        }

        if (activo && typeof activo.scrollIntoView === 'function') {
            // Deja visible la opción activa sin desplazar el resto de la página.
            activo.scrollIntoView({ block: 'nearest' });
        }
    }

    // ------------------------------------------------------------------------
    // Carga del menú
    // ------------------------------------------------------------------------

    function cargar() {
        if (!ns.api || !ns.sesion) {
            mostrarEstado(MENSAJE_ERROR);
            return;
        }

        var token = ns.sesion.obtenerToken();
        if (!token) {
            // La página protegida redirige al login; aquí solo se informa.
            mostrarEstado(MENSAJE_SIN_SESION);
            return;
        }

        mostrarCargando();

        ns.api.peticion(RUTA_MENU, {
            metodo: 'GET',
            token: token,
            // Petición protegida: un 401 dispara el flujo existente de sesión
            // expirada (limpieza + redirección al login) definido en api.js/guard.js.
            protegida: true
        }).then(function (resultado) {
            if (resultado.status === 401) {
                // El guard ya está redirigiendo: no se pinta ningún menú.
                return;
            }

            if (!resultado.ok
                || Object.prototype.toString.call(resultado.datos) !== '[object Array]') {
                vaciar(document.getElementById('sidebar-lista'));
                mostrarEstado(MENSAJE_ERROR);
                return;
            }

            renderizar(resultado.datos);
        }).catch(function () {
            vaciar(document.getElementById('sidebar-lista'));
            mostrarEstado(MENSAJE_ERROR);
        });
    }

    // ------------------------------------------------------------------------
    // Alternativa sin hover: apertura por toque o clic
    //
    // El sidebar se expande con .sidebar:hover (mouse) y con la clase
    // sidebar--abierto (toque o clic). La clase es un estado persistente: si queda
    // puesta en un equipo con mouse, el CSS mantiene el sidebar expandido aunque
    // el cursor esté fuera, así que el mouse la libera al entrar y al salir.
    // mouseenter/mouseleave no se disparan con el dedo: el estado táctil nunca
    // interfiere con el hover de escritorio.
    // ------------------------------------------------------------------------

    function liberarEstadoTactil(sidebar, boton) {
        if (!sidebar.classList.contains('sidebar--abierto')) {
            return;
        }

        sidebar.classList.remove('sidebar--abierto');
        boton.setAttribute('aria-expanded', 'false');
    }

    function enlazarBotonAlterno() {
        var boton = document.getElementById('btn-sidebar');
        var sidebar = document.getElementById('sidebar');
        if (!boton || !sidebar || boton.getAttribute(ENLAZADO) === '1') {
            // La marca de enlace hace la inicialización idempotente: volver a
            // llamar a iniciar() no duplica listeners ni estados.
            return;
        }

        boton.setAttribute(ENLAZADO, '1');

        boton.addEventListener('click', function () {
            var abierto = sidebar.classList.toggle('sidebar--abierto');
            boton.setAttribute('aria-expanded', abierto ? 'true' : 'false');
        });

        sidebar.addEventListener('mouseenter', function () {
            liberarEstadoTactil(sidebar, boton);
        });

        sidebar.addEventListener('mouseleave', function () {
            liberarEstadoTactil(sidebar, boton);
        });

        document.addEventListener('keydown', function (evento) {
            if (evento.key !== 'Escape' || !sidebar.classList.contains('sidebar--abierto')) {
                return;
            }

            sidebar.classList.remove('sidebar--abierto');
            boton.setAttribute('aria-expanded', 'false');
            boton.focus();
        });
    }

    // ------------------------------------------------------------------------
    // Panel de administración: fijar (recorrer pantalla) y ocultar
    // ------------------------------------------------------------------------

    var CLAVE_SIDEBAR_FIJO = 'visorSIG.sidebarFijo';
    var CLAVE_SIDEBAR_OCULTO = 'visorSIG.sidebarOculto';

    function refrescarMapa() {
        if (ns.mapa && typeof ns.mapa.refrescarTamano === 'function') {
            ns.mapa.refrescarTamano();
            setTimeout(function () {
                if (ns.mapa && typeof ns.mapa.refrescarTamano === 'function') {
                    ns.mapa.refrescarTamano();
                }
            }, 120);
            setTimeout(function () {
                if (ns.mapa && typeof ns.mapa.refrescarTamano === 'function') {
                    ns.mapa.refrescarTamano();
                }
            }, 260);
        }
    }

    function aplicarEstadoSidebar(fijo, oculto) {
        var shell = document.querySelector('.app-shell');
        var sidebar = document.getElementById('sidebar');
        var btnFijar = document.getElementById('btn-sidebar-fijar');
        var btnFijarPie = document.getElementById('btn-sidebar-fijar-pie');
        var textoFijar = document.getElementById('texto-sidebar-fijar');
        var btnReabrir = document.getElementById('btn-sidebar-reabrir');

        if (!shell || !sidebar) {
            return;
        }

        if (oculto) {
            sidebar.classList.add('sidebar--oculto');
            sidebar.classList.remove('sidebar--fijo');
            shell.classList.add('app-shell--sidebar-oculto');
            shell.classList.remove('app-shell--sidebar-fijo');
            if (btnReabrir) { btnReabrir.hidden = false; }
            try {
                window.localStorage.setItem(CLAVE_SIDEBAR_OCULTO, '1');
            } catch (e) {}
        } else {
            sidebar.classList.remove('sidebar--oculto');
            shell.classList.remove('app-shell--sidebar-oculto');
            if (btnReabrir) { btnReabrir.hidden = true; }
            try {
                window.localStorage.setItem(CLAVE_SIDEBAR_OCULTO, '0');
            } catch (e) {}

            if (fijo) {
                sidebar.classList.add('sidebar--fijo');
                shell.classList.add('app-shell--sidebar-fijo');
                if (btnFijar) {
                    btnFijar.setAttribute('aria-pressed', 'true');
                    btnFijar.title = 'Desanclar menú lateral';
                }
                if (btnFijarPie) {
                    btnFijarPie.setAttribute('aria-pressed', 'true');
                    btnFijarPie.title = 'Desanclar menú lateral';
                }
                if (textoFijar) {
                    textoFijar.textContent = 'Desfijar menú';
                }
                try {
                    window.localStorage.setItem(CLAVE_SIDEBAR_FIJO, '1');
                } catch (e) {}
            } else {
                sidebar.classList.remove('sidebar--fijo');
                shell.classList.remove('app-shell--sidebar-fijo');
                if (btnFijar) {
                    btnFijar.setAttribute('aria-pressed', 'false');
                    btnFijar.title = 'Fijar menú lateral y recorrer pantalla';
                }
                if (btnFijarPie) {
                    btnFijarPie.setAttribute('aria-pressed', 'false');
                    btnFijarPie.title = 'Fijar menú lateral y recorrer pantalla';
                }
                if (textoFijar) {
                    textoFijar.textContent = 'Fijar menú';
                }
                try {
                    window.localStorage.setItem(CLAVE_SIDEBAR_FIJO, '0');
                } catch (e) {}
            }
        }

        refrescarMapa();
    }

    function alternarFijo() {
        var sidebar = document.getElementById('sidebar');
        if (!sidebar) { return; }
        var esFijo = sidebar.classList.contains('sidebar--fijo');
        aplicarEstadoSidebar(!esFijo, false);
    }

    function enlazarControlesFijo() {
        var btnFijar = document.getElementById('btn-sidebar-fijar');
        var btnFijarPie = document.getElementById('btn-sidebar-fijar-pie');
        var btnOcultar = document.getElementById('btn-sidebar-ocultar');
        var btnReabrir = document.getElementById('btn-sidebar-reabrir');

        if (btnFijar && btnFijar.getAttribute(ENLAZADO) !== '1') {
            btnFijar.setAttribute(ENLAZADO, '1');
            btnFijar.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                alternarFijo();
            });
        }

        if (btnFijarPie && btnFijarPie.getAttribute(ENLAZADO) !== '1') {
            btnFijarPie.setAttribute(ENLAZADO, '1');
            btnFijarPie.addEventListener('click', function (e) {
                e.preventDefault();
                alternarFijo();
            });
        }

        if (btnOcultar && btnOcultar.getAttribute(ENLAZADO) !== '1') {
            btnOcultar.setAttribute(ENLAZADO, '1');
            btnOcultar.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                var sidebar = document.getElementById('sidebar');
                var esFijo = sidebar && sidebar.classList.contains('sidebar--fijo');
                aplicarEstadoSidebar(esFijo, true);
            });
        }

        if (btnReabrir && btnReabrir.getAttribute(ENLAZADO) !== '1') {
            btnReabrir.setAttribute(ENLAZADO, '1');
            btnReabrir.addEventListener('click', function (e) {
                e.preventDefault();
                var guardadoFijo = false;
                try {
                    guardadoFijo = window.localStorage.getItem(CLAVE_SIDEBAR_FIJO) === '1';
                } catch (err) {}
                aplicarEstadoSidebar(guardadoFijo, false);
            });
        }

        // Restaurar estado guardado en localStorage (por defecto fijo para mantener textos y menú visibles)
        var guardadoOculto = false;
        var guardadoFijo = true;
        try {
            guardadoOculto = window.localStorage.getItem(CLAVE_SIDEBAR_OCULTO) === '1';
            var valFijo = window.localStorage.getItem(CLAVE_SIDEBAR_FIJO);
            if (valFijo === '0') {
                guardadoFijo = false;
            } else {
                guardadoFijo = true;
            }
        } catch (e) {}

        aplicarEstadoSidebar(guardadoFijo, guardadoOculto);
    }

    var iniciado = false;

    function iniciar() {
        if (iniciado) {
            return;
        }

        iniciado = true;
        enlazarBotonAlterno();
        enlazarControlesFijo();
        cargar();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }

    ns.sidebar = {
        ICONOS: ICONOS,
        ICONO_GENERICO: ICONO_GENERICO,
        cargar: cargar,
        renderizar: renderizar,
        normalizarRuta: normalizarRuta,
        esRutaActiva: esRutaActiva,
        aplicarEstado: aplicarEstadoSidebar,
        alternarFijo: alternarFijo
    };
})(window.VisorSIG);
