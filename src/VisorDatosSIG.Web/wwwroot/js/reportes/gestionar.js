// =============================================================================
// VisorDatosSIG 2026 — Módulo 6: Reportes y Analítica Territorial
// CU32: Gestionar Reportes (JavaScript Interactivo)
// Soporta selección dinámica, vista previa en tiempo real y exportación en 4 formatos
// =============================================================================

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var codigoSeleccionado = 'REP-CAT-01';
    var categoriaSeleccionada = 'catastro';
    var datosVistaPrevia = null;
    var cargandoPreview = false;

    var el = function (id) { return document.getElementById(id); };

    function obtenerHoraBoliviaTexto() {
        var ahora = new Date();
        var utc = ahora.getTime() + (ahora.getTimezoneOffset() * 60000);
        var bolivia = new Date(utc - (4 * 3600000));
        var pad = function (n) { return n < 10 ? '0' + n : n; };
        return pad(bolivia.getDate()) + '/' + pad(bolivia.getMonth() + 1) + '/' + bolivia.getFullYear() + ' ' +
            pad(bolivia.getHours()) + ':' + pad(bolivia.getMinutes()) + ':' + pad(bolivia.getSeconds());
    }

    function mostrarAviso(mensaje, esError) {
        var aviso = el('gestionar-aviso');
        if (!aviso) return;
        if (!mensaje) {
            aviso.hidden = true;
            return;
        }
        aviso.textContent = mensaje;
        aviso.className = 'dashboard-aviso' + (esError ? ' dashboard-aviso--error' : '');
        aviso.hidden = false;
    }

    // Configuración contextual de los filtros por tipo de reporte
    function actualizarFiltrosContextuales(codigo) {
        var lblCriterio = el('lbl-criterio-especifico');
        var selCriterio = el('filtro-criterio-especifico');
        var inputBusqueda = el('filtro-busqueda-texto');

        if (!lblCriterio || !selCriterio) return;

        selCriterio.innerHTML = '';

        if (codigo === 'REP-SRV-01' || codigo === 'REP-SRV-03') {
            lblCriterio.textContent = 'Estado Operativo del Servicio';
            selCriterio.innerHTML = [
                '<option value="">Todos los estados</option>',
                '<option value="1">Normal (Operativo)</option>',
                '<option value="2">Pendiente</option>',
                '<option value="3">Para Corte</option>',
                '<option value="4">Cortado</option>',
                '<option value="5">En Inspección</option>'
            ].join('');
            if (inputBusqueda) inputBusqueda.placeholder = 'Buscar por titular, código SIG, código fijo...';
        } else if (codigo === 'REP-SRV-02') {
            lblCriterio.textContent = 'Disponibilidad del Personal';
            selCriterio.innerHTML = [
                '<option value="">Todas las disponibilidades</option>',
                '<option value="Disponible">Disponible</option>',
                '<option value="En Campo">En Campo</option>',
                '<option value="De Permiso">De Permiso</option>',
                '<option value="No Disponible">No Disponible</option>'
            ].join('');
            if (inputBusqueda) inputBusqueda.placeholder = 'Buscar por nombre, cargo, carnet CI...';
        } else if (codigo === 'REP-CAT-01') {
            lblCriterio.textContent = 'Unidad Territorial (UV)';
            selCriterio.innerHTML = [
                '<option value="">Todas las Unidades Vecinales</option>',
                '<option value="1">UV 1</option>',
                '<option value="2">UV 2</option>',
                '<option value="3">UV 3</option>',
                '<option value="4">UV 4</option>',
                '<option value="5">UV 5</option>'
            ].join('');
            if (inputBusqueda) inputBusqueda.placeholder = 'Buscar por número o nombre de UV...';
        } else if (codigo === 'REP-CAT-02') {
            lblCriterio.textContent = 'Padrón Predial';
            selCriterio.innerHTML = [
                '<option value="">Todo el padrón predial</option>',
                '<option value="Regular">Servicio Activo Regular</option>',
                '<option value="Observación">Con Observación / Corte</option>',
                '<option value="Sin Suministro">Sin Suministro</option>'
            ].join('');
            if (inputBusqueda) inputBusqueda.placeholder = 'Buscar por UV, MZA, Lote o Clave Catastral...';
        } else if (codigo === 'REP-CAT-03') {
            lblCriterio.textContent = 'Jerarquía de Vía';
            selCriterio.innerHTML = [
                '<option value="">Todas las jerarquías viales</option>',
                '<option value="Avenida">Avenidas</option>',
                '<option value="Calle">Calles</option>',
                '<option value="Pasaje">Pasajes y peatonales</option>'
            ].join('');
            if (inputBusqueda) inputBusqueda.placeholder = 'Buscar por nombre de calle, código OSM...';
        } else if (codigo === 'REP-AUD-01') {
            lblCriterio.textContent = 'Resultado de Transacción';
            selCriterio.innerHTML = [
                '<option value="">Todos los resultados</option>',
                '<option value="EXITO">EXITO (Completado)</option>',
                '<option value="FALLIDO">FALLIDO (Error)</option>',
                '<option value="ADVERTENCIA">ADVERTENCIA</option>'
            ].join('');
            if (inputBusqueda) inputBusqueda.placeholder = 'Buscar por usuario, módulo o IP...';
        } else {
            lblCriterio.textContent = 'Criterio General';
            selCriterio.innerHTML = [
                '<option value="">Todos los registros</option>',
                '<option value="activos">Solo activos</option>'
            ].join('');
            if (inputBusqueda) inputBusqueda.placeholder = 'Filtrar por texto o identificador...';
        }
    }

    // Selección de una tarjeta de reporte
    function seleccionarReporte(codigo, omitirRecarga) {
        codigoSeleccionado = codigo;

        var badgeCodigo = el('badge-codigo-actual');
        if (badgeCodigo) badgeCodigo.textContent = codigo;

        document.querySelectorAll('.reporte-card').forEach(function (card) {
            var cCod = card.getAttribute('data-codigo');
            var statusEl = card.querySelector('.reporte-card__status');
            if (cCod === codigo) {
                card.classList.add('reporte-card--activo');
                if (statusEl) statusEl.textContent = '● Reporte Seleccionado';
            } else {
                card.classList.remove('reporte-card--activo');
                if (statusEl) statusEl.textContent = 'Haga clic para seleccionar';
            }
        });

        actualizarFiltrosContextuales(codigo);

        // Si ya hay datos previos y se cambia de tarjeta, limpiar vista previa
        if (!omitirRecarga) {
            datosVistaPrevia = null;
            var tablaWrapper = el('preview-tabla-wrapper');
            var estadoVacio = el('preview-estado-vacio');
            var toolbar = el('preview-toolbar');
            var footer = el('preview-footer');
            if (tablaWrapper) tablaWrapper.hidden = true;
            if (toolbar) toolbar.hidden = true;
            if (footer) footer.hidden = true;
            if (estadoVacio) estadoVacio.hidden = false;

            var badgeCodigoPrev = el('preview-badge-codigo');
            var badgeReg = el('preview-badge-registros');
            var badgeFec = el('preview-badge-fecha');
            if (badgeCodigoPrev) badgeCodigoPrev.textContent = codigo;
            if (badgeReg) badgeReg.textContent = '0 registros';
            if (badgeFec) badgeFec.textContent = 'Sin generar';

            var tit = el('preview-titulo');
            var sub = el('preview-subtitulo');
            if (tit) tit.textContent = 'Vista Previa de Verificación';
            if (sub) sub.textContent = 'Haga clic en "Generar Vista Previa" para cargar los datos de ' + codigo + '.';
        }
    }

    // Filtrado de tarjetas por categoría (Pills)
    function filtrarPorCategoria(categoria) {
        categoriaSeleccionada = categoria;

        document.querySelectorAll('.btn-cat-pill').forEach(function (btn) {
            if (btn.getAttribute('data-categoria') === categoria) {
                btn.classList.add('btn-cat-pill--activo');
            } else {
                btn.classList.remove('btn-cat-pill--activo');
            }
        });

        var primerCodigoDeCategoria = null;
        document.querySelectorAll('.reporte-card').forEach(function (card) {
            var cat = card.getAttribute('data-categoria');
            if (cat === categoria) {
                card.style.display = '';
                if (!primerCodigoDeCategoria) {
                    primerCodigoDeCategoria = card.getAttribute('data-codigo');
                }
            } else {
                card.style.display = 'none';
            }
        });

        // Si la tarjeta actual no pertenece a la nueva categoría, seleccionar la primera
        var tarjetaActual = document.querySelector('.reporte-card[data-codigo="' + codigoSeleccionado + '"]');
        if (!tarjetaActual || tarjetaActual.getAttribute('data-categoria') !== categoria) {
            if (primerCodigoDeCategoria) {
                seleccionarReporte(primerCodigoDeCategoria);
            }
        }
    }

    // Generar la vista previa desde la API
    async function generarVistaPrevia() {
        if (cargandoPreview) return;

        var token = ns.sesion ? ns.sesion.obtenerToken() : null;
        if (!token) {
            mostrarAviso('Debe contar con una sesión activa para consultar reportes.', true);
            return;
        }

        var baseUrl = '';
        if (ns.config && typeof ns.config.apiBaseUrl === 'string') {
            baseUrl = ns.config.apiBaseUrl.replace(/\/+$/, '');
        }

        var btnPreview = el('btn-generar-preview');
        var textoOriginal = btnPreview ? btnPreview.innerHTML : '';
        if (btnPreview) {
            btnPreview.disabled = true;
            btnPreview.innerHTML = '<span class="rep-spinner-inline"></span> Generando Vista Previa...';
        }

        cargandoPreview = true;
        mostrarAviso(null);

        var selCriterio = el('filtro-criterio-especifico');
        var criterioVal = selCriterio ? selCriterio.value : '';
        var inputBusqueda = el('filtro-busqueda-texto');
        var busquedaVal = inputBusqueda ? inputBusqueda.value.trim() : '';

        var estadoFiltroNum = null;
        if ((codigoSeleccionado === 'REP-SRV-01' || codigoSeleccionado === 'REP-SRV-03') && criterioVal && !isNaN(parseInt(criterioVal, 10))) {
            estadoFiltroNum = parseInt(criterioVal, 10);
        } else if (criterioVal && !busquedaVal) {
            busquedaVal = criterioVal;
        }

        var payload = {
            tipoReporte: codigoSeleccionado,
            estadoFiltro: estadoFiltroNum,
            busqueda: busquedaVal.length > 0 ? busquedaVal : null
        };

        try {
            var res = await fetch(baseUrl + '/api/reportes/vista-previa', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer ' + token
                },
                body: JSON.stringify(payload)
            });

            if (!res.ok) {
                if (res.status === 403) {
                    mostrarAviso('No cuenta con autorización suficiente para consultar este reporte.', true);
                } else if (res.status === 401) {
                    mostrarAviso('Sesión expirada. Por favor inicie sesión nuevamente.', true);
                } else {
                    var errorData = await res.json().catch(function () { return {}; });
                    mostrarAviso(errorData.mensaje || 'Error al obtener la vista previa del reporte.', true);
                }
                renderizarTablaVacia('No fue posible procesar la consulta.');
                return;
            }

            datosVistaPrevia = await res.json();
            renderizarVistaPrevia(datosVistaPrevia);

        } catch (err) {
            console.error('Error al generar vista previa:', err);
            mostrarAviso('Error de conexión al generar la vista previa: ' + (err.message || 'Error de red'), true);
            renderizarTablaVacia('Error de conexión con el servidor.');
        } finally {
            cargandoPreview = false;
            if (btnPreview) {
                btnPreview.disabled = false;
                btnPreview.innerHTML = textoOriginal;
            }
        }
    }

    // Renderizar la tabla interactiva de vista previa
    function renderizarVistaPrevia(dto) {
        var estadoVacio = el('preview-estado-vacio');
        var tablaWrapper = el('preview-tabla-wrapper');
        var toolbar = el('preview-toolbar');
        var footer = el('preview-footer');
        var thead = el('tabla-preview-head');
        var tbody = el('tabla-preview-body');
        var badgeCodigo = el('preview-badge-codigo');
        var badgeRegistros = el('preview-badge-registros');
        var badgeFecha = el('preview-badge-fecha');
        var tit = el('preview-titulo');
        var sub = el('preview-subtitulo');

        if (!dto || !dto.columnas || !dto.filas) {
            renderizarTablaVacia('Sin datos para mostrar.');
            return;
        }

        var codReporte = dto.codigoReporte || codigoSeleccionado;
        if (badgeCodigo) badgeCodigo.textContent = codReporte;
        if (tit) tit.textContent = codReporte + ': ' + dto.titulo;
        if (sub) sub.textContent = dto.subtitulo || 'Consulta procesada en tiempo real.';

        var fechaStr = dto.fechaGeneracionBolivia ? new Date(dto.fechaGeneracionBolivia).toLocaleString('es-ES') : obtenerHoraBoliviaTexto();
        if (badgeFecha) badgeFecha.textContent = 'Generado: ' + fechaStr;

        var total = dto.totalRegistros || dto.filas.length;
        if (badgeRegistros) {
            badgeRegistros.textContent = total.toLocaleString('es-ES') + ' registros';
        }

        // Si no existen registros que cumplan los criterios (Excepción CU32)
        if (dto.filas.length === 0) {
            mostrarAviso('No existen resultados registrados que cumplan los criterios indicados para generar el reporte.', false);
            renderizarTablaVacia('No se encontraron registros coincidentes con los filtros aplicados.');
            return;
        }

        // Armar cabeceras
        if (thead) {
            var headHtml = '<tr>';
            dto.columnas.forEach(function (col) {
                headHtml += '<th scope="col">' + escaparHtml(col) + '</th>';
            });
            headHtml += '</tr>';
            thead.innerHTML = headHtml;
        }

        // Armar cuerpo de filas
        if (tbody) {
            var bodyHtml = '';
            dto.filas.forEach(function (fila) {
                bodyHtml += '<tr>';
                fila.forEach(function (celda) {
                    bodyHtml += '<td>' + formatearCelda(celda) + '</td>';
                });
                bodyHtml += '</tr>';
            });
            tbody.innerHTML = bodyHtml;
        }

        // Limpiar búsqueda rápida previa
        var inputFiltroRapido = el('filtro-rapido-preview');
        if (inputFiltroRapido) inputFiltroRapido.value = '';

        if (estadoVacio) estadoVacio.hidden = true;
        if (tablaWrapper) tablaWrapper.hidden = false;
        if (toolbar) toolbar.hidden = false;
        if (footer) footer.hidden = false;
    }

    function renderizarTablaVacia(mensaje) {
        var estadoVacio = el('preview-estado-vacio');
        var tablaWrapper = el('preview-tabla-wrapper');
        var toolbar = el('preview-toolbar');
        var footer = el('preview-footer');
        if (tablaWrapper) tablaWrapper.hidden = true;
        if (toolbar) toolbar.hidden = true;
        if (footer) footer.hidden = true;
        if (estadoVacio) {
            estadoVacio.hidden = false;
            var txt = estadoVacio.querySelector('.preview-estado-vacio__texto');
            if (txt) txt.textContent = mensaje;
        }
    }

    function formatearCelda(valor) {
        if (valor === null || valor === undefined) return '<span class="rep-muted">-</span>';
        var texto = String(valor);

        // Badges contextuales para estados conocidos
        if (texto === 'Normal' || texto === 'Activo' || texto === 'EXITO' || texto === 'Disponible') {
            return '<span class="badge-estado badge-estado--normal">' + escaparHtml(texto) + '</span>';
        }
        if (texto === 'Pendiente' || texto === 'En Inspección' || texto === 'ADVERTENCIA') {
            return '<span class="badge-estado badge-estado--pendiente">' + escaparHtml(texto) + '</span>';
        }
        if (texto === 'Para Corte') {
            return '<span class="badge-estado badge-estado--corte">' + escaparHtml(texto) + '</span>';
        }
        if (texto === 'Cortado' || texto === 'Inactivo' || texto === 'FALLIDO' || texto === 'Bloqueado') {
            return '<span class="badge-estado badge-estado--cortado">' + escaparHtml(texto) + '</span>';
        }

        return escaparHtml(texto);
    }

    function escaparHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    // Exportación a archivos (CSV, Excel, PDF, TXT)
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

        var selCriterio = el('filtro-criterio-especifico');
        var criterioVal = selCriterio ? selCriterio.value : '';
        var inputBusqueda = el('filtro-busqueda-texto');
        var busquedaVal = inputBusqueda ? inputBusqueda.value.trim() : '';

        var estadoFiltroNum = null;
        if ((codigoSeleccionado === 'REP-SRV-01' || codigoSeleccionado === 'REP-SRV-03') && criterioVal && !isNaN(parseInt(criterioVal, 10))) {
            estadoFiltroNum = parseInt(criterioVal, 10);
        } else if (criterioVal && !busquedaVal) {
            busquedaVal = criterioVal;
        }

        try {
            var body = {
                tipoReporte: codigoSeleccionado,
                formato: formato,
                estadoFiltro: estadoFiltroNum,
                busqueda: busquedaVal.length > 0 ? busquedaVal : null,
                titulo: 'Reporte Oficial ' + codigoSeleccionado
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
                    mostrarAviso('No cuenta con privilegios autorizados para exportar este tipo de reporte.', true);
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
            a.download = 'Reporte_' + codigoSeleccionado + '_' + new Date().toISOString().slice(0, 10) + '.' + ext;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            window.URL.revokeObjectURL(downloadUrl);

            mostrarAviso('Archivo descargado con éxito en formato ' + formato.toUpperCase() + '.', false);

        } catch (err) {
            console.error('Error al exportar archivo:', err);
            mostrarAviso('No se pudo descargar el archivo: ' + (err.message || 'Error de red'), true);
        } finally {
            if (btnExportar) btnExportar.disabled = false;
        }
    }

    // Inicialización del módulo CU32
    function iniciar() {
        var horaLabel = el('hora-auditoria');
        if (horaLabel) horaLabel.textContent = obtenerHoraBoliviaTexto();

        var perfilLabel = el('perfil-usuario');
        if (perfilLabel && ns.sesion) {
            var u = ns.sesion.obtenerUsuario();
            if (u && u.roles && u.roles.length) {
                perfilLabel.textContent = u.roles.join(', ');
            }
        }

        // Vincular tabs de categoría
        document.querySelectorAll('.btn-cat-pill').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var cat = btn.getAttribute('data-categoria');
                if (cat) filtrarPorCategoria(cat);
            });
        });

        // Vincular tarjetas de reporte
        document.querySelectorAll('.reporte-card').forEach(function (card) {
            card.addEventListener('click', function () {
                var cod = card.getAttribute('data-codigo');
                if (cod) seleccionarReporte(cod);
            });
        });

        // Botón Generar Vista Previa
        var btnPreview = el('btn-generar-preview');
        if (btnPreview) {
            btnPreview.addEventListener('click', function () {
                generarVistaPrevia();
            });
        }

        // Botones de Exportación
        document.querySelectorAll('[data-exportar]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var fmt = btn.getAttribute('data-exportar');
                if (fmt) exportarArchivo(fmt);
            });
        });

        // Búsqueda rápida local en la tabla de vista previa
        var inputFiltroRapido = el('filtro-rapido-preview');
        if (inputFiltroRapido) {
            inputFiltroRapido.addEventListener('input', function () {
                var q = (this.value || '').toLowerCase().trim();
                var tbody = el('tabla-preview-body');
                if (!tbody) return;
                var filas = tbody.querySelectorAll('tr');
                var countVisibles = 0;
                filas.forEach(function (tr) {
                    var match = !q || tr.textContent.toLowerCase().indexOf(q) !== -1;
                    tr.style.display = match ? '' : 'none';
                    if (match) countVisibles++;
                });
                var badgeRegistros = el('preview-badge-registros');
                if (badgeRegistros) {
                    if (q) {
                        badgeRegistros.textContent = countVisibles.toLocaleString('es-ES') + ' filtrados';
                    } else if (datosVistaPrevia) {
                        var total = datosVistaPrevia.totalRegistros || datosVistaPrevia.filas.length;
                        badgeRegistros.textContent = total.toLocaleString('es-ES') + ' registros';
                    }
                }
            });
        }

        // Iniciar en la primera categoría y primer reporte
        filtrarPorCategoria('catastro');
        seleccionarReporte('REP-CAT-01', true);
    }

    ns.gestionarReportes = {
        iniciar: iniciar,
        seleccionarReporte: seleccionarReporte,
        generarVistaPrevia: generarVistaPrevia,
        exportarArchivo: exportarArchivo
    };

})(window.VisorSIG);
