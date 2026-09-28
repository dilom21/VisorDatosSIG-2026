using System.Globalization;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Migrador.Forms;

/// <summary>
/// Ventana principal del Migrador.
/// Fase 1: selección, validación de archivos asociados e inspección del shapefile.
/// </summary>
/// <remarks>
/// El formulario no lee archivos: delega toda la inspección en <see cref="IShapefileReader"/>,
/// cuyo contrato está en Application y cuya implementación está en Infrastructure.
/// </remarks>
public sealed class MigradorForm : Form
{
    private const string TextoSinValor = "-";

    private static readonly Color ColorValorPresente = Color.FromArgb(0, 100, 0);
    private static readonly Color ColorValorFaltante = Color.Firebrick;
    private static readonly Color ColorValorOpcional = Color.FromArgb(180, 95, 6);

    private readonly IShapefileReader _shapefileReader;
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _txtRuta = new();
    private readonly Button _btnSeleccionar = new();
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
    private readonly DataGridView _dgvPrevisualizacion = new();
    private readonly TextBox _txtMensajes = new();
    private readonly ToolStripStatusLabel _lblActividad = new();

    private string? _ultimaCarpeta;

    /// <summary>
    /// Inicializa el formulario con el servicio de inspección de shapefiles.
    /// </summary>
    /// <param name="shapefileReader">Servicio de inspección de shapefiles.</param>
    public MigradorForm(IShapefileReader shapefileReader)
    {
        _shapefileReader = shapefileReader ?? throw new ArgumentNullException(nameof(shapefileReader));

        InicializarInterfaz();
        ReiniciarResultados();
    }

    /// <summary>
    /// Libera los recursos propios del formulario.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private async void btnSeleccionar_Click(object? sender, EventArgs e)
    {
        try
        {
            using var dialogo = CrearDialogoSeleccion();
            if (dialogo.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _ultimaCarpeta = Path.GetDirectoryName(dialogo.FileName);
            _txtRuta.Text = dialogo.FileName;

            await InspeccionarAsync(dialogo.FileName);
        }
        catch (Exception ex)
        {
            MostrarErrorInesperado("Ocurrió un error inesperado al seleccionar el archivo.", ex);
        }
    }

    private OpenFileDialog CrearDialogoSeleccion()
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Seleccione el archivo Shapefile (.shp)",
            Filter = "Shapefile (*.shp)|*.shp|Todos los archivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            RestoreDirectory = true
        };

        if (!string.IsNullOrWhiteSpace(_ultimaCarpeta))
        {
            dialogo.InitialDirectory = _ultimaCarpeta;
        }

