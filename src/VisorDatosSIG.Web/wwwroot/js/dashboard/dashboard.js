// =============================================================================
// VisorDatosSIG 2026 — Dashboard Adaptativo por Rol (Frontend)
// Soporta vistas personalizadas para Administrador, Empleado, Consultor y Supervisor.
// Muestra qué tiene que hacer cada rol, métricas territoriales y estado en vivo.
// =============================================================================

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var cargando = false;
    var datosCargados = null;
    var rolActivo = 'Administrador';

    var el = function (id) { return document.getElementById(id); };

    function formatearNumero(num) {
        if (num === null || num === undefined) return '0';
        return Number(num).toLocaleString('es-ES');
    }

    function mostrarAviso(mensaje, esError) {
        var aviso = el('dashboard-mensaje');
        if (!aviso) return;
        if (!mensaje) {
            aviso.hidden = true;
            return;
        }
        aviso.textContent = mensaje;
        aviso.className = 'dashboard-aviso' + (esError ? ' dashboard-aviso--error' : '');
        aviso.hidden = false;
    }

    function obtenerIniciales(nombre, login) {
        var texto = (nombre || login || 'U').trim();
        var partes = texto.split(/\s+/);
        if (partes.length >= 2) {
            return (partes[0].charAt(0) + partes[1].charAt(0)).toUpperCase();
        }
        return texto.substring(0, 2).toUpperCase();
    }

    // --- Creador auxiliar de tarjetas de KPI ---
    function crearKpiCard(etiqueta, valor, subtexto, enlaceUrl, esDestacada, badgeTexto, puntoColor) {
        var card = document.createElement('div');
        card.className = 'kpi-card' + (esDestacada ? ' kpi-card--destacada' : '');

        var top = document.createElement('div');
        top.className = 'kpi-card__top';

        var label = document.createElement('span');
        label.className = 'kpi-card__label';
        label.textContent = etiqueta;
        top.appendChild(label);

        if (enlaceUrl) {
            var arrow = document.createElement('a');
            arrow.className = 'kpi-card__arrow';
            arrow.setAttribute('href', enlaceUrl);
            arrow.setAttribute('aria-label', etiqueta);

            var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.setAttribute('width', '15');
            svg.setAttribute('height', '15');
            svg.setAttribute('viewBox', '0 0 24 24');
            svg.setAttribute('fill', 'none');
            svg.setAttribute('stroke', 'currentColor');
            svg.setAttribute('stroke-width', '2.5');
            svg.setAttribute('stroke-linecap', 'round');
            svg.setAttribute('stroke-linejoin', 'round');

            var line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('x1', '7');
            line.setAttribute('y1', '17');
            line.setAttribute('x2', '17');
            line.setAttribute('y2', '7');

            var poly = document.createElementNS('http://www.w3.org/2000/svg', 'polyline');
            poly.setAttribute('points', '7 7 17 7 17 17');

            svg.appendChild(line);
            svg.appendChild(poly);
            arrow.appendChild(svg);
            top.appendChild(arrow);
        }

        var valorEl = document.createElement('div');
        valorEl.className = 'kpi-card__valor';
        valorEl.textContent = valor;

        card.appendChild(top);
        card.appendChild(valorEl);

        if (badgeTexto) {
            var badge = document.createElement('div');
            badge.className = 'kpi-card__badge-destacado';

            var dot = document.createElement('span');
            dot.className = 'kpi-punto-online';
            if (puntoColor) dot.style.backgroundColor = puntoColor;

            var spanBadge = document.createElement('span');
            spanBadge.textContent = badgeTexto;

            badge.appendChild(dot);
            badge.appendChild(spanBadge);
            card.appendChild(badge);
        } else if (subtexto) {
            var sub = document.createElement('div');
            sub.className = 'kpi-card__subtexto';

            var spanSub = document.createElement('span');
            spanSub.className = 'subtexto-positivo';
            spanSub.textContent = subtexto;

            sub.appendChild(spanSub);
            card.appendChild(sub);
        }

        return card;
    }

    // --- Construcción de Gráfico de Barras de Capas SIG ---
    function construirGraficoBarras(kpis) {
        var seccion = document.createElement('section');
        seccion.className = 'dash-card dash-card--analitica';

        var header = document.createElement('div');
        header.className = 'dash-card__header';

        var titulo = document.createElement('h2');
        titulo.className = 'dash-card__titulo';
        titulo.textContent = 'Distribución Territorial SIG';

        var sub = document.createElement('span');
        sub.className = 'dash-card__sub';
        sub.textContent = 'Registros por capa';

        header.appendChild(titulo);
        header.appendChild(sub);
        seccion.appendChild(header);

        var contenedor = document.createElement('div');
        contenedor.className = 'dash-barras-contenedor';

        var leyenda = document.createElement('div');
        leyenda.className = 'dash-barras-leyenda';

        var items = [
            { nombre: 'Manzanas', total: kpis.totalManzanas || 0, clase: 'dash-barra-fill--1', abrev: 'MNZ' },
            { nombre: 'Lotes', total: kpis.totalLotes || 0, clase: 'dash-barra-fill--2', abrev: 'LOT' },
            { nombre: 'Cód. Fijos', total: kpis.totalCodigosFijos || 0, clase: 'dash-barra-fill--3', abrev: 'CF' },
            { nombre: 'Vías', total: kpis.totalVias || 0, clase: 'dash-barra-fill--4', abrev: 'VÍA' }
        ];

        var max = Math.max.apply(Math, items.map(function (i) { return i.total; }));
        if (max === 0) max = 1;

        items.forEach(function (item) {
            var alturaPct = Math.max(12, Math.round((item.total / max) * 100));

            var col = document.createElement('div');
            col.className = 'dash-col-barra';

            var tubo = document.createElement('div');
            tubo.className = 'dash-barra-tubo';
            tubo.title = item.nombre + ': ' + formatearNumero(item.total) + ' registros';

            var fill = document.createElement('div');
            fill.className = 'dash-barra-fill ' + item.clase;
            fill.style.height = '0%';
            tubo.appendChild(fill);

            var etiqueta = document.createElement('span');
            etiqueta.className = 'dash-col-etiqueta';
            etiqueta.textContent = item.abrev;

            col.appendChild(tubo);
            col.appendChild(etiqueta);
            contenedor.appendChild(col);

            setTimeout(function () {
                fill.style.height = alturaPct + '%';
            }, 60);

            var itemLeyenda = document.createElement('div');
            itemLeyenda.className = 'dash-leyenda-item';

            var spanNombre = document.createElement('span');
            spanNombre.textContent = item.nombre;

            var spanValor = document.createElement('strong');
            spanValor.textContent = formatearNumero(item.total);

            itemLeyenda.appendChild(spanNombre);
            itemLeyenda.appendChild(spanValor);
            leyenda.appendChild(itemLeyenda);
        });

        seccion.appendChild(contenedor);
        seccion.appendChild(leyenda);
        return seccion;
    }

    // --- Construcción de Guía de Pasos / Tareas a Realizar ---
    function construirGuiaPasos(tituloGuia, subtituloGuia, pasos) {
        var seccion = document.createElement('section');
        seccion.className = 'dash-card dash-card--guia';

        var header = document.createElement('div');
        header.className = 'dash-card__header';

        var titulo = document.createElement('h2');
        titulo.className = 'dash-card__titulo';
        titulo.textContent = tituloGuia;

        var sub = document.createElement('span');
        sub.className = 'dash-card__badge-ok';
        sub.textContent = subtituloGuia;

        header.appendChild(titulo);
        header.appendChild(sub);
        seccion.appendChild(header);

        var lista = document.createElement('div');
        lista.className = 'dash-pasos-lista';

        pasos.forEach(function (p, index) {
            var item = document.createElement('div');
            item.className = 'dash-paso-item';

            var num = document.createElement('span');
            num.className = 'dash-paso-num';
            num.textContent = (index + 1);

            var info = document.createElement('div');
            info.className = 'dash-paso-info';

            var t = document.createElement('span');
            t.className = 'dash-paso-titulo';
            t.textContent = p.titulo;

            var d = document.createElement('span');
            d.className = 'dash-paso-desc';
            d.textContent = p.desc;

            info.appendChild(t);
            info.appendChild(d);

            item.appendChild(num);
            item.appendChild(info);
            lista.appendChild(item);
        });

        seccion.appendChild(lista);
        return seccion;
    }

    // --- Construcción del Medidor de Cuadrillas / Gauge ---
    function construirMedidorCuadrillas(kpis) {
        var seccion = document.createElement('section');
        seccion.className = 'dash-card dash-card--progreso';

        var header = document.createElement('div');
        header.className = 'dash-card__header';

        var titulo = document.createElement('h2');
        titulo.className = 'dash-card__titulo';
        titulo.textContent = 'Disponibilidad de Cuadrillas';

        var sub = document.createElement('span');
        sub.className = 'dash-card__sub';
        sub.textContent = 'Personal operativo';

        header.appendChild(titulo);
        header.appendChild(sub);
        seccion.appendChild(header);

        var wrapper = document.createElement('div');
        wrapper.className = 'dash-gauge-wrapper';

        var grafico = document.createElement('div');
        grafico.className = 'dash-gauge-grafico';

        var total = kpis.totalEmpleados || 0;
        var disponibles = kpis.empleadosDisponibles || 0;
        var servicio = kpis.empleadosEnServicio || 0;
        var bajas = kpis.empleadosBajas || 0;
        var pct = total > 0 ? Math.round((disponibles / total) * 100) : 0;

        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('viewBox', '0 0 200 110');
        svg.setAttribute('class', 'gauge-svg');

        var bg = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        bg.setAttribute('class', 'gauge-bg');
        bg.setAttribute('d', 'M 20 100 A 80 80 0 0 1 180 100');
        bg.setAttribute('fill', 'none');
        bg.setAttribute('stroke-width', '22');
        bg.setAttribute('stroke-linecap', 'round');

        var fill = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        fill.setAttribute('class', 'gauge-fill');
        fill.setAttribute('d', 'M 20 100 A 80 80 0 0 1 180 100');
        fill.setAttribute('fill', 'none');
        fill.setAttribute('stroke-width', '22');
        fill.setAttribute('stroke-linecap', 'round');
        fill.setAttribute('stroke-dasharray', '251.2');
        fill.setAttribute('stroke-dashoffset', '251.2');

        svg.appendChild(bg);
        svg.appendChild(fill);

        var textoWrap = document.createElement('div');
        textoWrap.className = 'gauge-valor-texto';

        var porcentaje = document.createElement('span');
        porcentaje.className = 'gauge-porcentaje';
        porcentaje.textContent = pct + '%';

        var etiqueta = document.createElement('span');
        etiqueta.className = 'gauge-etiqueta';
        etiqueta.textContent = 'Disponible';

        textoWrap.appendChild(porcentaje);
        textoWrap.appendChild(etiqueta);

        grafico.appendChild(svg);
        grafico.appendChild(textoWrap);

        // Leyenda
        var leyenda = document.createElement('div');
        leyenda.className = 'gauge-leyenda';

        var leyendasData = [
            { nombre: 'Disponibles', count: disponibles, dotClase: 'gauge-dot--disponible' },
            { nombre: 'En Servicio', count: servicio, dotClase: 'gauge-dot--servicio' },
            { nombre: 'Bajas', count: bajas, dotClase: 'gauge-dot--bajas' }
        ];

        leyendasData.forEach(function (ld) {
            var itemL = document.createElement('div');
            itemL.className = 'gauge-leyenda-item';

            var dot = document.createElement('span');
            dot.className = 'gauge-dot ' + ld.dotClase;

            var nom = document.createElement('span');
            nom.className = 'gauge-nombre';
            nom.textContent = ld.nombre;

            var num = document.createElement('strong');
            num.className = 'gauge-num';
            num.textContent = ld.count;

            itemL.appendChild(dot);
            itemL.appendChild(nom);
            itemL.appendChild(num);
            leyenda.appendChild(itemL);
        });

        wrapper.appendChild(grafico);
        wrapper.appendChild(leyenda);
        seccion.appendChild(wrapper);

        setTimeout(function () {
            var offset = 251.2 * (1 - (pct / 100));
            fill.style.strokeDashoffset = offset;
        }, 80);

        return seccion;
    }

    // --- Construcción de Lista de Empleados / Cuadrillas ---
    function construirListaEmpleados(empleados) {
        var seccion = document.createElement('section');
        seccion.className = 'dash-card dash-card--personal';

        var header = document.createElement('div');
        header.className = 'dash-card__header';

        var titulo = document.createElement('h2');
        titulo.className = 'dash-card__titulo';
        titulo.textContent = 'Personal Operativo y Cuadrillas';

        var link = document.createElement('a');
        link.className = 'btn-dash-link';
        link.setAttribute('href', '/Empleados');
        link.textContent = 'Ver Empleados';

        header.appendChild(titulo);
        header.appendChild(link);
        seccion.appendChild(header);

        var lista = document.createElement('div');
        lista.className = 'dash-empleados-lista';

        if (!empleados || empleados.length === 0) {
            var vacio = document.createElement('p');
            vacio.className = 'dash-card__sub';
            vacio.textContent = 'No hay personal registrado.';
            lista.appendChild(vacio);
        } else {
            empleados.slice(0, 6).forEach(function (emp) {
                var row = document.createElement('div');
                row.className = 'dash-empleado-item';

                var avatar = document.createElement('div');
                avatar.className = 'dash-avatar';
                avatar.style.width = '34px';
                avatar.style.height = '34px';
                avatar.style.fontSize = '0.78rem';
                avatar.textContent = obtenerIniciales(emp.nombreCompleto, emp.codigo);

                var info = document.createElement('div');
                info.className = 'dash-empleado-info';

                var nom = document.createElement('span');
                nom.className = 'dash-empleado-nombre';
                nom.textContent = emp.nombreCompleto;

                var car = document.createElement('span');
                car.className = 'dash-empleado-cargo';
                car.textContent = (emp.cargo || 'Operativo') + ' · ' + (emp.area || 'Campo');

                info.appendChild(nom);
                info.appendChild(car);

                var badge = document.createElement('span');
                var disp = emp.disponibilidad || 'Disponible';
                var claseDisp = disp === 'En Servicio' ? 'badge-disp--servicio' : (disp === 'Baja Parcial' ? 'badge-disp--baja' : 'badge-disp--disponible');
                badge.className = 'badge-disp ' + claseDisp;
                badge.textContent = disp;

                row.appendChild(avatar);
                row.appendChild(info);
                row.appendChild(badge);
                lista.appendChild(row);
            });
        }

        seccion.appendChild(lista);
        return seccion;
    }

    // --- Construcción de Feed de Bitácora ---
    function construirFeedBitacora(eventos, tituloTexto) {
        var seccion = document.createElement('section');
        seccion.className = 'dash-card dash-card--bitacora';

        var header = document.createElement('div');
        header.className = 'dash-card__header';

        var titulo = document.createElement('h2');
        titulo.className = 'dash-card__titulo';
        titulo.textContent = tituloTexto || 'Auditoría en Vivo';

        var link = document.createElement('a');
        link.className = 'btn-dash-link';
        link.setAttribute('href', '/Bitacora');
        link.textContent = 'Ver todo';

        header.appendChild(titulo);
        header.appendChild(link);
        seccion.appendChild(header);

        var feed = document.createElement('div');
        feed.className = 'dash-bitacora-feed';

        if (!eventos || eventos.length === 0) {
            var vacio = document.createElement('p');
            vacio.className = 'dash-card__sub';
            vacio.textContent = 'Sin eventos registrados.';
            feed.appendChild(vacio);
        } else {
            eventos.slice(0, 6).forEach(function (ev) {
                var item = document.createElement('div');
                item.className = 'dash-bitacora-item';

                var punto = document.createElement('span');
                punto.className = 'dash-bitacora-punto';

                var detalle = document.createElement('div');
                detalle.className = 'dash-bitacora-detalle';

                var enc = document.createElement('div');
                enc.className = 'dash-bitacora-encabezado';

                var mod = document.createElement('span');
                mod.className = 'dash-bitacora-modulo';
                mod.textContent = ev.modulo || 'SISTEMA';

                var tiempo = document.createElement('span');
                tiempo.className = 'dash-bitacora-tiempo';
                if (ns.fechas && typeof ns.fechas.formatearFechaHora === 'function') {
                    tiempo.textContent = ns.fechas.formatearFechaHora(ev.fechaHora);
                } else {
                    tiempo.textContent = (ev.fechaHora || '').substring(11, 16);
                }

                enc.appendChild(mod);
                enc.appendChild(tiempo);

                var acc = document.createElement('span');
                acc.className = 'dash-bitacora-accion';
                acc.textContent = (ev.accion || '') + (ev.entidad ? ' · ' + ev.entidad : '');

                detalle.appendChild(enc);
                detalle.appendChild(acc);

                item.appendChild(punto);
                item.appendChild(detalle);
                feed.appendChild(item);
            });
        }

        seccion.appendChild(feed);
        return seccion;
    }

    // --- Construcción de Módulos de Accesos Rápidos ---
    function construirAccesosModulos(itemsModulo) {
        var seccion = document.createElement('section');
        seccion.className = 'dash-card dash-card--modulos';

        var header = document.createElement('div');
        header.className = 'dash-card__header';

        var titulo = document.createElement('h2');
        titulo.className = 'dash-card__titulo';
        titulo.textContent = 'Accesos Rápidos';

        var sub = document.createElement('span');
        sub.className = 'dash-card__sub';
        sub.textContent = 'Navegación directa';

        header.appendChild(titulo);
        header.appendChild(sub);
        seccion.appendChild(header);

        var lista = document.createElement('div');
        lista.className = 'dash-modulos-lista';

        itemsModulo.forEach(function (m) {
            var a = document.createElement('a');
            a.className = 'dash-modulo-item';
            a.setAttribute('href', m.url);

            var icon = document.createElement('div');
            icon.className = 'dash-modulo-item__icon';

            var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.setAttribute('width', '16');
            svg.setAttribute('height', '16');
            svg.setAttribute('viewBox', '0 0 24 24');
            svg.setAttribute('fill', 'none');
            svg.setAttribute('stroke', 'currentColor');
            svg.setAttribute('stroke-width', '2');

            if (m.tipo === 'mapa') {
                var path1 = document.createElementNS('http://www.w3.org/2000/svg', 'path');
                path1.setAttribute('d', 'M12 21.2s6.6-5.4 6.6-10.4a6.6 6.6 0 1 0-13.2 0c0 5 6.6 10.4 6.6 10.4Z');
                var circle1 = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                circle1.setAttribute('cx', '12');
                circle1.setAttribute('cy', '10.4');
                circle1.setAttribute('r', '2.5');
                svg.appendChild(path1);
                svg.appendChild(circle1);
            } else if (m.tipo === 'consultas') {
                var c = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                c.setAttribute('cx', '11');
                c.setAttribute('cy', '11');
                c.setAttribute('r', '8');
                var l = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                l.setAttribute('x1', '21');
                l.setAttribute('y1', '21');
                l.setAttribute('x2', '16.65');
                l.setAttribute('y2', '16.65');
                svg.appendChild(c);
                svg.appendChild(l);
            } else {
                var p = document.createElementNS('http://www.w3.org/2000/svg', 'path');
                p.setAttribute('d', 'M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2');
                var c2 = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                c2.setAttribute('cx', '9');
                c2.setAttribute('cy', '7');
                c2.setAttribute('r', '4');
                svg.appendChild(p);
                svg.appendChild(c2);
            }

            icon.appendChild(svg);

            var info = document.createElement('div');
            info.className = 'dash-modulo-item__info';

            var t = document.createElement('span');
            t.className = 'dash-modulo-item__titulo';
            t.textContent = m.titulo;

            var d = document.createElement('span');
            d.className = 'dash-modulo-item__cu';
            d.textContent = m.desc;

            info.appendChild(t);
            info.appendChild(d);

            var flecha = document.createElement('span');
            flecha.className = 'dash-modulo-item__flecha';
            flecha.textContent = '›';

            a.appendChild(icon);
            a.appendChild(info);
            a.appendChild(flecha);
            lista.appendChild(a);
        });

        seccion.appendChild(lista);
        return seccion;
    }

    // --- Construcción del Estado de Servicios / Infraestructura ---
    function construirEstadoServicios() {
        var seccion = document.createElement('section');
        seccion.className = 'dash-card dash-card--estado';

        var header = document.createElement('div');
        header.className = 'dash-card__header';

        var titulo = document.createElement('h2');
        titulo.className = 'dash-card__titulo';
        titulo.textContent = 'Estado de Servicios';

        var badge = document.createElement('span');
        badge.className = 'dash-card__badge-ok';
        badge.textContent = '● Operativo';

        header.appendChild(titulo);
        header.appendChild(badge);
        seccion.appendChild(header);

        var lista = document.createElement('div');
        lista.className = 'dash-estado-lista';

        var itemsServicio = [
            { nombre: 'Base de Datos SQL Server', desc: 'Azure SQL · Conectado', estado: 'Óptimo' },
            { nombre: 'API REST .NET 10', desc: 'Servicios web protegidos', estado: 'Activo' },
            { nombre: 'Visor Cartográfico Leaflet', desc: 'Renderizado geoespacial', estado: 'Listo' }
        ];

        itemsServicio.forEach(function (s) {
            var item = document.createElement('div');
            item.className = 'dash-estado-item';

            var info = document.createElement('div');
            info.className = 'dash-estado-item__info';

            var n = document.createElement('span');
            n.className = 'dash-estado-item__nombre';
            n.textContent = s.nombre;

            var d = document.createElement('span');
            d.className = 'dash-estado-item__desc';
            d.textContent = s.desc;

            info.appendChild(n);
            info.appendChild(d);

            var b = document.createElement('span');
            b.className = 'badge-salud';
            b.textContent = s.estado;

            item.appendChild(info);
            item.appendChild(b);
            lista.appendChild(item);
        });

        var accion = document.createElement('div');
        accion.className = 'dash-estado__accion';

        var btn = document.createElement('a');
        btn.className = 'btn-dash-accion-full';
        btn.setAttribute('href', '/Visor');

        var spanBtn = document.createElement('span');
        spanBtn.textContent = 'Explorar Mapa Territorial';
        btn.appendChild(spanBtn);
        accion.appendChild(btn);

        seccion.appendChild(lista);
        seccion.appendChild(accion);
        return seccion;
    }

    // --- Construcción de Lista de Usuarios / Cuentas ---
    function construirListaUsuarios(usuarios, kpis) {
        var seccion = document.createElement('section');
        seccion.className = 'dash-card dash-card--usuarios';

        var header = document.createElement('div');
        header.className = 'dash-card__header';

        var colHeader = document.createElement('div');
        var titulo = document.createElement('h2');
        titulo.className = 'dash-card__titulo';
        titulo.textContent = 'Usuarios del Sistema';

        var sub = document.createElement('span');
        sub.className = 'dash-card__sub';
        sub.textContent = (kpis.totalUsuarios || 0) + ' registrados · ' + (kpis.usuariosConectados || 0) + ' en línea';

        colHeader.appendChild(titulo);
        colHeader.appendChild(sub);

        var link = document.createElement('a');
        link.className = 'btn-dash-link';
        link.setAttribute('href', '/Usuarios');
        link.textContent = '+ Gestionar';

        header.appendChild(colHeader);
        header.appendChild(link);
        seccion.appendChild(header);

        var lista = document.createElement('div');
        lista.className = 'dash-usuarios-lista';

        if (!usuarios || usuarios.length === 0) {
            var vacio = document.createElement('p');
            vacio.className = 'dash-card__sub';
            vacio.textContent = 'No hay usuarios.';
            lista.appendChild(vacio);
        } else {
            var ordenados = usuarios.slice().sort(function (a, b) {
                if (a.activo === b.activo) return 0;
                return a.activo ? -1 : 1;
            }).slice(0, 6);

            ordenados.forEach(function (u) {
                var fila = document.createElement('div');
                fila.className = 'dash-usuario-fila';

                var aw = document.createElement('div');
                aw.className = 'dash-avatar-wrapper';

                var av = document.createElement('div');
                av.className = 'dash-avatar';
                av.textContent = obtenerIniciales(u.nombre, u.login);

                var pt = document.createElement('span');
                pt.className = 'dash-avatar-status ' + (u.activo ? 'dash-avatar-status--online' : 'dash-avatar-status--offline');

                aw.appendChild(av);
                aw.appendChild(pt);

                var d = document.createElement('div');
                d.className = 'dash-usuario-datos';

                var unom = document.createElement('span');
                unom.className = 'dash-usuario-nombre';
                unom.textContent = u.nombre || u.login;

                var urol = document.createElement('span');
                urol.className = 'dash-usuario-roles';
                var rtext = Array.isArray(u.roles) ? u.roles.join(', ') : (u.roles || 'Usuario');
                urol.textContent = rtext;

                d.appendChild(unom);
                d.appendChild(urol);

                var tag = document.createElement('span');
                tag.className = 'dash-usuario-estado-tag ' + (u.activo ? 'dash-usuario-estado-tag--online' : 'dash-usuario-estado-tag--offline');
                tag.textContent = u.activo ? 'Conectado' : 'Desconectado';

                fila.appendChild(aw);
                fila.appendChild(d);
                fila.appendChild(tag);
                lista.appendChild(fila);
            });
        }

        seccion.appendChild(lista);
        return seccion;
    }

    // =========================================================================
    // RENDERIZADO REACTIVO SEGÚN EL ROL
    // =========================================================================

    function renderizarVista(rol, datos) {
        var kpis = datos.kpis || {};
        var capas = datos.capas || [];
        var usuarios = datos.usuarios || [];
        var empleados = datos.empleados || [];
        var actividad = datos.actividadReciente || [];

        var tituloEl = el('dash-titulo');
        var subtituloEl = el('dash-subtitulo');
        var kpiGrid = el('dash-kpi-grid');
        var medioGrid = el('dash-medio-grid');
        var inferiorGrid = el('dash-inferior-grid');

        if (!kpiGrid || !medioGrid || !inferiorGrid) return;

        kpiGrid.textContent = '';
        medioGrid.textContent = '';
        inferiorGrid.textContent = '';

        // ---------------------------------------------------------------------
        // 1. ROL: EMPLEADO (Personal de Campo)
        // ---------------------------------------------------------------------
        if (rol === 'Empleado') {
            if (tituloEl) tituloEl.textContent = 'Dashboard Operativo de Campo';
            if (subtituloEl) subtituloEl.textContent = 'Resumen de tu jornada, disponibilidad de cuadrillas y tareas de inspección en terreno.';

            // 4 KPIs
            kpiGrid.appendChild(crearKpiCard('Mi Estado Operativo', 'Disponible', null, '/Visor', true, 'Cuadrilla de Terreno', '#38bdf8'));
            kpiGrid.appendChild(crearKpiCard('Personal en Servicio', kpis.empleadosEnServicio + ' en campo', 'Cuadrillas activas hoy', '/Empleados', false));
            kpiGrid.appendChild(crearKpiCard('Códigos Fijos Asignados', formatearNumero(kpis.totalCodigosFijos), 'Puntos para inspección', '/Consultas/CodigoFijo', false));
            kpiGrid.appendChild(crearKpiCard('Lotes en Territorio', formatearNumero(kpis.totalLotes), 'Predios catastrales', '/Visor', false));

            // Fila Media: Guía de qué tiene que hacer + Accesos de campo + Estado
            var pasosEmpleado = [
                { titulo: '1. Ubicar en el Visor Cartográfico', desc: 'Abre el mapa para localizar la manzana y lote donde se realizará la inspección física.' },
                { titulo: '2. Verificar el Código Fijo', desc: 'Comprueba el estado del predio (Normal, Para Corte o Cortado) antes de intervenir en el terreno.' },
                { titulo: '3. Registrar Seguimiento de Servicio', desc: 'Reporta la lectura, inspección física o solicitud de baja parcial técnica efectuada.' }
            ];
            medioGrid.appendChild(construirGuiaPasos('Flujo de Trabajo Diario (Qué hacer)', 'Guía Operativa', pasosEmpleado));

            var accesosEmpleado = [
                { titulo: 'Visor Cartográfico', desc: 'Navegar en mapa y rutas', url: '/Visor', tipo: 'mapa' },
                { titulo: 'Consultar Código Fijo', desc: 'Verificar abonado', url: '/Consultas/CodigoFijo', tipo: 'consultas' },
                { titulo: 'Consultar Manzana o Lote', desc: 'Inspección de linderos', url: '/Consultas/Manzana', tipo: 'consultas' }
            ];
            medioGrid.appendChild(construirAccesosModulos(accesosEmpleado));
            medioGrid.appendChild(construirEstadoServicios());

            // Fila Inferior: Compañeros de cuadrilla + Medidor + Operaciones de campo
            inferiorGrid.appendChild(construirListaEmpleados(empleados));
            inferiorGrid.appendChild(construirMedidorCuadrillas(kpis));
            inferiorGrid.appendChild(construirFeedBitacora(actividad, 'Mis Operaciones Recientes'));
        }

        // ---------------------------------------------------------------------
        // 2. ROL: CONSULTOR (Analista de Búsquedas y Consultas Territoriales)
        // ---------------------------------------------------------------------
        else if (rol === 'Consultor') {
            if (tituloEl) tituloEl.textContent = 'Dashboard de Consultas Territoriales';
            if (subtituloEl) subtituloEl.textContent = 'Búsqueda geoespacial, delimitación catastral y análisis de predios.';

            // 4 KPIs
            kpiGrid.appendChild(crearKpiCard('Entidades Catastrales', formatearNumero(kpis.totalRegistrosSIG), null, '/Visor', true, '22.982 registros para consulta', '#38bdf8'));
            kpiGrid.appendChild(crearKpiCard('Códigos Fijos de Servicio', formatearNumero(kpis.totalCodigosFijos), 'Abonados con estado de corte', '/Consultas/CodigoFijo', false));
            kpiGrid.appendChild(crearKpiCard('Lotes y Parcelas', formatearNumero(kpis.totalLotes), 'Polígonos delimitados', '/Consultas/Lotes', false));
            kpiGrid.appendChild(crearKpiCard('Manzanas Urbanas', formatearNumero(kpis.totalManzanas), 'Delimitaciones espaciales', '/Consultas/Manzana', false));

            // Fila Media: Gráfico de capas + Guía de consulta + Herramientas de búsqueda
            medioGrid.appendChild(construirGraficoBarras(kpis));

            var pasosConsultor = [
                { titulo: '1. Búsqueda por Código Fijo', desc: 'Localiza de inmediato el predio de un abonado ingresando su código único de servicio.' },
                { titulo: '2. Filtro por Manzana o Lote', desc: 'Consulta el conjunto de parcelas dentro de un manzano catastral específico.' },
                { titulo: '3. Análisis en Visor Cartográfico', desc: 'Mide distancias, inspecciona capas vectoriales y navega libremente por el territorio.' }
            ];
            medioGrid.appendChild(construirGuiaPasos('Herramientas de Consulta (Qué hacer)', 'Guía de Búsqueda', pasosConsultor));

            var accesosConsultor = [
                { titulo: 'Visor Cartográfico', desc: 'Mapa y capas interactivas', url: '/Visor', tipo: 'mapa' },
                { titulo: 'Consulta Código Fijo', desc: 'Búsqueda por número de predio', url: '/Consultas/CodigoFijo', tipo: 'consultas' },
                { titulo: 'Consulta por Manzana', desc: 'Delimitación de manzanas', url: '/Consultas/Manzana', tipo: 'consultas' },
                { titulo: 'Consulta de Lotes', desc: 'Listado de parcelas', url: '/Consultas/Lotes', tipo: 'consultas' },
                { titulo: 'Consulta de Vías', desc: 'Ejes viales y calles', url: '/Consultas/Vias', tipo: 'consultas' }
            ];
            medioGrid.appendChild(construirAccesosModulos(accesosConsultor));

            // Fila Inferior: Estado de servicios + Medidor + Bitácora de consultas
            inferiorGrid.appendChild(construirEstadoServicios());
            inferiorGrid.appendChild(construirMedidorCuadrillas(kpis));
            inferiorGrid.appendChild(construirFeedBitacora(actividad, 'Consultas y Auditoría'));
        }

        // ---------------------------------------------------------------------
        // 3. ROL: SUPERVISOR (Coordinador de Cuadrillas y Operaciones)
        // ---------------------------------------------------------------------
        else if (rol === 'Supervisor') {
            if (tituloEl) tituloEl.textContent = 'Dashboard de Supervisión Operativa';
            if (subtituloEl) subtituloEl.textContent = 'Coordinación de cuadrillas, balance de disponibilidad y monitoreo de campo en tiempo real.';

            var pctDisp = kpis.totalEmpleados > 0 ? Math.round((kpis.empleadosDisponibles / kpis.totalEmpleados) * 100) : 0;

            // 4 KPIs
            kpiGrid.appendChild(crearKpiCard('Disponibilidad de Cuadrillas', pctDisp + '%', null, '/Empleados', true, kpis.empleadosDisponibles + ' cuadrillas disponibles', '#38bdf8'));
            kpiGrid.appendChild(crearKpiCard('Personal en Servicio', kpis.empleadosEnServicio + ' en campo', 'Cuadrillas activas', '/Empleados', false));
            kpiGrid.appendChild(crearKpiCard('Personal Operativo Total', formatearNumero(kpis.totalEmpleados), 'Nómina operativa', '/Empleados', false));
            kpiGrid.appendChild(crearKpiCard('Entidades a Cubrir', formatearNumero(kpis.totalRegistrosSIG), 'Cobertura territorial SIG', '/Visor', false));

            // Fila Media: Guía del Supervisor + Medidor de cuadrillas + Accesos de coordinación
            var pasosSupervisor = [
                { titulo: '1. Verificar Disponibilidad del Personal', desc: 'Revisa qué empleados están disponibles o de baja temporal antes de iniciar asignaciones.' },
                { titulo: '2. Coordinar Zonas en Mapa Territorial', desc: 'Verifica en el Visor Cartográfico las manzanas y códigos fijos que requieren atención prioritaria.' },
                { titulo: '3. Auditar Operaciones de Campo', desc: 'Monitorea en tiempo real los eventos de seguimiento de servicio e inspecciones reportadas.' }
            ];
            medioGrid.appendChild(construirGuiaPasos('Coordinación de Personal (Qué hacer)', 'Guía de Supervisión', pasosSupervisor));
            medioGrid.appendChild(construirMedidorCuadrillas(kpis));

            var accesosSupervisor = [
                { titulo: 'Gestión de Empleados', desc: 'Disponibilidad y cuadrillas', url: '/Empleados', tipo: 'empleados' },
                { titulo: 'Visor Cartográfico', desc: 'Zonas y territorio', url: '/Visor', tipo: 'mapa' },
                { titulo: 'Reportes y Auditoría', desc: 'Historial de campo', url: '/Reportes', tipo: 'consultas' }
            ];
            medioGrid.appendChild(construirAccesosModulos(accesosSupervisor));

            // Fila Inferior: Lista de Empleados + Gráfico de Capas + Bitácora en vivo
            inferiorGrid.appendChild(construirListaEmpleados(empleados));
            inferiorGrid.appendChild(construirGraficoBarras(kpis));
            inferiorGrid.appendChild(construirFeedBitacora(actividad, 'Auditoría de Cuadrillas en Vivo'));
        }

        // ---------------------------------------------------------------------
        // 4. ROL: ADMINISTRADOR (Panel Ejecutivo Global del Sistema)
        // ---------------------------------------------------------------------
        else {
            if (tituloEl) tituloEl.textContent = 'Dashboard';
            if (subtituloEl) subtituloEl.textContent = 'Monitoreo general, métricas del territorio y estado operativo en tiempo real.';

            // 4 KPIs
            var tagConectados = kpis.usuariosConectados + (kpis.usuariosConectados === 1 ? ' conectado actualmente' : ' conectados actualmente');
            kpiGrid.appendChild(crearKpiCard('Total Usuarios', formatearNumero(kpis.totalUsuarios), null, '/Usuarios', true, tagConectados, '#38bdf8'));
            kpiGrid.appendChild(crearKpiCard('Usuarios Conectados', formatearNumero(kpis.usuariosConectados), 'Sesiones en vivo', '/Usuarios', false));
            kpiGrid.appendChild(crearKpiCard('Personal Operativo', formatearNumero(kpis.totalEmpleados), (kpis.empleadosDisponibles || 0) + ' disponibles para asignación', '/Empleados', false));
            kpiGrid.appendChild(crearKpiCard('Entidades SIG', formatearNumero(kpis.totalRegistrosSIG), 'Manzanas, lotes, vías y códigos', '/Visor', false));

            // Fila Media: Gráfico de Capas + Estado de Servicios + Módulos
            medioGrid.appendChild(construirGraficoBarras(kpis));
            medioGrid.appendChild(construirEstadoServicios());

            var accesosAdmin = [
                { titulo: 'Usuarios y Seguridad', desc: 'Gestión de cuentas y roles', url: '/Usuarios', tipo: 'seguridad' },
                { titulo: 'Personal Operativo', desc: 'Seguimiento de cuadrillas', url: '/Empleados', tipo: 'empleados' },
                { titulo: 'Visor Cartográfico', desc: 'Capas y territorio', url: '/Visor', tipo: 'mapa' },
                { titulo: 'Consultas y Filtros', desc: 'Búsqueda espacial', url: '/Consultas/CodigoFijo', tipo: 'consultas' },
                { titulo: 'Reportes y Auditoría', desc: 'Historial y estadísticas', url: '/Reportes', tipo: 'consultas' }
            ];
            medioGrid.appendChild(construirAccesosModulos(accesosAdmin));

            // Fila Inferior: Usuarios + Medidor + Bitácora
            inferiorGrid.appendChild(construirListaUsuarios(usuarios, kpis));
            inferiorGrid.appendChild(construirMedidorCuadrillas(kpis));
            inferiorGrid.appendChild(construirFeedBitacora(actividad, 'Auditoría en Vivo'));
        }
    }

    // --- Carga de Indicadores desde el Backend ---
    async function cargarIndicadores() {
        if (cargando) return;
        cargando = true;
        mostrarAviso('', false);

        var btn = el('btn-actualizar-dash');
        if (btn) btn.classList.add('cargando');

        try {
            var token = ns.sesion ? ns.sesion.obtenerToken() : null;
            var res = await ns.api.peticion('api/dashboard/indicadores', {
                metodo: 'GET',
                token: token,
                protegida: true
            });

            if (!res.ok || !res.datos) {
                mostrarAviso('No se pudieron obtener todos los indicadores del sistema. Verifique su conexión con el servidor.', true);
                return;
            }

            datosCargados = res.datos;
            renderizarVista(rolActivo, datosCargados);
        } catch (err) {
            mostrarAviso('Error inesperado al cargar el Dashboard: ' + (err.message || 'error de conexión'), true);
        } finally {
            cargando = false;
            if (btn) btn.classList.remove('cargando');
        }
    }

    // --- Configuración del selector de roles para Administradores ---
    function configurarSelectorRoles() {
        var usuario = ns.sesion ? ns.sesion.obtenerUsuario() : null;
        var switchContenedor = el('dash-roles-switch');
        if (!switchContenedor) return;

        var roles = (usuario && usuario.roles) ? usuario.roles : [];
        var esAdmin = roles.indexOf('Administrador') !== -1;

        // Si es Administrador, permitimos conmutar entre vistas para supervisar lo que ve cada rol
        if (esAdmin) {
            switchContenedor.hidden = false;
            var chips = switchContenedor.querySelectorAll('.btn-rol-chip');
            chips.forEach(function (chip) {
                chip.addEventListener('click', function () {
                    chips.forEach(function (c) { c.classList.remove('activo'); });
                    chip.classList.add('activo');
                    rolActivo = chip.getAttribute('data-rol') || 'Administrador';
                    if (datosCargados) {
                        renderizarVista(rolActivo, datosCargados);
                    }
                });
            });
            rolActivo = 'Administrador';
        } else {
            // Si es otro rol, activamos directamente su vista y ocultamos el conmutador
            switchContenedor.hidden = true;
            if (roles.indexOf('Supervisor') !== -1) {
                rolActivo = 'Supervisor';
            } else if (roles.indexOf('Empleado') !== -1) {
                rolActivo = 'Empleado';
            } else if (roles.indexOf('Consultor') !== -1) {
                rolActivo = 'Consultor';
            } else {
                rolActivo = 'Empleado';
            }
        }
    }

    // --- Inicialización pública ---
    function iniciar() {
        var btn = el('btn-actualizar-dash');
        if (btn) {
            btn.addEventListener('click', function () {
                cargarIndicadores();
            });
        }

        configurarSelectorRoles();
        cargarIndicadores();
    }

    ns.dashboard = {
        iniciar: iniciar,
        cargarIndicadores: cargarIndicadores
    };

})(window.VisorSIG);
