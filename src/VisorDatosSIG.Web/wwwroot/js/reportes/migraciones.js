// =============================================================================
// VisorDatosSIG 2026 — Módulo 6: Reportes y Analítica Territorial
// Consultar Historial de Migraciones (JavaScript)
// =============================================================================

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var eventosOriginales = [];
    var eventosFiltrados = [];
    var filtroCapa = '';
    var filtroEstado = '';
    var busquedaTexto = '';
    var timerBusqueda = null;

    var chartVolumenInstance = null;
    var chartDoughnutInstance = null;

    var el = function (id) { return document.getElementById(id); };

    function formatearNumero(num) {
        if (num === null || num === undefined) return '0';
        return Number(num).toLocaleString('es-ES');
    }

    function mostrarAviso(mensaje, esError) {
        var aviso = el('migraciones-aviso');
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

    function formatearFechaHora(fechaStr) {
        if (!fechaStr) return '-';
        var d = new Date(fechaStr);
        if (isNaN(d.getTime())) return fechaStr;
        var pad = function (n) { return n < 10 ? '0' + n : n; };
        return pad(d.getDate()) + '/' + pad(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' +
            pad(d.getHours()) + ':' + pad(d.getMinutes());
    }

    function obtenerClaseEstado(estado) {
        var est = (estado || '').toUpperCase();
        if (est.indexOf('EXITO') !== -1) return 'badge-estado--exito';
        if (est.indexOf('ADVERT') !== -1) return 'badge-estado--advertencia';
        if (est.indexOf('ERROR') !== -1 || est.indexOf('FALL') !== -1) return 'badge-estado--error';
        return 'badge-estado--exito';
    }

    // =========================================================================
    // Carga de Procesos de Migración desde la API
    // =========================================================================
    async function cargarHistorial() {
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
            var res = await ns.api.peticion('api/reportes/migraciones?limite=100', {
                metodo: 'GET',
                token: token,
                protegida: true
            });

            if (!res || !res.ok || !res.datos) {
                var msjError = 'No se pudo recuperar el historial de migraciones.';
                if (res && res.status === 401) {
                    msjError = 'Sesión no autorizada o expirada. Por favor inicie sesión nuevamente.';
                } else if (res && res.status === 403) {
                    msjError = 'No cuenta con permisos de Administrador o Responsable de Migración para consultar este historial.';
                }
                mostrarAviso(msjError, true);
                renderizarTablaVacia('No fue posible consultar el historial de migraciones.');
                return;
            }

            var d = res.datos;
            eventosOriginales = d.eventos || [];

            // 1. KPIs Principales
            var totalMig = d.totalMigraciones || 0;
            var totalReg = d.totalRegistrosMigrados || 0;
            var exitosas = d.migracionesExitosas || 0;
            var advertencias = d.migracionesConAdvertencia || 0;
            var pctExito = totalMig > 0 ? Math.round((exitosas * 100) / totalMig) : 0;

            if (el('kpi-total-migraciones')) el('kpi-total-migraciones').textContent = formatearNumero(totalMig);
            if (el('kpi-total-registros-migrados')) el('kpi-total-registros-migrados').textContent = formatearNumero(totalReg);
            if (el('kpi-migraciones-exitosas')) el('kpi-migraciones-exitosas').textContent = formatearNumero(exitosas);
            if (el('kpi-migraciones-pct')) el('kpi-migraciones-pct').textContent = pctExito + '% Tasa de Éxito';
            if (el('kpi-migraciones-advertencias')) el('kpi-migraciones-advertencias').textContent = formatearNumero(advertencias);

            // Poblar dropdown de capas si aún no se ha poblado
            poblarFiltroCapas(eventosOriginales);

            // Aplicar filtros actuales
            aplicarFiltros();

            // Renderizar gráficos
            renderizarGraficos(eventosOriginales, exitosas, advertencias);

        } catch (err) {
            console.error('Error al cargar historial de migraciones:', err);
            mostrarAviso('Error inesperado al consultar el historial: ' + (err.message || 'error de conexión'), true);
            renderizarTablaVacia('Error de conexión con el servidor.');
        }
    }

    // =========================================================================
    // Filtros Locales y Búsqueda
    // =========================================================================
    function poblarFiltroCapas(eventos) {
        var selectCapa = el('filtro-mig-capa');
        if (!selectCapa || selectCapa.options.length > 1) return;

        var capasUnicas = {};
        eventos.forEach(function (e) {
            if (e.capa) capasUnicas[e.capa] = true;
        });

        Object.keys(capasUnicas).sort().forEach(function (capa) {
            var opt = document.createElement('option');
            opt.value = capa;
            opt.textContent = capa;
            selectCapa.appendChild(opt);
        });
    }

    function aplicarFiltros() {
        eventosFiltrados = eventosOriginales.filter(function (e) {
            // Filtro por Capa
            if (filtroCapa && e.capa !== filtroCapa) return false;

            // Filtro por Estado
            if (filtroEstado) {
                var est = (e.estado || '').toUpperCase();
                if (filtroEstado === 'EXITO' && est.indexOf('EXITO') === -1) return false;
                if (filtroEstado === 'ADVERTENCIA' && est.indexOf('EXITO') !== -1) return false;
            }

            // Búsqueda textual
            if (busquedaTexto) {
                var b = busquedaTexto.toLowerCase();
                var enArch = (e.archivo || '').toLowerCase().indexOf(b) !== -1;
                var enCapa = (e.capa || '').toLowerCase().indexOf(b) !== -1;
                var enUser = (e.usuario || '').toLowerCase().indexOf(b) !== -1;
                var enMod = (e.modalidad || '').toLowerCase().indexOf(b) !== -1;
                var enId = String(e.idBitacora).indexOf(b) !== -1;
                if (!enArch && !enCapa && !enUser && !enMod && !enId) return false;
            }

            return true;
        });

        renderizarTabla(eventosFiltrados);
    }

    // =========================================================================
    // Gráficos Estadísticos del Historial
    // =========================================================================
    function renderizarGraficos(eventos, exitosas, advertencias) {
        renderizarGraficoVolumen(eventos);
        renderizarGraficoEstados(exitosas, advertencias);
    }

    function renderizarGraficoVolumen(eventos) {
        var canvas = el('chart-volumen-migraciones');
        if (!canvas || !window.Chart) return;

        // Mostrar hasta las últimas 10 migraciones en orden cronológico (invertido)
        var eventosOrdenados = eventos.slice(0, 10).reverse();
        var labels = eventosOrdenados.map(function (e) {
            var arch = e.archivo || e.capa;
            if (arch.length > 18) arch = arch.substring(0, 15) + '...';
            return arch;
        });
        var dataProc = eventosOrdenados.map(function (e) { return e.registrosProcesados; });
        var dataIns = eventosOrdenados.map(function (e) { return e.registrosInsertados || e.registrosProcesados; });

        if (chartVolumenInstance) {
            chartVolumenInstance.destroy();
        }

        var ctx = canvas.getContext('2d');
        chartVolumenInstance = new window.Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Insertados',
                        data: dataIns,
                        backgroundColor: '#0d3b66',
                        borderRadius: 4
                    },
                    {
                        label: 'Procesados',
                        data: dataProc,
                        backgroundColor: '#1d4ed8',
                        borderRadius: 4
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'top',
                        labels: { boxWidth: 12, font: { family: 'inherit', size: 12, weight: 'bold' } }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                return ' ' + ctx.dataset.label + ': ' + formatearNumero(ctx.raw);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: { font: { size: 10 } }
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

    function renderizarGraficoEstados(exitosas, advertencias) {
        var canvas = el('chart-doughnut-estados-mig');
        if (!canvas || !window.Chart) return;

        if (chartDoughnutInstance) {
            chartDoughnutInstance.destroy();
        }

        var ctx = canvas.getContext('2d');
        chartDoughnutInstance = new window.Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['Completadas con Éxito', 'Con Advertencias / Omitidos'],
                datasets: [{
                    data: [exitosas, advertencias],
                    backgroundColor: ['#16a34a', '#ea580c'],
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
                        position: 'bottom',
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
                                var total = exitosas + advertencias;
                                var pct = total > 0 ? ((val * 100) / total).toFixed(1) : 0;
                                return ' ' + context.label + ': ' + formatearNumero(val) + ' (' + pct + '%)';
                            }
                        }
                    }
                }
            }
        });
    }

    // =========================================================================
    // Renderizado de Tabla de Migraciones
    // =========================================================================
    function renderizarTabla(eventos) {
        var tbody = el('tabla-migraciones-body');
        if (!tbody) return;

        tbody.innerHTML = '';

        if (!eventos || eventos.length === 0) {
            renderizarTablaVacia('No existen procesos de migración registrados con los criterios seleccionados.');
            return;
        }

        eventos.forEach(function (m) {
            var tr = document.createElement('tr');

            // ID
            var tdId = document.createElement('td');
            tdId.textContent = '#' + m.idBitacora;
            tr.appendChild(tdId);

            // Fecha/Hora
            var tdFecha = document.createElement('td');
            tdFecha.textContent = formatearFechaHora(m.fechaHoraBolivia);
            tr.appendChild(tdFecha);

            // Capa Destino
            var tdCapa = document.createElement('td');
            tdCapa.innerHTML = '<strong>' + escapeHtml(m.capa) + '</strong>';
            tr.appendChild(tdCapa);

            // Archivo Origen
            var tdArch = document.createElement('td');
            tdArch.textContent = m.archivo || '(Vector directo)';
            tr.appendChild(tdArch);

            // Modalidad
            var tdMod = document.createElement('td');
            tdMod.textContent = m.modalidad || 'Normal';
            tr.appendChild(tdMod);

            // Registros
            var tdReg = document.createElement('td');
            var ins = m.registrosInsertados || m.registrosProcesados;
            tdReg.innerHTML = '<strong>' + formatearNumero(ins) + '</strong> <small style="color:var(--rep-text-muted);">de ' + formatearNumero(m.registrosProcesados) + '</small>';
            tr.appendChild(tdReg);

            // Duración
            var tdDur = document.createElement('td');
            tdDur.textContent = (m.duracionSegundos ? m.duracionSegundos.toFixed(1) + ' s' : '-');
            tr.appendChild(tdDur);

            // Estado
            var tdEst = document.createElement('td');
            var badge = document.createElement('span');
            badge.className = 'badge-estado ' + obtenerClaseEstado(m.estado);
            badge.textContent = m.estado || 'EXITO';
            tdEst.appendChild(badge);
            tr.appendChild(tdEst);

            // Usuario
            var tdUser = document.createElement('td');
            tdUser.textContent = m.usuario || 'Sistema';
            tr.appendChild(tdUser);

            // Acción: Ver Detalle
            var tdAcc = document.createElement('td');
            var btnDetalle = document.createElement('button');
            btnDetalle.type = 'button';
            btnDetalle.className = 'btn-rep-ver-detalle';
            btnDetalle.innerHTML = '<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg> Ver';
            btnDetalle.title = 'Consultar información detallada de esta carga';
            btnDetalle.addEventListener('click', function () {
                abrirDetalle(m.idBitacora);
            });
            tdAcc.appendChild(btnDetalle);
            tr.appendChild(tdAcc);

            tbody.appendChild(tr);
        });

        var conteoInfo = el('tabla-migraciones-conteo');
        if (conteoInfo) {
            conteoInfo.textContent = 'Mostrando ' + formatearNumero(eventos.length) + ' de ' + formatearNumero(eventosOriginales.length) + ' procesos registrados';
        }
    }

    function renderizarTablaVacia(mensaje) {
        var tbody = el('tabla-migraciones-body');
        if (!tbody) return;
        tbody.innerHTML = '<tr><td colspan="10" class="rep-vacio-mensaje">' +
            '<div class="rep-vacio-icono"><svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg></div>' +
            escapeHtml(mensaje) + '</td></tr>';

        var conteoInfo = el('tabla-migraciones-conteo');
        if (conteoInfo) conteoInfo.textContent = '0 procesos registrados';
    }

    // =========================================================================
    // Modal de Información Registrada de la Carga (Flujo Principal)
    // =========================================================================
    function abrirDetalle(idBitacora) {
        var m = eventosOriginales.find(function (item) { return item.idBitacora === idBitacora; });
        if (!m) return;

        if (ns.modal && typeof ns.modal.detalle === 'function') {
            ns.modal.detalle({
                titulo: 'Información de Proceso de Migración #' + m.idBitacora,
                subtitulo: 'Archivo: ' + (m.archivo || 'Carga Vectorial Espacial'),
                textoCerrar: 'Cerrar Información',
                filas: [
                    { etiqueta: 'ID Bitácora', valor: '#' + m.idBitacora },
                    { etiqueta: 'Fecha y Hora Oficial (UTC-4)', valor: formatearFechaHora(m.fechaHoraBolivia) },
                    { etiqueta: 'Capa Cartográfica Destino', valor: m.capa },
                    { etiqueta: 'Archivo de Origen', valor: m.archivo || 'N/A' },
                    { etiqueta: 'Modalidad de Carga', valor: m.modalidad || 'Normal' },
                    { etiqueta: 'Registros de Origen', valor: formatearNumero(m.registrosOrigen) },
                    { etiqueta: 'Registros Procesados', valor: formatearNumero(m.registrosProcesados) },
                    { etiqueta: 'Registros Insertados en BD', valor: formatearNumero(m.registrosInsertados) },
                    { etiqueta: 'Registros Omitidos', valor: formatearNumero(m.registrosOmitidos) },
                    { etiqueta: 'Registros Fallidos / Errores', valor: formatearNumero(m.registrosFallidos) },
                    { etiqueta: 'Duración del Proceso', valor: (m.duracionSegundos ? m.duracionSegundos.toFixed(2) + ' segundos' : 'N/A') },
                    { etiqueta: 'Resultado / Estado', valor: m.estado },
                    { etiqueta: 'Operador Responsable', valor: m.usuario }
                ]
            });
        }
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
                tipoReporte: 'HistorialMigraciones',
                formato: formato,
                titulo: 'Reporte de Historial de Migraciones'
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
                    mostrarAviso('No cuenta con privilegios autorizados (Administrador / Responsable de Migración) para exportar este reporte.', true);
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
            a.download = 'Reporte_Historial_Migraciones_' + new Date().toISOString().slice(0, 10) + '.' + ext;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            window.URL.revokeObjectURL(downloadUrl);

        } catch (err) {
            console.error('Error al exportar historial de migraciones:', err);
            mostrarAviso('No se pudo descargar el archivo: ' + (err.message || 'Error de red'), true);
        } finally {
            if (btnExportar) btnExportar.disabled = false;
        }
    }

    // =========================================================================
    // Inicialización y Eventos
    // =========================================================================
    function iniciar() {
        // Filtro por Capa
        var selectCapa = el('filtro-mig-capa');
        if (selectCapa) {
            selectCapa.addEventListener('change', function () {
                filtroCapa = selectCapa.value;
                aplicarFiltros();
            });
        }

        // Filtro por Estado
        var selectEstado = el('filtro-mig-estado');
        if (selectEstado) {
            selectEstado.addEventListener('change', function () {
                filtroEstado = selectEstado.value;
                aplicarFiltros();
            });
        }

        // Búsqueda textual
        var inputBuscar = el('filtro-mig-buscar');
        if (inputBuscar) {
            inputBuscar.addEventListener('input', function () {
                clearTimeout(timerBusqueda);
                timerBusqueda = setTimeout(function () {
                    busquedaTexto = inputBuscar.value.trim();
                    aplicarFiltros();
                }, 300);
            });
        }

        // Botón Limpiar Filtros
        var btnLimpiar = el('btn-limpiar-mig-filtros');
        if (btnLimpiar) {
            btnLimpiar.addEventListener('click', function () {
                if (selectCapa) selectCapa.value = '';
                if (selectEstado) selectEstado.value = '';
                if (inputBuscar) inputBuscar.value = '';
                filtroCapa = '';
                filtroEstado = '';
                busquedaTexto = '';
                aplicarFiltros();
            });
        }

        // Botón Actualizar
        var btnActualizar = el('btn-actualizar-migraciones');
        if (btnActualizar) {
            btnActualizar.addEventListener('click', function () {
                btnActualizar.disabled = true;
                cargarHistorial().finally(function () {
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
        cargarHistorial();
    }

    ns.reportesMigraciones = {
        iniciar: iniciar,
        cargarHistorial: cargarHistorial,
        abrirDetalle: abrirDetalle,
        exportarArchivo: exportarArchivo
    };

})(window.VisorSIG);
