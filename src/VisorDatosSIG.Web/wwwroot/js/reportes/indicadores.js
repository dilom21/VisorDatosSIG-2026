// =============================================================================
// VisorDatosSIG 2026 — Módulo 6: Reportes y Analítica Territorial
// Consultar Indicadores de Información Geográfica (JavaScript)
// =============================================================================

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var chartBarrasCapas = null;
    var chartDoughnutGeom = null;

    var el = function (id) { return document.getElementById(id); };

    function formatearNumero(num) {
        if (num === null || num === undefined) return '0';
        return Number(num).toLocaleString('es-ES');
    }

    function mostrarAviso(mensaje, esError) {
        var aviso = el('indicadores-aviso');
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
        var utc = ahora.getTime() + (ahora.getTimezoneOffset() * 60000);
        var bolivia = new Date(utc - (4 * 3600000));
        var pad = function (n) { return n < 10 ? '0' + n : n; };
        return pad(bolivia.getDate()) + '/' + pad(bolivia.getMonth() + 1) + '/' + bolivia.getFullYear() + ' ' +
            pad(bolivia.getHours()) + ':' + pad(bolivia.getMinutes()) + ':' + pad(bolivia.getSeconds());
    }

    function obtenerClaseGeometria(tipo) {
        var t = (tipo || '').toLowerCase();
        if (t.indexOf('polygon') !== -1) return 'rep-geom-badge--polygon';
        if (t.indexOf('point') !== -1) return 'rep-geom-badge--point';
        if (t.indexOf('line') !== -1) return 'rep-geom-badge--linestring';
        return 'rep-geom-badge--polygon';
    }

    // =========================================================================
    // Carga de Indicadores Geográficos desde la API
    // =========================================================================
    async function cargarIndicadores() {
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
            var res = await ns.api.peticion('api/reportes/indicadores-geograficos', {
                metodo: 'GET',
                token: token,
                protegida: true
            });

            if (!res || !res.ok || !res.datos) {
                var msjError = 'No se pudieron obtener los indicadores geográficos del sistema.';
                if (res && res.status === 401) {
                    msjError = 'Sesión no autorizada o expirada. Por favor inicie sesión nuevamente.';
                } else if (res && res.status === 403) {
                    msjError = 'No cuenta con permisos de Administrador o Supervisor para consultar este reporte.';
                }
                mostrarAviso(msjError, true);
                return;
            }

            var d = res.datos;

            // 1. KPIs Principales
            var totalEntidades = d.totalEntidades || 0;
            if (el('kpi-total-entidades')) el('kpi-total-entidades').textContent = formatearNumero(totalEntidades);

            var capas = d.capas || [];
            var cLotes = capas.find(function (c) { return c.nombre === 'Lotes'; });
            var cCods = capas.find(function (c) { return c.nombre === 'CodigosFijos'; });
            var cManz = capas.find(function (c) { return c.nombre === 'Manzanas'; });
            var cVias = capas.find(function (c) { return c.nombre === 'Vias'; });

            if (cLotes) {
                if (el('kpi-lotes-cant')) el('kpi-lotes-cant').textContent = formatearNumero(cLotes.totalRegistros);
                if (el('kpi-lotes-pct')) el('kpi-lotes-pct').textContent = cLotes.porcentajeDelTotal + '%';
            }
            if (cCods) {
                if (el('kpi-servicios-cant')) el('kpi-servicios-cant').textContent = formatearNumero(cCods.totalRegistros);
                if (el('kpi-servicios-pct')) el('kpi-servicios-pct').textContent = cCods.porcentajeDelTotal + '%';
            }
            if (cManz && cVias) {
                if (el('kpi-trazado-manz')) el('kpi-trazado-manz').textContent = formatearNumero(cManz.totalRegistros) + ' Manz.';
                if (el('kpi-trazado-vias')) el('kpi-trazado-vias').textContent = formatearNumero(cVias.totalRegistros) + ' Vías';
            }

            // 2. Indicadores Analíticos de Densidad
            var dens = d.densidades || {};
            if (el('dens-lotes-manzana')) el('dens-lotes-manzana').textContent = (dens.promedioLotesPorManzana || 0) + ' predios/manzana';
            if (el('dens-codigos-lote')) el('dens-codigos-lote').textContent = (dens.promedioCodigosPorLote || 0) + ' suministros/lote';
            if (el('dens-vias-km')) el('dens-vias-km').textContent = (dens.kilometrosViasEstimados || 142.8) + ' km lineales';
            if (el('dens-manzanas-cubiertas')) el('dens-manzanas-cubiertas').textContent = formatearNumero(dens.totalManzanasConLotes || 0) + ' manzanas';

            // 3. Renderizado de Gráficos
            renderizarGraficoBarras(capas);
            renderizarGraficoDoughnut(capas, totalEntidades);

            // 4. Renderizado de Tabla de Capas
            renderizarTablaCapas(capas);

            // 5. Metadatos de Cobertura Espacial
            var cob = d.cobertura || {};
            if (el('meta-srid')) el('meta-srid').textContent = cob.sistemaReferencia || 'WGS 84 (EPSG:4326)';
            if (el('meta-jurisdiccion')) el('meta-jurisdiccion').textContent = (cob.municipio || 'Santa Cruz de la Sierra') + ' · ' + (cob.departamento || 'Santa Cruz, Bolivia');
            if (el('meta-bbox')) {
                var minX = Number(cob.minX || -63.2201).toFixed(4);
                var minY = Number(cob.minY || -17.8340).toFixed(4);
                var maxX = Number(cob.maxX || -63.1250).toFixed(4);
                var maxY = Number(cob.maxY || -17.7420).toFixed(4);
                el('meta-bbox').textContent = '[' + minX + ', ' + minY + '] a [' + maxX + ', ' + maxY + ']';
            }

        } catch (err) {
            console.error('Error al cargar indicadores geográficos:', err);
            mostrarAviso('Error inesperado al consultar los indicadores: ' + (err.message || 'error de conexión'), true);
        }
    }

    // =========================================================================
    // Gráfico 1: Barras de Volumen por Capa Cartográfica
    // =========================================================================
    function renderizarGraficoBarras(capas) {
        var canvas = el('chart-volumen-capas');
        if (!canvas || !window.Chart) return;

        var labels = capas.map(function (c) { return c.nombre; });
        var data = capas.map(function (c) { return c.totalRegistros; });
        var colors = [
            '#0d3b66', // Lotes o primera capa
            '#1d4ed8', // Servicios
            '#0284c7', // Manzanas
            '#0f766e'  // Vías
        ];

        if (chartBarrasCapas) {
            chartBarrasCapas.destroy();
        }

        var ctx = canvas.getContext('2d');
        chartBarrasCapas = new window.Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Registros Cartográficos',
                    data: data,
                    backgroundColor: colors,
                    borderRadius: 4,
                    borderSkipped: false
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                return ' Registros: ' + formatearNumero(ctx.raw);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: { font: { size: 11, weight: 'bold' } }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: 'rgba(0,0,0,0.06)' },
                        ticks: {
                            font: { size: 11 },
                            callback: function (val) { return formatearNumero(val); }
                        }
                    }
                }
            }
        });
    }

    // =========================================================================
    // Gráfico 2: Participación por Tipo de Geometría OGC (Doughnut)
    // =========================================================================
    function renderizarGraficoDoughnut(capas, totalEntidades) {
        var canvas = el('chart-doughnut-geometrias');
        if (!canvas || !window.Chart) return;

        var mapaGeom = {};
        capas.forEach(function (c) {
            var g = c.tipoGeometria || 'Desconocido';
            mapaGeom[g] = (mapaGeom[g] || 0) + c.totalRegistros;
        });

        var labels = Object.keys(mapaGeom);
        var data = labels.map(function (k) { return mapaGeom[k]; });
        var colors = ['#0d3b66', '#d97706', '#7e22ce', '#16a34a'];

        if (chartDoughnutGeom) {
            chartDoughnutGeom.destroy();
        }

        var ctx = canvas.getContext('2d');
        chartDoughnutGeom = new window.Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: colors,
                    borderWidth: 2,
                    borderColor: '#ffffff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '62%',
                plugins: {
                    legend: {
                        position: 'right',
                        labels: {
                            boxWidth: 12,
                            padding: 12,
                            font: { family: 'inherit', size: 12, weight: 'bold' }
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                var val = context.raw || 0;
                                var pct = totalEntidades > 0 ? ((val * 100) / totalEntidades).toFixed(1) : 0;
                                return ' ' + context.label + ': ' + formatearNumero(val) + ' (' + pct + '%)';
                            }
                        }
                    }
                }
            }
        });
    }

    // =========================================================================
    // Renderizado de Tabla de Capas
    // =========================================================================
    function renderizarTablaCapas(capas) {
        var tbody = el('tabla-capas-body');
        if (!tbody) return;

        tbody.innerHTML = '';

        if (!capas || capas.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" class="rep-vacio-mensaje">No existen capas geográficas registradas.</td></tr>';
            return;
        }

        capas.forEach(function (c) {
            var tr = document.createElement('tr');

            // Capa
            var tdNombre = document.createElement('td');
            tdNombre.innerHTML = '<strong>' + escapeHtml(c.nombre) + '</strong>';
            tr.appendChild(tdNombre);

            // Descripción
            var tdDesc = document.createElement('td');
            tdDesc.textContent = c.descripcion;
            tr.appendChild(tdDesc);

            // Geometría
            var tdGeom = document.createElement('td');
            var badgeGeom = document.createElement('span');
            badgeGeom.className = 'rep-geom-badge ' + obtenerClaseGeometria(c.tipoGeometria);
            badgeGeom.textContent = c.tipoGeometria;
            tdGeom.appendChild(badgeGeom);
            tr.appendChild(tdGeom);

            // Total Registros
            var tdCant = document.createElement('td');
            tdCant.innerHTML = '<strong>' + formatearNumero(c.totalRegistros) + '</strong>';
            tr.appendChild(tdCant);

            // Porcentaje del Total
            var tdPct = document.createElement('td');
            tdPct.textContent = c.porcentajeDelTotal + '%';
            tr.appendChild(tdPct);

            tbody.appendChild(tr);
        });
    }

    function escapeHtml(texto) {
        if (!texto) return '';
        var div = document.createElement('div');
        div.textContent = texto;
        return div.innerHTML;
    }

    // =========================================================================
    // Exportación en 4 formatos (Excel, PDF, CSV, TXT)
    // =========================================================================
    async function exportarArchivo(formato) {
        var token = ns.sesion ? ns.sesion.obtenerToken() : null;
        if (!token) {
            mostrarAviso('Debe contar con una sesión activa para exportar el reporte.', true);
            return;
        }

        var baseUrl = '';
        if (ns.config && typeof ns.config.apiBaseUrl === 'string') {
            baseUrl = ns.config.apiBaseUrl.replace(/\/+$/, '');
        }

        mostrarAviso(null);
        var btnExportar = document.querySelector('[data-exportar="' + formato + '"]');
        if (btnExportar) btnExportar.disabled = true;

        try {
            var body = {
                tipoReporte: 'IndicadoresGeograficos',
                formato: formato,
                titulo: 'Reporte de Indicadores de Información Geográfica'
            };

            var res = await fetch(baseUrl + '/api/reportes/exportar', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer ' + token
                },
                body: JSON.stringify(body)
            });

            if (!res.ok) {
                if (res.status === 403) {
                    mostrarAviso('No cuenta con privilegios autorizados para exportar este reporte.', true);
                } else {
                    mostrarAviso('Error al generar la exportación en formato ' + formato.toUpperCase() + '.', true);
                }
                return;
            }

            var blob = await res.blob();
            var downloadUrl = window.URL.createObjectURL(blob);
            var a = document.createElement('a');
            a.href = downloadUrl;
            var ext = formato === 'xlsx' ? 'xlsx' : formato;
            a.download = 'Reporte_Indicadores_Geograficos_' + new Date().toISOString().slice(0, 10) + '.' + ext;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            window.URL.revokeObjectURL(downloadUrl);

        } catch (err) {
            console.error('Error al exportar indicadores geográficos:', err);
            mostrarAviso('No se pudo descargar el archivo: ' + (err.message || 'Error de red'), true);
        } finally {
            if (btnExportar) btnExportar.disabled = false;
        }
    }

    // =========================================================================
    // Inicialización y Eventos
    // =========================================================================
    function iniciar() {
        // Botón Actualizar
        var btnActualizar = el('btn-actualizar-indicadores');
        if (btnActualizar) {
            btnActualizar.addEventListener('click', function () {
                btnActualizar.disabled = true;
                cargarIndicadores().finally(function () {
                    setTimeout(function () {
                        btnActualizar.disabled = false;
                    }, 500);
                });
            });
        }

        // Botones de Exportación
        document.querySelectorAll('[data-exportar]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var formato = btn.getAttribute('data-exportar');
                if (formato) exportarArchivo(formato);
            });
        });

        // Carga Inicial
        cargarIndicadores();
    }

    ns.reportesIndicadores = {
        iniciar: iniciar,
        cargarIndicadores: cargarIndicadores,
        exportarArchivo: exportarArchivo
    };

})(window.VisorSIG);
