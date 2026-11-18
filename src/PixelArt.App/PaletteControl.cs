using System.ComponentModel;
using PixelArt.Core;

namespace PixelArt.App;

public sealed class PaletteControl : Control
{
    private const int Cell = 27;
    private const int Columns = 8;
    private readonly EditorSession session;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PrimaryColor { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SecondaryColor { get; set; }
    public event Action<int, bool>? ColorSelected;
    public event Action<int>? ColorEditRequested;

    public PaletteControl(EditorSession session)
    {
        this.session = session;
        DoubleBuffered = true;
        Width = Columns * Cell;
        UpdatePalette();
    }

    public void UpdatePalette()
    {
        Height = (int)Math.Ceiling(session.Document.Palette.Count / (double)Columns) * Cell;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var colors = session.Document.Palette;
        for (int i = 0; i < colors.Count; i++)
        {
            var rect = new Rectangle(i % Columns * Cell + 2, i / Columns * Cell + 2, Cell - 5, Cell - 5);
            e.Graphics.FillRectangle(Brushes.White, rect);
            e.Graphics.FillRectangle(Brushes.LightGray, rect.X, rect.Y, rect.Width / 2, rect.Height / 2);
            e.Graphics.FillRectangle(Brushes.LightGray, rect.X + rect.Width / 2, rect.Y + rect.Height / 2,
                rect.Width / 2, rect.Height / 2);
            using var brush = new SolidBrush(Color.FromArgb(colors[i]));
            e.Graphics.FillRectangle(brush, rect);
            using var border = new Pen(colors[i] == PrimaryColor ? Color.FromArgb(15, 115, 103) : Color.FromArgb(176, 181, 171),
                colors[i] == PrimaryColor ? 3 : 1);
            e.Graphics.DrawRectangle(border, rect);
            if (colors[i] == SecondaryColor) e.Graphics.FillRectangle(Brushes.White, rect.Right - 5, rect.Bottom - 5, 4, 4);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int index = e.Y / Cell * Columns + e.X / Cell;
        if (index < 0 || index >= session.Document.Palette.Count) return;
        if (e.Button is MouseButtons.Left or MouseButtons.Right)
            ColorSelected?.Invoke(index, e.Button == MouseButtons.Right);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        int index = e.Y / Cell * Columns + e.X / Cell;
        if (e.Button == MouseButtons.Left && index >= 0 && index < session.Document.Palette.Count)
            ColorEditRequested?.Invoke(index);
    }
}
