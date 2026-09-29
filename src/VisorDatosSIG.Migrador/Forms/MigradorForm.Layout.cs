using System.Drawing;
using System.Windows.Forms;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Migrador.Controls;

namespace VisorDatosSIG.Migrador.Forms;

/// <summary>
/// Construcción de la interfaz del Migrador (diseño en código, sin archivo del diseñador).
/// </summary>
/// <remarks>
/// Se usan únicamente controles estándar de Windows Forms: TableLayoutPanel, FlowLayoutPanel,
/// Panel, TabControl, DataGridView, ListView y StatusStrip.
/// </remarks>
public sealed partial class MigradorForm
{
    // Paleta poco saturada: se prioriza la legibilidad.
    private static readonly Color ColorMarca = Color.FromArgb(31, 59, 91);
    private static readonly Color ColorMarcaClara = Color.FromArgb(235, 241, 247);
    private static readonly Color ColorTexto = Color.FromArgb(48, 58, 68);
    private static readonly Color ColorOk = Color.FromArgb(24, 128, 74);
    private static readonly Color ColorAdvertencia = Color.FromArgb(176, 116, 10);
    private static readonly Color ColorError = Color.FromArgb(178, 60, 55);
    private static readonly Color ColorInfo = Color.FromArgb(37, 99, 160);
    private static readonly Color ColorFilaAlterna = Color.FromArgb(246, 248, 250);
    private static readonly Color ColorSuaveInfo = Color.FromArgb(232, 240, 250);
    private static readonly Color ColorSuaveAdvertencia = Color.FromArgb(253, 245, 227);
    private static readonly Color ColorSuaveError = Color.FromArgb(251, 236, 236);

    private readonly ToolTip _toolTip = new();

    // Barra de archivo y acciones.
    private readonly TextBox _txtRuta = new();
    private readonly Button _btnSeleccionar = new();
    private readonly Button _btnProbarConexion = new();
    private readonly Button _btnValidar = new();
    private readonly Label _lblModalidad = new();
    private readonly ComboBox _cboModalidad = new();
    private readonly Button _btnMigrar = new();
    private readonly Button _btnCancelar = new();
    private readonly Button _btnExportar = new();
    private readonly Button _btnReconstruirIndices = new();

    // Sesión del usuario en la cabecera: botón de acceso o identificación del usuario.
    private readonly Panel _areaSesion = new();
    private readonly GradientButton _btnIniciarSesion = new();
    private readonly UserChipButton _chipUsuario = new();

    // Tarjetas de resumen.
    private readonly SummaryCard _cardCapa = new("Capa detectada");
    private readonly SummaryCard _cardRegistros = new("Registros");
    private readonly SummaryCard _cardReferencia = new("SRID / Referencia");
    private readonly SummaryCard _cardEstado = new("Estado");
    private readonly SummaryCard _cardAnalizados = new("Analizados");
    private readonly SummaryCard _cardValidos = new("Válidos para migrar");
    private readonly SummaryCard _cardAdvertencias = new("Registros con advertencias");
    private readonly SummaryCard _cardOmitibles = new("Omitibles");

    // Panel de progreso visible con porcentaje.
    private readonly Panel _pnlProgreso = new();
    private readonly Label _lblProgresoTitulo = new();
    private readonly ProgressBar _pgbProgresoGrande = new();
    private readonly Label _lblProgresoPorcentaje = new();
    private readonly Label _lblProgresoDetalle = new();

    // Pestañas.
    private readonly TabControl _tabs = new();
    private readonly TabPage _tabInspeccion = new("Inspección");
    private readonly TabPage _tabPrevisualizacion = new("Previsualización");
    private readonly TabPage _tabValidacion = new("Validación");
    private readonly TabPage _tabIncidencias = new("Incidencias");
    private readonly TabPage _tabBitacora = new("Dashboard de Bitácora");

    // Controles de Pestaña Bitácora.
    private readonly SummaryCard _cardBitacoraTotal = new("Operaciones registradas");
    private readonly SummaryCard _cardBitacoraExitos = new("Migraciones exitosas");
    private readonly SummaryCard _cardBitacoraErrores = new("Errores / Cancelados");
    private readonly SummaryCard _cardBitacoraUltima = new("Última actividad");
    private readonly Button _btnRefrescarBitacora = new();
    private readonly ComboBox _cboFiltroBitacoraTabla = new();
    private readonly ComboBox _cboFiltroBitacoraResultado = new();
    private readonly Label _lblResumenBitacora = new();
    private readonly DataGridView _dgvBitacora = new();
    private readonly TextBox _txtDetalleBitacora = new();

