namespace VisorDatosSIG.Migrador.Controls;

/// <summary>
/// Tarjeta de resumen: muestra un título corto, un valor destacado y una barra de acento.
/// </summary>
/// <remarks>
/// Se construye únicamente con controles estándar de Windows Forms (sin librerías externas).
/// El contenido se organiza con un TableLayoutPanel de dos filas (título y valor) y ambas
/// etiquetas tienen <c>AutoSize</c> desactivado, de modo que el valor siempre queda visible.
/// </remarks>
internal sealed class SummaryCard : UserControl
{
    private const int AltoTitulo = 16;

    private readonly Panel _barraAcento = new();
    private readonly Label _lblTitulo = new();
    private readonly Label _lblValor = new();

    /// <summary>
    /// Crea la tarjeta con su título.
    /// </summary>
    /// <param name="caption">Título corto de la tarjeta.</param>
    public SummaryCard(string caption)
    {
        BackColor = Color.White;
        BorderStyle = BorderStyle.FixedSingle;
        MinimumSize = new Size(140, 48);
        Margin = new Padding(4);

        _barraAcento.Dock = DockStyle.Fill;
        _barraAcento.BackColor = Color.FromArgb(37, 99, 160);

        _lblTitulo.AutoSize = false;
        _lblTitulo.Dock = DockStyle.Fill;
        _lblTitulo.Font = new Font("Segoe UI", 8.25F);
        _lblTitulo.ForeColor = Color.FromArgb(108, 118, 128);
        _lblTitulo.TextAlign = ContentAlignment.MiddleLeft;
        _lblTitulo.AutoEllipsis = true;
        _lblTitulo.Text = caption;

        _lblValor.AutoSize = false;
        _lblValor.Dock = DockStyle.Fill;
        _lblValor.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _lblValor.ForeColor = Color.FromArgb(37, 99, 160);
        _lblValor.TextAlign = ContentAlignment.MiddleLeft;
        _lblValor.AutoEllipsis = true;
        _lblValor.Text = "-";

        var contenido = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10, 5, 8, 5),
            Margin = new Padding(0),
            BackColor = Color.White
        };
        contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        contenido.RowStyles.Add(new RowStyle(SizeType.Absolute, AltoTitulo));
        contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        contenido.Controls.Add(_lblTitulo, 0, 0);
        contenido.Controls.Add(_lblValor, 0, 1);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 4F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(_barraAcento, 0, 0);
        layout.Controls.Add(contenido, 1, 0);

        Controls.Add(layout);
    }

    /// <summary>
    /// Actualiza el contenido y el color de acento de la tarjeta.
    /// </summary>
    public void Set(string caption, string value, Color accent)
    {
        _lblTitulo.Text = caption;
        _lblValor.Text = value;
        _lblValor.ForeColor = accent;
        _barraAcento.BackColor = accent;
    }
}
