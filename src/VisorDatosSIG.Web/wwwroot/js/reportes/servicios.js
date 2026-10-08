// =============================================================================
// VisorDatosSIG 2026 — Módulo 6: Reportes y Analítica Territorial
// Consultar Estado de Servicios (JavaScript)
// =============================================================================

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var chartDistribucion = null;
    var estadoFiltro = null;
    var busquedaTexto = '';
    var paginaActual = 1;
    var tamanoPagina = 25;
    var totalPaginas = 1;
    var totalRegistros = 0;
    var timerBusqueda = null;

    var el = function (id) { return document.getElementById(id); };

    function formatearNumero(num) {
        if (num === null || num === undefined) return '0';
        return Number(num).toLocaleString('es-ES');
    }

    function mostrarAviso(mensaje, esError) {
        var aviso = el('servicios-aviso');
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

    function obtenerClaseBadge(estadoId) {
        switch (estadoId) {
            case 1: return 'badge-estado--normal';
            case 2: return 'badge-estado--pendiente';
            case 3: return 'badge-estado--corte';
            case 4: return 'badge-estado--cortado';
            case 5: return 'badge-estado--inspeccion';
            default: return 'badge-estado--normal';
        }
    }

    // =========================================================================
    // Carga de Servicios desde la API
    // =========================================================================
    async function cargarServicios() {
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
            var query = '?pagina=' + paginaActual + '&tamano=' + tamanoPagina;
            if (estadoFiltro !== null && estadoFiltro > 0) {
                query += '&estado=' + estadoFiltro;
            }
            if (busquedaTexto && busquedaTexto.trim().length > 0) {
                query += '&busqueda=' + encodeURIComponent(busquedaTexto.trim());
            }

            var token = ns.sesion ? ns.sesion.obtenerToken() : null;
            var res = await ns.api.peticion('api/reportes/servicios' + query, {
                metodo: 'GET',
                token: token,
                protegida: true
            });

            if (!res || !res.ok || !res.datos) {
                var msjError = 'No se pudieron obtener los servicios registrados.';
                if (res && res.status === 401) {
                    msjError = 'Sesión no autorizada o expirada. Por favor inicie sesión nuevamente.';
                } else if (res && res.status === 403) {
                    msjError = 'No cuenta con permisos de Administrador o Supervisor para consultar este reporte.';
                }
                mostrarAviso(msjError, true);
                renderizarTablaVacia('No fue posible cargar los datos del servidor.');
                return;
            }

            var d = res.datos;
            totalRegistros = d.totalRegistros || 0;
            totalPaginas = d.totalPaginas || 1;
            paginaActual = d.paginaActual || 1;

            // Actualizar KPIs de Resumen
            actualizarKpis(d.resumenEstados || []);

            // Actualizar Gráfico
            renderizarGrafico(d.resumenEstados || []);

            // Actualizar Tabla
            renderizarTabla(d.registros || []);

            // Actualizar Paginación
            renderizarPaginacion();

        } catch (err) {
            console.error('Error al cargar estado de servicios:', err);
            mostrarAviso('Error inesperado al consultar los servicios: ' + (err.message || 'error de conexión'), true);
            renderizarTablaVacia('Error de conexión con el servidor.');
        }
    }

    // =========================================================================
    // Actualización de KPIs
    // =========================================================================
    function actualizarKpis(resumen) {
        var totalGeneral = 0;
        var conteos = { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0 };
        var pcts = { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0 };

        resumen.forEach(function (r) {
            totalGeneral += r.cantidad;
            conteos[r.estadoId] = r.cantidad;
            pcts[r.estadoId] = r.porcentaje;
        });

        if (el('kpi-total-servicios')) el('kpi-total-servicios').textContent = formatearNumero(totalGeneral);

        if (el('kpi-normal-cant')) el('kpi-normal-cant').textContent = formatearNumero(conteos[1]);
        if (el('kpi-normal-pct')) el('kpi-normal-pct').textContent = pcts[1] + '%';

        if (el('kpi-pendiente-cant')) el('kpi-pendiente-cant').textContent = formatearNumero(conteos[2]);
        if (el('kpi-pendiente-pct')) el('kpi-pendiente-pct').textContent = pcts[2] + '%';

        if (el('kpi-corte-cant')) el('kpi-corte-cant').textContent = formatearNumero(conteos[3]);
        if (el('kpi-corte-pct')) el('kpi-corte-pct').textContent = pcts[3] + '%';

        if (el('kpi-cortado-cant')) el('kpi-cortado-cant').textContent = formatearNumero(conteos[4]);
        if (el('kpi-cortado-pct')) el('kpi-cortado-pct').textContent = pcts[4] + '%';

        // Poblado del dropdown de estados si aún no tiene opciones completas
        var selectEstado = el('filtro-estado');
        if (selectEstado && selectEstado.options.length <= 1) {
            resumen.forEach(function (r) {
                var opt = document.createElement('option');
                opt.value = r.estadoId;
                opt.textContent = r.estadoNombre + ' (' + formatearNumero(r.cantidad) + ')';
                selectEstado.appendChild(opt);
            });
        }
    }

    // =========================================================================
    // Gráfico de Distribución (Chart.js)
    // =========================================================================
    function renderizarGrafico(resumen) {
        var canvas = el('chart-distribucion-servicios');
        if (!canvas || !window.Chart) return;

        var labels = resumen.map(function (r) { return r.estadoNombre; });
        var data = resumen.map(function (r) { return r.cantidad; });
        var colors = resumen.map(function (r) { return r.colorHex || '#0d3b66'; });

        if (chartDistribucion) {
            chartDistribucion.destroy();
        }

        var ctx = canvas.getContext('2d');
        chartDistribucion = new window.Chart(ctx, {
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
                cutout: '65%',
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
                                var total = context.dataset.data.reduce(function (a, b) { return a + b; }, 0);
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
    // Renderizado de Tabla de Servicios
    // =========================================================================
    function renderizarTabla(registros) {
        var tbody = el('tabla-servicios-body');
        if (!tbody) return;

        tbody.innerHTML = '';

        if (!registros || registros.length === 0) {
            renderizarTablaVacia('No existen servicios registrados con los criterios seleccionados.');
            return;
        }

        registros.forEach(function (s) {
            var tr = document.createElement('tr');

            // ID
            var tdId = document.createElement('td');
            tdId.textContent = s.idCodigo;
            tr.appendChild(tdId);

            // Código Fijo / SIG
            var tdCod = document.createElement('td');
            var codFijoTexto = s.codFijo ? s.codFijo.toString() : (s.codF_SIG || s.codF_SQL || '-');
            tdCod.innerHTML = '<strong>' + escapeHtml(codFijoTexto) + '</strong>' +
                (s.codF_SIG ? '<br><small style="color:var(--rep-text-muted);">' + escapeHtml(s.codF_SIG) + '</small>' : '');
            tr.appendChild(tdCod);

            // Nombre / Titular
            var tdNombre = document.createElement('td');
            tdNombre.textContent = s.nombre || '(Sin titular)';
            tr.appendChild(tdNombre);

            // Estado Operativo
            var tdEstado = document.createElement('td');
            var badge = document.createElement('span');
            badge.className = 'badge-estado ' + obtenerClaseBadge(s.estado);
            badge.textContent = s.estadoNombre || 'Estado ' + s.estado;
            tdEstado.appendChild(badge);
            tr.appendChild(tdEstado);

            // Lote Catastral
            var tdLote = document.createElement('td');
            tdLote.textContent = s.idLote ? 'Lote #' + s.idLote : '-';
            tr.appendChild(tdLote);

            // Coordenadas
            var tdCoord = document.createElement('td');
            if (s.longitud !== null && s.latitud !== null) {
                tdCoord.textContent = s.longitud.toFixed(5) + ', ' + s.latitud.toFixed(5);
            } else {
                tdCoord.textContent = '-';
            }
            tr.appendChild(tdCoord);

            tbody.appendChild(tr);
        });

        // Actualizar etiqueta de conteo
        var conteoLabel = el('tabla-conteo-info');
        if (conteoLabel) {
            var inicio = (paginaActual - 1) * tamanoPagina + 1;
            var fin = Math.min(paginaActual * tamanoPagina, totalRegistros);
            conteoLabel.textContent = 'Mostrando ' + formatearNumero(inicio) + ' - ' + formatearNumero(fin) + ' de ' + formatearNumero(totalRegistros) + ' servicios';
        }
    }

    function renderizarTablaVacia(mensaje) {
        var tbody = el('tabla-servicios-body');
        if (!tbody) return;
        tbody.innerHTML = '<tr><td colspan="6" class="rep-vacio-mensaje">' +
            '<div class="rep-vacio-icono"><svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg></div>' +
            escapeHtml(mensaje) + '</td></tr>';

        var conteoLabel = el('tabla-conteo-info');
        if (conteoLabel) conteoLabel.textContent = '0 servicios encontrados';

        var nav = el('paginacion-nav');
        if (nav) nav.innerHTML = '';
    }

    function escapeHtml(texto) {
        if (!texto) return '';
        var div = document.createElement('div');
        div.textContent = texto;
        return div.innerHTML;
    }

    // =========================================================================
    // Renderizado de Paginación
    // =========================================================================
    function renderizarPaginacion() {
        var nav = el('paginacion-nav');
        if (!nav) return;

        nav.innerHTML = '';

        if (totalPaginas <= 1) return;

        // Botón Anterior
        var btnPrev = document.createElement('button');
        btnPrev.type = 'button';
        btnPrev.className = 'rep-btn-pag';
        btnPrev.innerHTML = '&laquo;';
        btnPrev.title = 'Página anterior';
        btnPrev.disabled = paginaActual <= 1;
        btnPrev.addEventListener('click', function () {
            if (paginaActual > 1) {
                paginaActual--;
                cargarServicios();
            }
        });
        nav.appendChild(btnPrev);

        // Rango de páginas a mostrar (máximo 5)
        var inicio = Math.max(1, paginaActual - 2);
        var fin = Math.min(totalPaginas, inicio + 4);
        if (fin - inicio < 4) {
            inicio = Math.max(1, fin - 4);
        }

        for (var p = inicio; p <= fin; p++) {
            (function (num) {
                var btnNum = document.createElement('button');
                btnNum.type = 'button';
                btnNum.className = 'rep-btn-pag' + (num === paginaActual ? ' activo' : '');
                btnNum.textContent = num;
                btnNum.addEventListener('click', function () {
                    if (num !== paginaActual) {
                        paginaActual = num;
                        cargarServicios();
                    }
                });
                nav.appendChild(btnNum);
            })(p);
        }

        // Botón Siguiente
        var btnNext = document.createElement('button');
        btnNext.type = 'button';
        btnNext.className = 'rep-btn-pag';
        btnNext.innerHTML = '&raquo;';
        btnNext.title = 'Página siguiente';
        btnNext.disabled = paginaActual >= totalPaginas;
        btnNext.addEventListener('click', function () {
            if (paginaActual < totalPaginas) {
                paginaActual++;
                cargarServicios();
            }
        });
        nav.appendChild(btnNext);
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
                tipoReporte: 'EstadoServicios',
                formato: formato,
                titulo: 'Reporte de Estado de Servicios',
                filtroEstado: (estadoFiltro !== null && estadoFiltro > 0) ? estadoFiltro : null
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
            a.download = 'Reporte_Estado_Servicios_' + new Date().toISOString().slice(0, 10) + '.' + ext;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            window.URL.revokeObjectURL(downloadUrl);

        } catch (err) {
            console.error('Error al exportar archivo:', err);
            mostrarAviso('No se pudo descargar el archivo: ' + (err.message || 'Error de red'), true);
        } finally {
            if (btnExportar) btnExportar.disabled = false;
        }
    }

    // =========================================================================
    // Inicialización y Eventos
    // =========================================================================
    function iniciar() {
        // Filtro por Estado
        var selectEstado = el('filtro-estado');
        if (selectEstado) {
            selectEstado.addEventListener('change', function () {
                estadoFiltro = selectEstado.value ? parseInt(selectEstado.value, 10) : null;
                paginaActual = 1;
                cargarServicios();
            });
        }

        // Búsqueda textual
        var inputBuscar = el('filtro-buscar');
        if (inputBuscar) {
            inputBuscar.addEventListener('input', function () {
                clearTimeout(timerBusqueda);
                timerBusqueda = setTimeout(function () {
                    busquedaTexto = inputBuscar.value.trim();
                    paginaActual = 1;
                    cargarServicios();
                }, 350);
            });
            inputBuscar.addEventListener('keypress', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    clearTimeout(timerBusqueda);
                    busquedaTexto = inputBuscar.value.trim();
                    paginaActual = 1;
                    cargarServicios();
                }
            });
        }

        // Botón Limpiar Filtros
        var btnLimpiar = el('btn-limpiar-filtros');
        if (btnLimpiar) {
            btnLimpiar.addEventListener('click', function () {
                if (selectEstado) selectEstado.value = '';
                if (inputBuscar) inputBuscar.value = '';
                estadoFiltro = null;
                busquedaTexto = '';
                paginaActual = 1;
                cargarServicios();
            });
        }

        // Selector de tamaño de página
        var selectTamano = el('select-tamano-pagina');
        if (selectTamano) {
            selectTamano.addEventListener('change', function () {
                tamanoPagina = parseInt(selectTamano.value, 10) || 25;
                paginaActual = 1;
                cargarServicios();
            });
        }

        // Botón Actualizar
        var btnActualizar = el('btn-actualizar-servicios');
        if (btnActualizar) {
            btnActualizar.addEventListener('click', function () {
                btnActualizar.disabled = true;
                cargarServicios().finally(function () {
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
        cargarServicios();
    }

    ns.reportesServicios = {
        iniciar: iniciar,
        cargarServicios: cargarServicios,
        exportarArchivo: exportarArchivo
    };

})(window.VisorSIG);