    // Pestaña Inspección.
    private readonly Label _lblValorArchivo = new();
    private readonly Label _lblValorCapa = new();
    private readonly Label _lblValorRegistros = new();
    private readonly Label _lblValorGeometria = new();
    private readonly Label _lblValorReferencia = new();
    private readonly Label _lblValorExtension = new();
    private readonly Label _lblValorCodificacion = new();
    private readonly Label _lblValorEstado = new();
    private readonly Dictionary<ShapefileComponentType, Label> _lblComponentes = new();
    private readonly ListView _lstCampos = new();
    private readonly TextBox _txtMensajes = new();

    // Pestaña Previsualización.
    private readonly Label _lblResumenPrevisualizacion = new();
    private readonly DataGridView _dgvPrevisualizacion = new();

    // Pestaña Validación.
    private readonly Label _lblEstadoValidacion = new();
    private readonly DataGridView _dgvEstadisticas = new();

    // Pestaña Incidencias.
    private readonly Label _lblResumenIncidencias = new();
    private readonly ComboBox _cboFiltroIncidencias = new();
    private readonly DataGridView _dgvIncidencias = new();

    // Barra de estado.
    private readonly ToolStripStatusLabel _lblActividad = new();
    private readonly ToolStripProgressBar _barraProgreso = new();

    private void InicializarInterfaz()
    {
        SuspendLayout();

        Text = "VisorDatosSIG — Migrador";
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Font;
        MinimumSize = new Size(1100, 720);
        ClientSize = new Size(1240, 860);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;

        var layoutRaiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        layoutRaiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutRaiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layoutRaiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layoutRaiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layoutRaiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
        layoutRaiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layoutRaiz.Controls.Add(CrearCabecera(), 0, 0);
        layoutRaiz.Controls.Add(CrearBarraAcciones(), 0, 1);
        layoutRaiz.Controls.Add(CrearPanelProgreso(), 0, 2);
        layoutRaiz.Controls.Add(CrearPanelTarjetas(), 0, 3);
        layoutRaiz.Controls.Add(CrearPestanias(), 0, 4);

        Controls.Add(layoutRaiz);
        Controls.Add(CrearBarraEstado());

        ResumeLayout(true);
    }

    private Control CrearCabecera()
    {
        var cabecera = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorMarca,
            Padding = new Padding(18, 10, 18, 10),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        cabecera.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var panelTitulos = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        var titulo = new Label
        {
            Text = "VisorDatosSIG — Migrador",
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(0, 2)
        };

        var subtitulo = new Label
        {
            Text = "Inspección y validación de datos geográficos",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.FromArgb(203, 216, 231),
            AutoSize = true,
            Location = new Point(2, 34)
        };

        panelTitulos.Controls.Add(titulo);
        panelTitulos.Controls.Add(subtitulo);

        cabecera.Controls.Add(panelTitulos, 0, 0);
        cabecera.Controls.Add(CrearAreaSesion(), 1, 0);
        return cabecera;
    }

    /// <summary>
    /// Crea la zona derecha de la cabecera: «Iniciar sesión» cuando no hay sesión y la
    /// identificación del usuario (con su menú) cuando ya se inició sesión.
    /// </summary>
    private Control CrearAreaSesion()
    {
        _areaSesion.AutoSize = true;
        _areaSesion.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _areaSesion.BackColor = Color.Transparent;
        _areaSesion.Anchor = AnchorStyles.Right;
        _areaSesion.Margin = new Padding(0);

        _btnIniciarSesion.Text = "Iniciar sesión";
        _btnIniciarSesion.TextoEnProgreso = "Conectando...";
        _btnIniciarSesion.MostrarFlecha = false;
        _btnIniciarSesion.Size = new Size(152, 36);
        _btnIniciarSesion.Radio = 18;
        _btnIniciarSesion.Font = EstiloUI.FuenteBotonPequeno;
        _btnIniciarSesion.Location = Point.Empty;
        _btnIniciarSesion.AccessibleName = "Iniciar sesión";
        _btnIniciarSesion.Click += btnIniciarSesion_Click;
        _toolTip.SetToolTip(
            _btnIniciarSesion,
            $"Iniciar sesión en VisorDatosSIG · Servidor: {_apiClient.DireccionBase?.AbsoluteUri.TrimEnd('/')}");

        // El chip solo se muestra con sesión activa; la cabecera lo activa desde MigradorForm.Session.
        _chipUsuario.Visible = false;
        _chipUsuario.Location = Point.Empty;
        _chipUsuario.AccessibleName = "Menú del usuario autenticado";
        _chipUsuario.Click += btnUsuario_Click;

        _areaSesion.Controls.Add(_btnIniciarSesion);
        _areaSesion.Controls.Add(_chipUsuario);
        return _areaSesion;
    }

