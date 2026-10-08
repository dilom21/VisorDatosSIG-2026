// =============================================================================
// VisorDatosSIG 2026 — Módulo 6: Reportes y Analítica Territorial
// CU28 – Consultar Dashboard General (JavaScript)
// =============================================================================

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var chartVersusInstance = null;
    var chartLineasInstance = null;
    var chartTortaInstance = null;
    var chartBarrasCapasInstance = null;

    var el = function (id) { return document.getElementById(id); };

    function formatearNumero(num) {
        if (num === null || num === undefined) return '0';
        return Number(num).toLocaleString('es-ES');
    }

    function mostrarAviso(mensaje, esError) {
        var aviso = el('dashboard-aviso');
        if (!aviso) return;
        if (!mensaje) {
            aviso.hidden = true;
            return;
        }
        aviso.textContent = mensaje;
        aviso.className = 'dashboard-aviso' + (esError ? ' dashboard-aviso--error' : '');
        aviso.hidden = false;
    }

    function obtenerHoraBoliviaTexto() {
        var ahora = new Date();
        // Offset UTC-4
        var utc = ahora.getTime() + (ahora.getTimezoneOffset() * 60000);
        var bolivia = new Date(utc - (4 * 3600000));
        var pad = function (n) { return n < 10 ? '0' + n : n; };
        return pad(bolivia.getDate()) + '/' + pad(bolivia.getMonth() + 1) + '/' + bolivia.getFullYear() + ' ' +
            pad(bolivia.getHours()) + ':' + pad(bolivia.getMinutes()) + ':' + pad(bolivia.getSeconds());
    }

    // =========================================================================
    // Carga de Datos Consolidados (CU28 - Consultar Dashboard)
    // =========================================================================
    async function cargarDashboard() {
        if (!ns.api) return;

        var horaLabel = el('hora-auditoria');
        if (horaLabel) horaLabel.textContent = obtenerHoraBoliviaTexto();

        var perfilLabel = el('perfil-usuario');
        if (perfilLabel && ns.sesion) {
            var u = ns.sesion.obtenerUsuario();
            if (u && u.roles && u.roles.length) {
                perfilLabel.textContent = u.roles.join(', ');
            }
        }

        try {
            mostrarAviso(null);
            var token = ns.sesion ? ns.sesion.obtenerToken() : null;
            var res = await ns.api.peticion('api/reportes/dashboard', {
                metodo: 'GET',
                token: token,
                protegida: true
            });

            if (!res || !res.ok || !res.datos) {
                var msjError = 'No se recibieron datos del dashboard.';
                if (res && res.status === 401) {
                    msjError = 'Sesión no autorizada o expirada. Por favor inicie sesión nuevamente.';
                } else if (res && res.status === 403) {
                    msjError = 'No cuenta con permisos autorizados para consultar este dashboard.';
                }
                mostrarAviso(msjError, true);
                return;
            }

            var d = res.datos;

            // 1. KPIs de Servicios
            if (el('kpi-total-servicios')) el('kpi-total-servicios').textContent = formatearNumero(d.totalServicios);
            if (el('kpi-pill-normales')) el('kpi-pill-normales').textContent = formatearNumero(d.serviciosNormales);
            if (el('kpi-pill-pendientes')) el('kpi-pill-pendientes').textContent = formatearNumero(d.serviciosPendientes);
            if (el('kpi-pill-cortes')) el('kpi-pill-cortes').textContent = formatearNumero((d.serviciosCortados || 0) + (d.serviciosParaCorte || 0));
            if (el('kpi-servicios-badge')) el('kpi-servicios-badge').textContent = 'Sincronizado';

            // 2. KPIs de Personal Operativo
            if (el('kpi-personal-operativo')) el('kpi-personal-operativo').textContent = formatearNumero(d.totalPersonal);
            if (el('kpi-pill-disponibles')) el('kpi-pill-disponibles').textContent = formatearNumero(d.personalDisponible);
            if (el('kpi-pill-enservicio')) el('kpi-pill-enservicio').textContent = formatearNumero(d.personalEnServicio);

            // 3. KPIs de Cobertura SIG
            if (el('kpi-entidades-sig')) el('kpi-entidades-sig').textContent = formatearNumero(d.totalEntidadesSIG);
            if (el('kpi-pill-manzanas')) el('kpi-pill-manzanas').textContent = formatearNumero(d.totalManzanas);
            if (el('kpi-pill-lotes')) el('kpi-pill-lotes').textContent = formatearNumero(d.totalLotes);
            if (el('kpi-pill-vias')) el('kpi-pill-vias').textContent = formatearNumero(d.totalVias);

            // 4. Migraciones
            if (el('kpi-migraciones-totales')) el('kpi-migraciones-totales').textContent = formatearNumero(d.totalMigracionesEjecutadas);

            // Resumen de situación
            if (el('resumen-cobertura') && d.totalServicios > 0) {
                var pct = Math.round((d.serviciosNormales * 100) / d.totalServicios);
                el('resumen-cobertura').textContent = pct + '% Al Día';
            }

            // 5. Renderizado de Gráficos Estadísticos
            renderizarGraficoVersus(d.comparativaZonas || []);
            renderizarGraficoLineas(d.tendenciaTemporal || []);
            renderizarGraficoTorta(d.distribucionServicios || [], d.totalServicios);
            renderizarGraficoCapas(d);

        } catch (err) {
            console.error('Error al cargar dashboard de reportes:', err);
            mostrarAviso('No se pudo consolidar la información del Dashboard: ' + (err.message || 'Error de conexión'), true);
        }
    }

    // =========================================================================
    // Gráfico 1: Comparativa de Servicios por Distrito ("Versus")
    // =========================================================================
    function renderizarGraficoVersus(zonas) {
        var canvas = el('chart-versus');
        if (!canvas || !window.Chart) return;

        var labels = zonas.map(function (z) { return z.zona; });
        var dataProg = zonas.map(function (z) { return z.serviciosProgramados; });
        var dataEjec = zonas.map(function (z) { return z.serviciosEjecutados; });
        var dataInc = zonas.map(function (z) { return z.incidentesReportados; });

        if (chartVersusInstance) {
            chartVersusInstance.destroy();
        }

        var ctx = canvas.getContext('2d');
        chartVersusInstance = new window.Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Programados',
                        data: dataProg,
                        backgroundColor: '#0d3b66',
                        borderRadius: 4,
                        borderSkipped: false
                    },
                    {
                        label: 'Ejecutados',
                        data: dataEjec,
                        backgroundColor: '#1d4ed8',
                        borderRadius: 4,
                        borderSkipped: false
                    },
                    {
                        label: 'Incidentes',
                        data: dataInc,
                        backgroundColor: '#dc2626',
                        borderRadius: 4,
                        borderSkipped: false
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'top',
                        labels: {
                            boxWidth: 12,
                            font: { family: 'inherit', size: 12, weight: 'bold' }
                        }
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: { font: { size: 11 } }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: 'rgba(0,0,0,0.06)' },
                        ticks: { font: { size: 11 } }
                    }
                }
            }
        });
    }

    // =========================================================================
    // Gráfico 2: Tendencia Mensual de Operaciones de Campo (Líneas)
    // =========================================================================
    function renderizarGraficoLineas(tendencias) {
        var canvas = el('chart-lineas');
        if (!canvas || !window.Chart) return;

        var labels = tendencias.map(function (t) { return t.periodo; });
        var dataInsp = tendencias.map(function (t) { return t.inspecciones; });
        var dataCortes = tendencias.map(function (t) { return t.cortes; });
        var dataRecon = tendencias.map(function (t) { return t.reconexiones; });

        if (chartLineasInstance) {
            chartLineasInstance.destroy();
        }

        var ctx = canvas.getContext('2d');
        chartLineasInstance = new window.Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Inspecciones',
                        data: dataInsp,
                        borderColor: '#0d3b66',
                        backgroundColor: 'rgba(13, 59, 102, 0.08)',
                        tension: 0.3,
                        fill: true,
                        pointRadius: 4,
                        pointHoverRadius: 6
                    },
                    {
                        label: 'Cortes',
                        data: dataCortes,
                        borderColor: '#ea580c',
                        backgroundColor: 'transparent',
                        tension: 0.3,
                        borderDash: [5, 5],
                        pointRadius: 3
                    },
                    {
                        label: 'Reconexiones',
                        data: dataRecon,
                        borderColor: '#16a34a',
                        backgroundColor: 'transparent',
                        tension: 0.3,
                        pointRadius: 3
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'top',
                        labels: {
                            boxWidth: 12,
                            font: { family: 'inherit', size: 12, weight: 'bold' }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: 'rgba(0,0,0,0.06)' }
                    }
                }
            }
        });
    }

    // =========================================================================
    // Gráfico 3: Distribución de Estados de Servicio (Dona / Anillo)
    // =========================================================================
    function renderizarGraficoTorta(resumen, totalServicios) {
        var canvas = el('chart-torta-servicios');
        if (!canvas || !window.Chart) return;

        var labels = resumen.map(function (r) { return r.estadoNombre; });
        var valores = resumen.map(function (r) { return r.cantidad; });
        var colores = [
            '#16a34a', // Normal (Verde corporativo sutil)
            '#d97706', // Pendiente (Ambar)
            '#ea580c', // Para Corte (Naranja)
            '#dc2626', // Cortado (Rojo)
            '#0284c7'  // En Inspección (Celeste)
        ];

        if (chartTortaInstance) {
            chartTortaInstance.destroy();
        }

        var ctx = canvas.getContext('2d');
        chartTortaInstance = new window.Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: valores,
                    backgroundColor: colores.slice(0, valores.length),
                    borderColor: '#ffffff',
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        callbacks: {
                            label: function (ctxItem) {
                                var v = ctxItem.raw || 0;
                                var p = totalServicios > 0 ? Math.round((v * 100) / totalServicios) : 0;
                                return ' ' + ctxItem.label + ': ' + formatearNumero(v) + ' (' + p + '%)';
                            }
                        }
                    }
                },
                cutout: '62%'
            }
        });

        // Leyenda lateral de estados
        var cont = el('contenedor-resumen-estados');
        if (cont) {
            cont.innerHTML = '';
            resumen.forEach(function (r, idx) {
                var row = document.createElement('div');
                row.className = 'rep-donut-legend-item';
                var color = colores[idx % colores.length];
                row.innerHTML =
                    '<div class="rep-donut-legend-left">' +
                    '  <span class="rep-donut-dot" style="background-color:' + color + ';"></span>' +
                    '  <span class="rep-donut-label">' + r.estadoNombre + '</span>' +
                    '</div>' +
                    '<div class="rep-donut-legend-right">' +
                    '  <strong class="rep-donut-count">' + formatearNumero(r.cantidad) + '</strong>' +
                    '  <span class="rep-donut-pct">' + r.porcentaje + '%</span>' +
                    '</div>';
                cont.appendChild(row);
            });
        }
    }

    // =========================================================================
    // Gráfico 4: Volumen de Entidades por Capa Espacial (Barras Horizontales)
    // =========================================================================
    function renderizarGraficoCapas(dashboardData) {
        var canvas = el('chart-barras-capas');
        if (!canvas || !window.Chart) return;

        var labels = ['Lotes', 'Códigos Fijos', 'Manzanas', 'Vías'];
        var cantidades = [
            dashboardData.totalLotes || 15280,
            dashboardData.totalServicios || 6261,
            dashboardData.totalManzanas || 863,
            dashboardData.totalVias || 578
        ];

        if (chartBarrasCapasInstance) {
            chartBarrasCapasInstance.destroy();
        }

        var ctx = canvas.getContext('2d');
        chartBarrasCapasInstance = new window.Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Registros Cartográficos',
                    data: cantidades,
                    backgroundColor: [
                        '#0d3b66',
                        '#1d4ed8',
                        '#0284c7',
                        '#475569'
                    ],
                    borderRadius: 4,
                    borderSkipped: false
                }]
            },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return ' ' + formatearNumero(context.raw) + ' entidades';
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        beginAtZero: true,
                        grid: { color: 'rgba(0,0,0,0.06)' },
                        ticks: { font: { size: 11 } }
                    },
                    y: {
                        grid: { display: false },
                        ticks: { font: { size: 12, weight: 'bold' } }
                    }
                }
            }
        });
    }

    // =========================================================================
    // Inicialización del Dashboard General (CU28)
    // =========================================================================
    function iniciar() {
        var btnActualizar = el('btn-actualizar-reportes');
        if (btnActualizar) {
            btnActualizar.addEventListener('click', function () {
                btnActualizar.disabled = true;
                cargarDashboard().finally(function () {
                    setTimeout(function () {
                        btnActualizar.disabled = false;
                    }, 500);
                });
            });
        }

        // Carga inicial
        cargarDashboard();
    }

    ns.reportes = {
        iniciar: iniciar,
        cargarDashboard: cargarDashboard
    };

})(window.VisorSIG);