        return dialogo;
    }

    private async Task InspeccionarAsync(string rutaShp)
    {
        EstablecerActividad("Leyendo el shapefile y validando los archivos asociados...");
        AlternarControles(false);
        ReiniciarResultados();
        _txtRuta.Text = rutaShp;

        try
        {
            var info = await _shapefileReader.InspectAsync(rutaShp);
            MostrarResultado(info);
        }
        catch (Exception ex)
        {
            MostrarErrorInesperado("No se pudo inspeccionar el archivo seleccionado.", ex);
        }
        finally
        {
            AlternarControles(true);
        }
    }

    private void InicializarInterfaz()
    {
        Text = "VisorDatosSIG 2026 - Migrador (Fase 1: inspección de shapefiles)";
        MinimumSize = new Size(1000, 720);
        ClientSize = new Size(1140, 800);
        StartPosition = FormStartPosition.CenterScreen;

        var layoutRaiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };
        layoutRaiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutRaiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layoutRaiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 262F));
        layoutRaiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutRaiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F));

        layoutRaiz.Controls.Add(CrearPanelSeleccion(), 0, 0);
        layoutRaiz.Controls.Add(CrearPanelDetalles(), 0, 1);
        layoutRaiz.Controls.Add(CrearPanelPrevisualizacion(), 0, 2);
        layoutRaiz.Controls.Add(CrearPanelMensajes(), 0, 3);

        var barraEstado = new StatusStrip();
        _lblActividad.Text = "Listo";
        barraEstado.Items.Add(_lblActividad);

        Controls.Add(layoutRaiz);
        Controls.Add(barraEstado);
    }

    private Control CrearPanelSeleccion()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var etiqueta = new Label
        {
            Text = "Archivo SHP:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 8, 8, 0)
        };

        _txtRuta.Dock = DockStyle.Fill;
        _txtRuta.ReadOnly = true;
        _txtRuta.Margin = new Padding(0, 4, 8, 0);
        _txtRuta.BackColor = SystemColors.Window;

        _btnSeleccionar.Text = "Seleccionar SHP";
        _btnSeleccionar.AutoSize = true;
        _btnSeleccionar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _btnSeleccionar.Padding = new Padding(10, 3, 10, 3);
        _btnSeleccionar.Margin = new Padding(0, 2, 0, 0);
        _btnSeleccionar.Click += btnSeleccionar_Click;

        layout.Controls.Add(etiqueta, 0, 0);
        layout.Controls.Add(_txtRuta, 1, 0);
        layout.Controls.Add(_btnSeleccionar, 2, 0);

        return layout;
    }

    private Control CrearPanelDetalles()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(CrearGrupoInformacion(), 0, 0);
        layout.Controls.Add(CrearGrupoComponentes(), 1, 0);
        layout.Controls.Add(CrearGrupoCampos(), 2, 0);

        return layout;
    }

    private Control CrearGrupoInformacion()
    {
        var grupo = new GroupBox
        {
            Text = "Información del shapefile",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 8)
        };

        var tabla = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8
        };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138F));
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
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var etiqueta = new Label
        {
            Text = titulo,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 6, 6, 0)
        };

        valor.Dock = DockStyle.Fill;
        valor.AutoSize = false;
        valor.AutoEllipsis = true;
        valor.Height = 20;
        valor.TextAlign = ContentAlignment.MiddleLeft;
        valor.Margin = new Padding(0, 4, 0, 0);
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
            Padding = new Padding(10, 6, 10, 8)
        };

        var tabla = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4
        };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52F));
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
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 6, 6, 0)
            };

            var estado = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 0, 0),
                Text = TextoSinValor
            };

            _lblComponentes[tipo] = estado;
            _toolTip.SetToolTip(etiqueta, DescripcionComponente(tipo));

            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
            Padding = new Padding(10, 6, 10, 8)
        };

        _lstCampos.Dock = DockStyle.Fill;
        _lstCampos.View = View.Details;
        _lstCampos.GridLines = true;
        _lstCampos.FullRowSelect = true;
        _lstCampos.MultiSelect = false;
        _lstCampos.HideSelection = false;
        _lstCampos.ShowItemToolTips = true;
        _lstCampos.Columns.Add("Campo", 120, HorizontalAlignment.Left);
        _lstCampos.Columns.Add("Tipo", 90, HorizontalAlignment.Left);
        _lstCampos.Columns.Add("Longitud", 64, HorizontalAlignment.Right);
        _lstCampos.Columns.Add("Decimales", 70, HorizontalAlignment.Right);

        grupo.Controls.Add(_lstCampos);
        return grupo;
    }

    private Control CrearPanelPrevisualizacion()
    {
        var grupo = new GroupBox
        {
            Text = "Previsualización (máximo 20 registros)",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 8)
        };

        _dgvPrevisualizacion.Dock = DockStyle.Fill;
        _dgvPrevisualizacion.ReadOnly = true;
        _dgvPrevisualizacion.AllowUserToAddRows = false;
        _dgvPrevisualizacion.AllowUserToDeleteRows = false;
        _dgvPrevisualizacion.AllowUserToResizeRows = false;
        _dgvPrevisualizacion.RowHeadersVisible = false;
        _dgvPrevisualizacion.MultiSelect = false;
        _dgvPrevisualizacion.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _dgvPrevisualizacion.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _dgvPrevisualizacion.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _dgvPrevisualizacion.BackgroundColor = SystemColors.Window;
        _dgvPrevisualizacion.BorderStyle = BorderStyle.FixedSingle;
        _dgvPrevisualizacion.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(244, 244, 244);

        grupo.Controls.Add(_dgvPrevisualizacion);
        return grupo;
    }

    private Control CrearPanelMensajes()
    {
        var grupo = new GroupBox
        {
            Text = "Errores y advertencias",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 8)
        };

        _txtMensajes.Dock = DockStyle.Fill;
        _txtMensajes.Multiline = true;
        _txtMensajes.ReadOnly = true;
        _txtMensajes.ScrollBars = ScrollBars.Vertical;
        _txtMensajes.WordWrap = false;
        _txtMensajes.Font = new Font("Consolas", 9F);
        _txtMensajes.BackColor = SystemColors.Window;

        grupo.Controls.Add(_txtMensajes);
        return grupo;
    }

    private static string DescripcionComponente(ShapefileComponentType tipo) => tipo switch
    {
        ShapefileComponentType.Shp => ".shp: geometrías del shapefile",
        ShapefileComponentType.Shx => ".shx: índice espacial",
        ShapefileComponentType.Dbf => ".dbf: atributos (tabla dBASE)",
        _ => ".prj: referencia espacial en texto WKT (opcional)"
    };

    private void MostrarResultado(ShapefileInfoDto info)
    {
        _lblValorArchivo.Text = info.FileName;
        _toolTip.SetToolTip(_lblValorArchivo, info.FilePath);

        _lblValorCapa.Text = info.LayerDisplayName;
        _lblValorCapa.ForeColor = info.Layer == ShapefileLayer.Unrecognized
            ? ColorValorFaltante
            : SystemColors.ControlText;

        _lblValorRegistros.Text = info.RecordCount.ToString("N0", CultureInfo.CurrentCulture);
        _toolTip.SetToolTip(
            _lblValorRegistros,
            $"Registros declarados por el archivo DBF: {info.RecordCount:N0}. " +
            $"Registros previsualizados: {info.Preview.Records.Count:N0}.");

        _lblValorGeometria.Text = ComponerTextoGeometria(info);
        _toolTip.SetToolTip(_lblValorGeometria, ComponerTextoGeometria(info));

        _lblValorReferencia.Text = ComponerTextoReferencia(info.SpatialReference);
        _toolTip.SetToolTip(_lblValorReferencia, ComponerDetalleReferencia(info.SpatialReference));

        _lblValorExtension.Text = info.ExtentText ?? "No disponible";
        _toolTip.SetToolTip(
            _lblValorExtension,
            info.ExtentWithinGeographicRange switch
            {
                true => "Las coordenadas están dentro del rango geográfico válido (longitud -180..180, latitud -90..90).",
                false => "Las coordenadas están fuera del rango geográfico válido: el archivo podría no estar en un sistema geográfico.",
                _ => "No fue posible determinar la extensión del archivo."
            });

        _lblValorCodificacion.Text = info.DbfEncodingName ?? "No disponible";

        _lblValorEstado.Text = $"{info.Status.ToDisplayName()} - {info.StatusMessage}";
        _lblValorEstado.ForeColor = info.Status switch
        {
            ShapefileInspectionStatus.Failed => ColorValorFaltante,
            ShapefileInspectionStatus.Warning => ColorValorOpcional,
            _ => ColorValorPresente
        };
        _toolTip.SetToolTip(_lblValorEstado, _lblValorEstado.Text);

        MostrarComponentes(info.Components);
        MostrarCampos(info.Fields);
        MostrarPrevisualizacion(info.Preview);
        MostrarMensajes(info);

        EstablecerActividad(info.Status switch
        {
            ShapefileInspectionStatus.Failed => "No se pudo inspeccionar el archivo.",
            ShapefileInspectionStatus.Warning =>
                $"Inspección finalizada con advertencias: {info.RecordCount:N0} registro(s), " +
                $"{info.Preview.Records.Count:N0} previsualizado(s).",
            _ =>
                $"Inspección finalizada correctamente: {info.RecordCount:N0} registro(s), " +
                $"{info.Preview.Records.Count:N0} previsualizado(s)."
        });
    }

    private void MostrarComponentes(IReadOnlyList<ShapefileComponentDto> componentes)
    {
        foreach (var componente in componentes)
        {
            if (!_lblComponentes.TryGetValue(componente.Type, out var etiqueta))
            {
                continue;
            }

            etiqueta.Text = componente.Exists
                ? "Presente"
                : componente.IsRequired ? "FALTANTE" : "FALTANTE (opcional)";

            etiqueta.ForeColor = componente.Exists
                ? ColorValorPresente
                : componente.IsRequired ? ColorValorFaltante : ColorValorOpcional;

            _toolTip.SetToolTip(etiqueta, componente.FilePath);
        }
    }

    private void MostrarCampos(IReadOnlyList<ShapefileFieldDto> campos)
    {
        _lstCampos.BeginUpdate();
        _lstCampos.Items.Clear();

        foreach (var campo in campos)
        {
            var item = new ListViewItem(campo.Name);
            item.SubItems.Add(campo.DataType);
            item.SubItems.Add(campo.Length.ToString(CultureInfo.CurrentCulture));
            item.SubItems.Add(campo.DecimalCount.ToString(CultureInfo.CurrentCulture));
            item.ToolTipText = $"{campo.Name} ({campo.DisplayType})";
            _lstCampos.Items.Add(item);
        }

        _lstCampos.EndUpdate();
    }

    private void MostrarPrevisualizacion(ShapefilePreviewDto previsualizacion)
    {
        _dgvPrevisualizacion.Rows.Clear();
        _dgvPrevisualizacion.Columns.Clear();

        if (previsualizacion.Records.Count == 0)
        {
            return;
        }

        _dgvPrevisualizacion.Columns.Add("columnaRegistro", "N°");
        _dgvPrevisualizacion.Columns.Add("columnaGeometria", "Geometría");

        for (var indice = 0; indice < previsualizacion.ColumnNames.Count; indice++)
        {
            _dgvPrevisualizacion.Columns.Add($"columna{indice}", previsualizacion.ColumnNames[indice]);
        }

        foreach (var registro in previsualizacion.Records)
        {
            var valores = new List<object>(previsualizacion.ColumnNames.Count + 2)
            {
                registro.RecordNumber,
                registro.GeometryType
            };

            valores.AddRange(previsualizacion.ColumnNames.Select(
                nombre => (object)(registro.GetValue(nombre) ?? string.Empty)));

            _dgvPrevisualizacion.Rows.Add(valores.ToArray());
        }
    }

    private void MostrarMensajes(ShapefileInfoDto info)
    {
        var lineas = new List<string>(info.Errors.Count + info.Warnings.Count + 1);
        lineas.AddRange(info.Errors.Select(mensaje => "ERROR: " + mensaje));
        lineas.AddRange(info.Warnings.Select(mensaje => "ADVERTENCIA: " + mensaje));

        if (lineas.Count == 0)
        {
            lineas.Add("Sin errores ni advertencias.");
        }

        _txtMensajes.Lines = lineas.ToArray();
        _txtMensajes.SelectionStart = 0;
        _txtMensajes.SelectionLength = 0;
    }

    private void ReiniciarResultados()
    {
        _lblValorArchivo.Text = TextoSinValor;
        _lblValorCapa.Text = TextoSinValor;
        _lblValorCapa.ForeColor = SystemColors.ControlText;
        _lblValorRegistros.Text = TextoSinValor;
        _lblValorGeometria.Text = TextoSinValor;
        _lblValorReferencia.Text = TextoSinValor;
        _lblValorExtension.Text = TextoSinValor;
        _lblValorCodificacion.Text = TextoSinValor;
        _lblValorEstado.Text = TextoSinValor;
        _lblValorEstado.ForeColor = SystemColors.ControlText;

        foreach (var etiqueta in _lblComponentes.Values)
        {
            etiqueta.Text = TextoSinValor;
            etiqueta.ForeColor = SystemColors.ControlText;
        }

        _lstCampos.Items.Clear();
        _dgvPrevisualizacion.Rows.Clear();
        _dgvPrevisualizacion.Columns.Clear();
        _txtMensajes.Text = "Seleccione un archivo .shp para inspeccionarlo.";
    }

    private void AlternarControles(bool habilitado)
    {
        _btnSeleccionar.Enabled = habilitado;
        UseWaitCursor = !habilitado;
    }

    private void EstablecerActividad(string texto) => _lblActividad.Text = texto;

    private void MostrarErrorInesperado(string mensaje, Exception excepcion)
    {
        _txtMensajes.Lines =
        [
            "ERROR: " + mensaje,
            $"Detalle técnico: {excepcion.GetType().Name}: {excepcion.Message}"
        ];

        EstablecerActividad("Error durante la inspección.");

        MessageBox.Show(
            this,
            $"{mensaje}{Environment.NewLine}{Environment.NewLine}" +
            $"Detalle técnico: {excepcion.GetType().Name}: {excepcion.Message}",
            "Migrador - Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static string ComponerTextoGeometria(ShapefileInfoDto info)
    {
        var declarado = info.DeclaredShapeType ?? "No disponible";

        return info.ObservedGeometryTypes.Count == 0
            ? declarado
            : $"{declarado} | leídos: {string.Join(", ", info.ObservedGeometryTypes)}";
    }

    private static string ComponerTextoReferencia(SpatialReferenceDto referencia)
    {
        if (!referencia.IsAvailable)
        {
            return "No verificable (falta el archivo .prj)";
        }

        var tipo = referencia.IsGeographic ? "geográfico" : "proyectado";
        var srid = referencia.Srid.HasValue ? $"EPSG:{referencia.Srid.Value}" : "EPSG no determinado";
        var nombre = string.IsNullOrWhiteSpace(referencia.Name) ? "sin nombre declarado" : referencia.Name;

        return referencia.IsWgs84
            ? $"{srid} - {nombre} ({tipo})"
            : $"{srid} - {nombre} ({tipo}) [no corresponde a WGS 84]";
    }

    private static string ComponerDetalleReferencia(SpatialReferenceDto referencia)
    {
        var partes = new List<string>(3);

        if (!referencia.IsAvailable)
        {
            partes.Add(referencia.Note ?? "No se encontró el archivo .prj.");
            return string.Join(Environment.NewLine + Environment.NewLine, partes);
        }

        if (referencia.DetectionEvidence is { Length: > 0 } evidencia)
        {
            partes.Add(evidencia);
        }

        if (referencia.Note is { Length: > 0 } observacion)
        {
            partes.Add(observacion);
        }

        if (referencia.Wkt is { Length: > 0 } wkt)
        {
            var wktRecortado = wkt.Trim();
            partes.Add("WKT: " + (wktRecortado.Length > 300 ? wktRecortado[..300] + "..." : wktRecortado));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, partes);
    }
}