    private Control CrearBarraAcciones()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(18, 12, 18, 6),
            BackColor = ColorMarcaClara,
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(CrearFilaArchivo(), 0, 0);
        layout.Controls.Add(CrearFilaAcciones(), 0, 1);
        return layout;
    }

    private Control CrearFilaArchivo()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var etiqueta = new Label
        {
            Text = "Archivo SHP:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = ColorTexto,
            Margin = new Padding(0, 9, 8, 0)
        };

        _txtRuta.Dock = DockStyle.Fill;
        _txtRuta.ReadOnly = true;
        _txtRuta.BackColor = Color.White;
        _txtRuta.ForeColor = ColorTexto;
        _txtRuta.Margin = new Padding(0, 5, 8, 0);

        _btnSeleccionar.Text = "Examinar...";
        _btnSeleccionar.AutoSize = true;
        _btnSeleccionar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _btnSeleccionar.Padding = new Padding(14, 5, 14, 5);
        _btnSeleccionar.Margin = new Padding(0, 5, 0, 0);
        _btnSeleccionar.Click += btnSeleccionar_Click;

        layout.Controls.Add(etiqueta, 0, 0);
        layout.Controls.Add(_txtRuta, 1, 0);
        layout.Controls.Add(_btnSeleccionar, 2, 0);
        return layout;
    }

    private Control CrearFilaAcciones()
    {
        var flujo = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 10, 0, 0)
        };

        _btnProbarConexion.Text = "Probar conexión";
        _btnProbarConexion.AutoSize = true;
        _btnProbarConexion.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _btnProbarConexion.Padding = new Padding(12, 6, 12, 6);
        _btnProbarConexion.Margin = new Padding(0, 0, 8, 0);
        _btnProbarConexion.Click += btnProbarConexion_Click;

        _btnValidar.Text = "Validar datos";
        _btnValidar.AutoSize = true;
        _btnValidar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _btnValidar.Padding = new Padding(16, 6, 16, 6);
        _btnValidar.BackColor = ColorMarca;
        _btnValidar.ForeColor = Color.White;
        _btnValidar.FlatStyle = FlatStyle.Flat;
        _btnValidar.FlatAppearance.BorderSize = 0;
        _btnValidar.Enabled = false;
        _btnValidar.Margin = new Padding(0, 0, 8, 0);
        _btnValidar.Click += btnValidar_Click;

        _lblModalidad.Text = "Modo:";
        _lblModalidad.AutoSize = true;
        _lblModalidad.Anchor = AnchorStyles.Left;
        _lblModalidad.ForeColor = ColorTexto;
        _lblModalidad.Margin = new Padding(4, 8, 4, 0);

        _cboModalidad.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboModalidad.Items.AddRange(["Reemplazar (limpia y recarga)", "Anexar (sin duplicados)"]);
        _cboModalidad.SelectedIndex = 0;
        _cboModalidad.Width = 195;
        _cboModalidad.Margin = new Padding(0, 5, 8, 0);
        _cboModalidad.SelectedIndexChanged += cboModalidad_SelectedIndexChanged;

        _btnMigrar.Text = "Migrar datos";
        _btnMigrar.AutoSize = true;
        _btnMigrar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _btnMigrar.Padding = new Padding(14, 6, 14, 6);
        _btnMigrar.BackColor = ColorOk;
        _btnMigrar.ForeColor = Color.White;
        _btnMigrar.FlatStyle = FlatStyle.Flat;
        _btnMigrar.FlatAppearance.BorderSize = 0;
        _btnMigrar.Enabled = false;
        _btnMigrar.Margin = new Padding(0, 0, 8, 0);
        _btnMigrar.Click += btnMigrar_Click;

        _btnCancelar.Text = "Cancelar";
        _btnCancelar.AutoSize = true;
        _btnCancelar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _btnCancelar.Padding = new Padding(12, 6, 12, 6);
        _btnCancelar.Enabled = false;
        _btnCancelar.Margin = new Padding(0, 0, 8, 0);
        _btnCancelar.Click += btnCancelar_Click;

        _btnExportar.Text = "Exportar resumen...";
        _btnExportar.AutoSize = true;
        _btnExportar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _btnExportar.Padding = new Padding(12, 6, 12, 6);
        _btnExportar.Enabled = false;
        _btnExportar.Margin = new Padding(0, 0, 8, 0);
        _btnExportar.Click += btnExportar_Click;

        _btnReconstruirIndices.Text = "Reconstruir índices";
        _btnReconstruirIndices.AutoSize = true;
        _btnReconstruirIndices.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _btnReconstruirIndices.Padding = new Padding(12, 6, 12, 6);
        _btnReconstruirIndices.Margin = new Padding(0, 0, 0, 0);
        _btnReconstruirIndices.Click += btnReconstruirIndices_Click;

        flujo.Controls.Add(_btnProbarConexion);
        flujo.Controls.Add(_btnValidar);
        flujo.Controls.Add(_lblModalidad);
        flujo.Controls.Add(_cboModalidad);
        flujo.Controls.Add(_btnMigrar);
        flujo.Controls.Add(_btnCancelar);
        flujo.Controls.Add(_btnExportar);
        flujo.Controls.Add(_btnReconstruirIndices);
        return flujo;
    }

    private Control CrearPanelTarjetas()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(14, 8, 14, 6),
            BackColor = ColorMarcaClara,
            Margin = new Padding(0)
        };
        for (var columna = 0; columna < 4; columna++)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        SummaryCard[] tarjetas =
        [
            _cardCapa,
            _cardRegistros,
            _cardReferencia,
            _cardEstado,
            _cardAnalizados,
            _cardValidos,
            _cardAdvertencias,
            _cardOmitibles
        ];

        for (var indice = 0; indice < tarjetas.Length; indice++)
        {
            tarjetas[indice].Dock = DockStyle.Fill;
            layout.Controls.Add(tarjetas[indice], indice % 4, indice / 4);
        }

        return layout;
    }

    private Control CrearPanelProgreso()
    {
        _pnlProgreso.Dock = DockStyle.Fill;
        _pnlProgreso.AutoSize = true;
        _pnlProgreso.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _pnlProgreso.BackColor = Color.FromArgb(242, 247, 252);
        _pnlProgreso.Padding = new Padding(18, 10, 18, 10);
        _pnlProgreso.Margin = new Padding(0);
        _pnlProgreso.Visible = false;

        var tabla = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0)
        };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblProgresoTitulo.Text = "Progreso de la operación";
        _lblProgresoTitulo.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _lblProgresoTitulo.ForeColor = ColorMarca;
        _lblProgresoTitulo.AutoSize = true;
        _lblProgresoTitulo.Margin = new Padding(0, 0, 0, 6);

        var filaBarra = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 6)
        };
        filaBarra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        filaBarra.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95F));
        filaBarra.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));

        _pgbProgresoGrande.Dock = DockStyle.Fill;
        _pgbProgresoGrande.Height = 24;
        _pgbProgresoGrande.Style = ProgressBarStyle.Continuous;
        _pgbProgresoGrande.Maximum = 100;
        _pgbProgresoGrande.Value = 0;
        _pgbProgresoGrande.Margin = new Padding(0, 2, 10, 2);

        _lblProgresoPorcentaje.Dock = DockStyle.Fill;
        _lblProgresoPorcentaje.Text = "0 %";
        _lblProgresoPorcentaje.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        _lblProgresoPorcentaje.ForeColor = ColorMarca;
        _lblProgresoPorcentaje.TextAlign = ContentAlignment.MiddleCenter;
        _lblProgresoPorcentaje.Margin = new Padding(0);

        filaBarra.Controls.Add(_pgbProgresoGrande, 0, 0);
        filaBarra.Controls.Add(_lblProgresoPorcentaje, 1, 0);

        _lblProgresoDetalle.Text = "Listo para iniciar.";
        _lblProgresoDetalle.Font = new Font("Segoe UI", 9F);
        _lblProgresoDetalle.ForeColor = ColorTexto;
        _lblProgresoDetalle.AutoSize = true;
        _lblProgresoDetalle.Margin = new Padding(0);

        tabla.Controls.Add(_lblProgresoTitulo, 0, 0);
        tabla.Controls.Add(filaBarra, 0, 1);
        tabla.Controls.Add(_lblProgresoDetalle, 0, 2);

        _pnlProgreso.Controls.Add(tabla);
        return _pnlProgreso;
    }

    private Control CrearPestanias()
    {
        _tabs.Dock = DockStyle.Fill;
        _tabs.Padding = new Point(16, 6);
        _tabs.Font = new Font("Segoe UI", 9.5F);
        _tabs.Margin = new Padding(14, 6, 14, 0);
        _tabs.Selecting += tabs_Selecting;

        TabPage[] paginas = [_tabInspeccion, _tabPrevisualizacion, _tabValidacion, _tabIncidencias, _tabBitacora];
        foreach (var pagina in paginas)
        {
            pagina.BackColor = Color.White;
            pagina.Padding = new Padding(8);
        }

        _tabInspeccion.Controls.Add(CrearContenidoInspeccion());
        _tabPrevisualizacion.Controls.Add(CrearContenidoPrevisualizacion());
        _tabValidacion.Controls.Add(CrearContenidoValidacion());
        _tabIncidencias.Controls.Add(CrearContenidoIncidencias());
        _tabBitacora.Controls.Add(CrearContenidoBitacora());
        _tabs.TabPages.AddRange(paginas);

        return _tabs;
    }

    private Control CrearContenidoInspeccion()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var izquierda = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        izquierda.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        izquierda.RowStyles.Add(new RowStyle(SizeType.Absolute, 204F));
        izquierda.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));
        izquierda.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        izquierda.Controls.Add(CrearGrupoInformacion(), 0, 0);
        izquierda.Controls.Add(CrearGrupoComponentes(), 0, 1);
        izquierda.Controls.Add(CrearGrupoMensajes(), 0, 2);

        layout.Controls.Add(izquierda, 0, 0);
        layout.Controls.Add(CrearGrupoCampos(), 1, 0);
        return layout;
    }

    private Control CrearGrupoInformacion()
    {
        var grupo = new GroupBox
        {
            Text = "Información del shapefile",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 10),
            ForeColor = ColorTexto,
            Margin = new Padding(0, 0, 6, 6)
        };

        var tabla = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8 };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142F));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        AgregarFilaInformacion(tabla, 0, "Archivo:", _lblValorArchivo);
        AgregarFilaInformacion(tabla, 1, "Capa:", _lblValorCapa);
        AgregarFilaInformacion(tabla, 2, "Registros:", _lblValorRegistros);
        AgregarFilaInformacion(tabla, 3, "Tipo de geometría:", _lblValorGeometria);
        AgregarFilaInformacion(tabla, 4, "SRID / Referencia:", _lblValorReferencia);
        AgregarFilaInformacion(tabla, 5, "Extensión (bbox):", _lblValorExtension);
        AgregarFilaInformacion(tabla, 6, "Codificación DBF:", _lblValorCodificacion);
        AgregarFilaInformacion(tabla, 7, "Estado:", _lblValorEstado);

        grupo.Controls.Add(tabla);
        return grupo;
    }

    private static void AgregarFilaInformacion(TableLayoutPanel tabla, int fila, string titulo, Label valor)
    {
        // Altura fija por fila: evita que el contenedor recorte la información
        // (el grupo se muestra en una fila de altura absoluta).
        tabla.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));

        var etiqueta = new Label
        {
            Text = titulo,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Color.FromArgb(108, 118, 128),
            Margin = new Padding(0, 3, 6, 0)
        };

        valor.Dock = DockStyle.Fill;
        valor.AutoSize = false;
        valor.AutoEllipsis = true;
        valor.Height = 18;
        valor.ForeColor = ColorTexto;
        valor.TextAlign = ContentAlignment.MiddleLeft;
        valor.Margin = new Padding(0, 1, 0, 0);
        valor.Text = TextoSinValor;

        tabla.Controls.Add(etiqueta, 0, fila);
        tabla.Controls.Add(valor, 1, fila);
    }

    private Control CrearGrupoComponentes()
    {
        var grupo = new GroupBox
        {
            Text = "Archivos asociados",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 10),
            ForeColor = ColorTexto,
            Margin = new Padding(0, 0, 6, 6)
        };

        var tabla = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56F));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        (ShapefileComponentType Tipo, string Extension)[] componentes =
        [
            (ShapefileComponentType.Shp, ".shp"),
            (ShapefileComponentType.Shx, ".shx"),
            (ShapefileComponentType.Dbf, ".dbf"),
            (ShapefileComponentType.Prj, ".prj")
        ];

        for (var fila = 0; fila < componentes.Length; fila++)
        {
            var (tipo, extension) = componentes[fila];

            var etiqueta = new Label
            {
                Text = extension,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = Color.FromArgb(108, 118, 128),
                Margin = new Padding(0, 4, 6, 0)
            };

            var estado = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                Height = 18,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 1, 0, 0),
                Text = TextoSinValor
            };

            _lblComponentes[tipo] = estado;
            _toolTip.SetToolTip(etiqueta, DescripcionComponente(tipo));

            // Altura fija por fila: garantiza que los cuatro archivos (.shp/.shx/.dbf/.prj)
            // se vean simultáneamente dentro del grupo.
            tabla.RowStyles.Add(new RowStyle(SizeType.Absolute, 21F));
            tabla.Controls.Add(etiqueta, 0, fila);
            tabla.Controls.Add(estado, 1, fila);
        }

        grupo.Controls.Add(tabla);
        return grupo;
    }

    private Control CrearGrupoCampos()
    {
        var grupo = new GroupBox
        {
            Text = "Campos del DBF",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 10),
            ForeColor = ColorTexto,
            Margin = new Padding(6, 0, 0, 0)
        };

        _lstCampos.Dock = DockStyle.Fill;
        _lstCampos.View = View.Details;
        _lstCampos.GridLines = true;
        _lstCampos.FullRowSelect = true;
        _lstCampos.MultiSelect = false;
        _lstCampos.HideSelection = false;
        _lstCampos.ShowItemToolTips = true;
        _lstCampos.BorderStyle = BorderStyle.FixedSingle;
        _lstCampos.Columns.Add("Campo", 140, HorizontalAlignment.Left);
        _lstCampos.Columns.Add("Tipo", 100, HorizontalAlignment.Left);
        _lstCampos.Columns.Add("Longitud", 70, HorizontalAlignment.Right);
        _lstCampos.Columns.Add("Decimales", 76, HorizontalAlignment.Right);

        grupo.Controls.Add(_lstCampos);
        return grupo;
    }

    private Control CrearGrupoMensajes()
    {
        var grupo = new GroupBox
        {
            Text = "Errores y advertencias de la inspección",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 10),
            ForeColor = ColorTexto,
            Margin = new Padding(0, 0, 6, 0)
        };

        _txtMensajes.Dock = DockStyle.Fill;
        _txtMensajes.Multiline = true;
        _txtMensajes.ReadOnly = true;
        _txtMensajes.ScrollBars = ScrollBars.Vertical;
        _txtMensajes.WordWrap = false;
        _txtMensajes.Font = new Font("Consolas", 9F);
        _txtMensajes.BackColor = Color.White;
        _txtMensajes.ForeColor = ColorTexto;
        _txtMensajes.BorderStyle = BorderStyle.FixedSingle;

        grupo.Controls.Add(_txtMensajes);
        return grupo;
    }

    private Control CrearContenidoPrevisualizacion()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _lblResumenPrevisualizacion.Dock = DockStyle.Fill;
        _lblResumenPrevisualizacion.AutoSize = false;
        _lblResumenPrevisualizacion.Height = 22;
        _lblResumenPrevisualizacion.TextAlign = ContentAlignment.MiddleLeft;
        _lblResumenPrevisualizacion.ForeColor = Color.FromArgb(108, 118, 128);
        _lblResumenPrevisualizacion.Text = "Sin datos: seleccione un archivo .shp.";
        _lblResumenPrevisualizacion.Margin = new Padding(2, 0, 0, 6);

        var grupo = new GroupBox
        {
            Text = "Primeros 20 registros",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 10),
            ForeColor = ColorTexto
        };

        ConfigurarGrid(_dgvPrevisualizacion);
        _dgvPrevisualizacion.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _dgvPrevisualizacion.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;

        grupo.Controls.Add(_dgvPrevisualizacion);
        layout.Controls.Add(_lblResumenPrevisualizacion, 0, 0);
        layout.Controls.Add(grupo, 0, 1);
        return layout;
    }

    private Control CrearContenidoValidacion()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _lblEstadoValidacion.Dock = DockStyle.Fill;
        _lblEstadoValidacion.AutoSize = false;
        _lblEstadoValidacion.Height = 34;
        _lblEstadoValidacion.Padding = new Padding(10, 0, 10, 0);
        _lblEstadoValidacion.TextAlign = ContentAlignment.MiddleLeft;
        _lblEstadoValidacion.BackColor = ColorMarcaClara;
        _lblEstadoValidacion.ForeColor = ColorTexto;
        _lblEstadoValidacion.Text = "Sin validación ejecutada. Seleccione un archivo .shp y pulse «Validar datos».";
        _lblEstadoValidacion.Margin = new Padding(0, 0, 0, 8);

        var grupo = new GroupBox
        {
            Text = "Estadísticas de calidad",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 10),
            ForeColor = ColorTexto
        };

        ConfigurarGrid(_dgvEstadisticas);
        _dgvEstadisticas.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _dgvEstadisticas.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Estadística", Name = "columnaEstadistica" });
        _dgvEstadisticas.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Valor",
            Name = "columnaValorEstadistica",
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _dgvEstadisticas.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Observación",
            Name = "columnaObservacion",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        grupo.Controls.Add(_dgvEstadisticas);
        layout.Controls.Add(_lblEstadoValidacion, 0, 0);
        layout.Controls.Add(grupo, 0, 1);
        return layout;
    }

    private Control CrearContenidoIncidencias()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var barra = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 6)
        };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var etiquetaFiltro = new Label
        {
            Text = "Mostrar:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Color.FromArgb(108, 118, 128),
            Margin = new Padding(0, 8, 8, 0)
        };

        _cboFiltroIncidencias.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboFiltroIncidencias.Width = 230;
        _cboFiltroIncidencias.Margin = new Padding(0, 4, 14, 0);
        _cboFiltroIncidencias.SelectedIndexChanged += cboFiltroIncidencias_SelectedIndexChanged;

        _lblResumenIncidencias.AutoSize = true;
        _lblResumenIncidencias.Anchor = AnchorStyles.Left;
        _lblResumenIncidencias.ForeColor = ColorTexto;
        _lblResumenIncidencias.Margin = new Padding(0, 8, 0, 0);
        _lblResumenIncidencias.Text = "Sin incidencias registradas.";

        barra.Controls.Add(etiquetaFiltro, 0, 0);
        barra.Controls.Add(_cboFiltroIncidencias, 1, 0);
        barra.Controls.Add(_lblResumenIncidencias, 2, 0);

        var grupo = new GroupBox
        {
            Text = "Incidencias detectadas",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 10),
            ForeColor = ColorTexto
        };

        ConfigurarGrid(_dgvIncidencias);
        _dgvIncidencias.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _dgvIncidencias.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Registro", Name = "columnaRegistro" });
        _dgvIncidencias.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Severidad", Name = "columnaSeveridad" });
        _dgvIncidencias.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tipo", Name = "columnaTipo" });
        _dgvIncidencias.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Campo", Name = "columnaCampo" });
        _dgvIncidencias.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Valor", Name = "columnaValor" });
        _dgvIncidencias.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Acción", Name = "columnaAccion" });
        _dgvIncidencias.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Mensaje", Name = "columnaMensaje" });
        _dgvIncidencias.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Acción recomendada",
            Name = "columnaRecomendacion",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        grupo.Controls.Add(_dgvIncidencias);
        layout.Controls.Add(barra, 0, 0);
        layout.Controls.Add(grupo, 0, 1);
        return layout;
    }

    private StatusStrip CrearBarraEstado()
    {
        var barra = new StatusStrip
        {
            SizingGrip = false,
            BackColor = Color.FromArgb(240, 243, 247),
            ForeColor = ColorTexto
        };

        _lblActividad.Spring = true;
        _lblActividad.TextAlign = ContentAlignment.MiddleLeft;
        _lblActividad.Text = "Listo";

        _barraProgreso.Visible = false;
        _barraProgreso.Width = 190;
        _barraProgreso.Style = ProgressBarStyle.Continuous;
        _barraProgreso.Maximum = 100;

        barra.Items.Add(_lblActividad);
        barra.Items.Add(_barraProgreso);
        return barra;
    }

    private static void ConfigurarGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = Color.FromArgb(226, 230, 234);
        grid.Font = new Font("Segoe UI", 9F);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(238, 242, 246);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = ColorTexto;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 242, 246);
        grid.DefaultCellStyle.ForeColor = ColorTexto;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 228, 242);
        grid.DefaultCellStyle.SelectionForeColor = ColorTexto;
        grid.AlternatingRowsDefaultCellStyle.BackColor = ColorFilaAlterna;
        grid.RowTemplate.Height = 22;
    }

    private static string DescripcionComponente(ShapefileComponentType tipo) => tipo switch
    {
        ShapefileComponentType.Shp => ".shp: geometrías del shapefile",
        ShapefileComponentType.Shx => ".shx: índice espacial",
        ShapefileComponentType.Dbf => ".dbf: atributos (tabla dBASE)",
        _ => ".prj: referencia espacial en texto WKT (opcional)"
    };

    private Control CrearContenidoBitacora()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var layoutTarjetas = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 6)
        };
        for (var c = 0; c < 4; c++)
        {
            layoutTarjetas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }
        layoutTarjetas.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        SummaryCard[] tarjetas = [_cardBitacoraTotal, _cardBitacoraExitos, _cardBitacoraErrores, _cardBitacoraUltima];
        for (var i = 0; i < tarjetas.Length; i++)
        {
            tarjetas[i].Dock = DockStyle.Fill;
            layoutTarjetas.Controls.Add(tarjetas[i], i, 0);
        }

        var barraFiltros = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 6)
        };

        _btnRefrescarBitacora.Text = "🔄 Actualizar historial";
        _btnRefrescarBitacora.AutoSize = true;
        _btnRefrescarBitacora.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _btnRefrescarBitacora.Padding = new Padding(12, 5, 12, 5);
        _btnRefrescarBitacora.Margin = new Padding(0, 0, 10, 0);
        _btnRefrescarBitacora.Click += btnRefrescarBitacora_Click;

        var lblTabla = new Label
        {
            Text = "Tabla:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = ColorTexto,
            Margin = new Padding(4, 7, 4, 0)
        };

        _cboFiltroBitacoraTabla.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboFiltroBitacoraTabla.Items.AddRange(["Todas", "dbo.Manzanas", "dbo.Lotes", "dbo.CodigosFijos", "dbo.Vias"]);
        _cboFiltroBitacoraTabla.SelectedIndex = 0;
        _cboFiltroBitacoraTabla.Width = 140;
        _cboFiltroBitacoraTabla.Margin = new Padding(0, 4, 10, 0);
        _cboFiltroBitacoraTabla.SelectedIndexChanged += cboFiltroBitacora_SelectedIndexChanged;

        var lblResultado = new Label
        {
            Text = "Resultado:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = ColorTexto,
            Margin = new Padding(4, 7, 4, 0)
        };

        _cboFiltroBitacoraResultado.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboFiltroBitacoraResultado.Items.AddRange(["Todos", "EXITO", "ERROR", "CANCELADO"]);
        _cboFiltroBitacoraResultado.SelectedIndex = 0;
        _cboFiltroBitacoraResultado.Width = 120;
        _cboFiltroBitacoraResultado.Margin = new Padding(0, 4, 14, 0);
        _cboFiltroBitacoraResultado.SelectedIndexChanged += cboFiltroBitacora_SelectedIndexChanged;

        _lblResumenBitacora.AutoSize = true;
        _lblResumenBitacora.Anchor = AnchorStyles.Left;
        _lblResumenBitacora.ForeColor = ColorTexto;
        _lblResumenBitacora.Margin = new Padding(0, 7, 0, 0);
        _lblResumenBitacora.Text = "Cargando historial...";

        barraFiltros.Controls.Add(_btnRefrescarBitacora);
        barraFiltros.Controls.Add(lblTabla);
        barraFiltros.Controls.Add(_cboFiltroBitacoraTabla);
        barraFiltros.Controls.Add(lblResultado);
        barraFiltros.Controls.Add(_cboFiltroBitacoraResultado);
        barraFiltros.Controls.Add(_lblResumenBitacora);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            Margin = new Padding(0)
        };

        split.HandleCreated += (s, e) =>
        {
            split.BeginInvoke(() =>
            {
                if (split.Width > 200)
                {
                    try
                    {
                        split.SplitterDistance = Math.Max(150, (int)(split.Width * 0.52));
                    }
                    catch
                    {
                    }
                }
            });
        };

        var grupoGrilla = new GroupBox
        {
            Text = "Historial de operaciones (dbo.Bitacora)",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 10),
            ForeColor = ColorTexto
        };

        ConfigurarGrid(_dgvBitacora);
        _dgvBitacora.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _dgvBitacora.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#", Name = "colBitacoraId" });
        _dgvBitacora.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Fecha y Hora", Name = "colBitacoraFecha" });
        _dgvBitacora.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Módulo", Name = "colBitacoraModulo" });
        _dgvBitacora.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Acción", Name = "colBitacoraAccion" });
        _dgvBitacora.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tabla", Name = "colBitacoraTabla" });
        _dgvBitacora.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Resultado", Name = "colBitacoraResultado" });
        _dgvBitacora.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "IP / Host", Name = "colBitacoraIP" });
        _dgvBitacora.SelectionChanged += dgvBitacora_SelectionChanged;
        grupoGrilla.Controls.Add(_dgvBitacora);

        var grupoDetalle = new GroupBox
        {
            Text = "Detalle técnico de la operación seleccionada",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 10),
            ForeColor = ColorTexto
        };

        _txtDetalleBitacora.Dock = DockStyle.Fill;
        _txtDetalleBitacora.Multiline = true;
        _txtDetalleBitacora.ReadOnly = true;
        _txtDetalleBitacora.ScrollBars = ScrollBars.Vertical;
        _txtDetalleBitacora.Font = new Font("Consolas", 9.5F);
        _txtDetalleBitacora.BackColor = Color.White;
        _txtDetalleBitacora.ForeColor = ColorTexto;
        _txtDetalleBitacora.BorderStyle = BorderStyle.FixedSingle;
        grupoDetalle.Controls.Add(_txtDetalleBitacora);

        split.Panel1.Controls.Add(grupoGrilla);
        split.Panel2.Controls.Add(grupoDetalle);

        layout.Controls.Add(layoutTarjetas, 0, 0);
        layout.Controls.Add(barraFiltros, 0, 1);
        layout.Controls.Add(split, 0, 2);
        return layout;
    }
}
