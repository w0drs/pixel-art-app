using System.ComponentModel;
using System.Drawing.Drawing2D;
using PixelArt.Core;

namespace PixelArt.App;

public sealed class CanvasControl : Control
{
    private readonly EditorSession session;
    private Bitmap? bitmap;
    private bool imageChanged = true;
    private PixelDocument? preview;
    private Point startPixel;
    private Point lastPixel;
    private Point lastMouse;
    private bool drawing;
    private bool panning;
    private bool spacePan;
    private bool panWithSpace;
    private MouseButtons gestureButton;
    private int strokeColor;
    private EditorTool strokeTool;
    private int strokeSize;
    private SmudgeBrush? smudge;
    private float zoom = 16;
    private PointF origin = new(40, 40);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EditorTool Tool { get; set; } = EditorTool.Pencil;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PrimaryColor { get; set; } = unchecked((int)0xFF182833);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SecondaryColor { get; set; } = unchecked((int)0xFFF7F3E8);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool FilledShapes { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PencilSize { get; set; } = 1;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int EraserSize { get; set; } = 1;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SmudgeSize { get; set; } = 8;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SmudgeStrength { get; set; } = 80;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int GridStep { get; set; } = 4;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowGrid { get; set; } = true;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool DarkBackground { get; set; }
    public int ZoomPercent => (int)Math.Round(zoom * 100);
    public event Action<int>? ColorPicked;
    public event Action<string>? StatusChanged;
    public event EventHandler? ViewChanged;

    public CanvasControl(EditorSession session)
    {
        this.session = session;
        DoubleBuffered = true;
        ResizeRedraw = true;
        TabStop = true;
        BackColor = Color.FromArgb(230, 233, 229);
        SetStyle(ControlStyles.Selectable, true);
        session.Changed += SessionChanged;
    }

    private void SessionChanged(object? sender, EventArgs e) => RefreshImage();

    public void SetSpacePan(bool pressed)
    {
        // Пробел перед перетаскиванием временно включает руку, не меняя Tool.
        // Во время незавершенного штриха инструмент не переключаем.
        if (pressed && drawing) return;
        spacePan = pressed;
        if (!pressed && panWithSpace)
        {
            panning = false;
            panWithSpace = false;
            Capture = false;
        }
        UpdateCursor();
    }

    private void UpdateCursor() => Cursor = spacePan || panning || Tool == EditorTool.Pan
        ? Cursors.Hand : Cursors.Default;

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) == Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Space || e.Modifiers != Keys.None) return;
        SetSpacePan(true);
        e.Handled = e.SuppressKeyPress = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode != Keys.Space) return;
        SetSpacePan(false);
        e.Handled = e.SuppressKeyPress = true;
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        SetSpacePan(false);
    }

    public void RefreshImage()
    {
        imageChanged = true;
        Invalidate();
    }

    public void FitToWindow()
    {
        CancelGesture();
        zoom = Math.Clamp(Math.Min((ClientSize.Width - 72f) / session.Document.Width,
            (ClientSize.Height - 72f) / session.Document.Height), 0.5f, 64f);
        origin = new PointF((ClientSize.Width - session.Document.Width * zoom) / 2,
            (ClientSize.Height - session.Document.Height * zoom) / 2);
        Invalidate();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ChangeZoom(float factor) => ZoomAt(factor, new Point(Width / 2, Height / 2));

    private void ZoomAt(float factor, Point anchor)
    {
        if (drawing || panning) return;
        float next = Math.Clamp(zoom * factor, 0.5f, 64f);
        origin = new PointF(anchor.X - (anchor.X - origin.X) * next / zoom,
            anchor.Y - (anchor.Y - origin.Y) * next / zoom);
        zoom = next;
        Invalidate();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var doc = preview ?? session.Document;
        if (imageChanged || bitmap == null)
        {
            bitmap?.Dispose();
            bitmap = ImageFiles.ToBitmap(doc);
            imageChanged = false;
        }
        var graphics = e.Graphics;
        graphics.Clear(DarkBackground ? Color.FromArgb(48, 57, 59) : BackColor);
        var bounds = new RectangleF(origin.X, origin.Y, doc.Width * zoom, doc.Height * zoom);
        using var shadow = new SolidBrush(Color.FromArgb(35, 0, 0, 0));
        graphics.FillRectangle(shadow, bounds.X + 6, bounds.Y + 6, bounds.Width, bounds.Height);
        // Шахматная подложка показывает прозрачность и никогда не попадает в файл.
        using var tile = new Bitmap(16, 16);
        using (var tg = Graphics.FromImage(tile))
        {
            tg.Clear(Color.FromArgb(250, 249, 245));
            using var check = new SolidBrush(Color.FromArgb(221, 225, 220));
            tg.FillRectangle(check, 0, 0, 8, 8);
            tg.FillRectangle(check, 8, 8, 8, 8);
        }
        using var background = new TextureBrush(tile);
        graphics.FillRectangle(background, bounds);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(bitmap!, bounds, new RectangleF(0, 0, doc.Width, doc.Height), GraphicsUnit.Pixel);
        graphics.PixelOffsetMode = PixelOffsetMode.Default;
        if (ShowGrid && zoom >= 6)
        {
            using var grid = new Pen(Color.FromArgb(65, 38, 57, 58));
            for (int x = 0; x <= doc.Width; x++)
                graphics.DrawLine(grid, origin.X + x * zoom, origin.Y, origin.X + x * zoom, bounds.Bottom);
            for (int y = 0; y <= doc.Height; y++)
                graphics.DrawLine(grid, origin.X, origin.Y + y * zoom, bounds.Right, origin.Y + y * zoom);
        }
        using var border = new Pen(Color.FromArgb(107, 126, 121));
        graphics.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ZoomAt(e.Delta > 0 ? 1.25f : 0.8f, e.Location);
    }

    private Point PixelAt(Point mouse, bool clamp = false)
    {
        int x = (int)Math.Floor((mouse.X - origin.X) / zoom);
        int y = (int)Math.Floor((mouse.Y - origin.Y) / zoom);
        if (clamp)
        {
            x = Math.Clamp(x, 0, session.Document.Width - 1);
            y = Math.Clamp(y, 0, session.Document.Height - 1);
        }
        return new Point(x, y);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (drawing || panning) return;
        if (e.Button == MouseButtons.Middle || ((Tool == EditorTool.Pan || spacePan) && e.Button == MouseButtons.Left))
        {
            panning = true;
            panWithSpace = spacePan && e.Button == MouseButtons.Left;
            gestureButton = e.Button;
            lastMouse = e.Location;
            Capture = true;
            Cursor = Cursors.Hand;
            return;
        }
        if (e.Button is not (MouseButtons.Left or MouseButtons.Right)) return;
        var point = PixelAt(e.Location);
        if (!session.Document.Contains(point.X, point.Y)) return;
        if (Tool == EditorTool.Picker)
        {
            ColorPicked?.Invoke(session.Document.GetPixel(point.X, point.Y));
            return;
        }
        strokeColor = Tool == EditorTool.Eraser ? 0 :
            e.Button == MouseButtons.Right ? SecondaryColor : PrimaryColor;
        if (Tool == EditorTool.Fill)
        {
            session.Apply(doc => RasterDrawing.FloodFill(doc, point.X, point.Y, strokeColor));
            return;
        }
        if (Tool == EditorTool.Pan) return;
        startPixel = lastPixel = point;
        strokeTool = Tool;
        strokeSize = Math.Clamp(Tool == EditorTool.Eraser ? EraserSize : PencilSize, 1, RasterDrawing.MaxBrushSize);
        smudge = Tool == EditorTool.Smudge ? new SmudgeBrush(session.Document, point.X, point.Y,
            Math.Clamp(SmudgeSize, 1, RasterDrawing.MaxBrushSize), Math.Clamp(SmudgeStrength, 0, 100)) : null;
        session.BeginEdit();
        drawing = true;
        gestureButton = e.Button;
        Capture = true;
        UpdateDrawing(point);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = PixelAt(e.Location);
        StatusChanged?.Invoke(session.Document.Contains(point.X, point.Y) ? $"X {point.X}  /  Y {point.Y}" : "За пределами холста");
        if (panning)
        {
            origin.X += e.X - lastMouse.X;
            origin.Y += e.Y - lastMouse.Y;
            lastMouse = e.Location;
            Invalidate();
        }
        else if (drawing) UpdateDrawing(PixelAt(e.Location, true));
    }

    private void UpdateDrawing(Point point)
    {
        if (strokeTool is EditorTool.Pencil or EditorTool.Eraser)
        {
            RasterDrawing.BrushStroke(session.Document, lastPixel.X, lastPixel.Y, point.X, point.Y, strokeColor, strokeSize);
            lastPixel = point;
        }
        else if (strokeTool == EditorTool.Smudge)
        {
            smudge!.MoveTo(point.X, point.Y);
        }
        else
        {
            preview = session.Document.Clone();
            switch (strokeTool)
            {
                case EditorTool.Line:
                    RasterDrawing.Line(preview, startPixel.X, startPixel.Y, point.X, point.Y, strokeColor);
                    break;
                case EditorTool.Rectangle:
                    RasterDrawing.Rectangle(preview, startPixel.X, startPixel.Y, point.X, point.Y, strokeColor, FilledShapes);
                    break;
                case EditorTool.Ellipse:
                    RasterDrawing.Ellipse(preview, startPixel.X, startPixel.Y, point.X, point.Y, strokeColor, FilledShapes);
                    break;
                case EditorTool.Grid:
                    RasterDrawing.Grid(preview, startPixel.X, startPixel.Y, point.X, point.Y, strokeColor, GridStep);
                    break;
            }
        }
        RefreshImage();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if ((!drawing && !panning) || e.Button != gestureButton) return;
        if (drawing)
        {
            UpdateDrawing(PixelAt(e.Location, true));
            if (preview != null) session.Document.SetPixels(preview.CopyPixels());
            drawing = false;
            preview = null;
            smudge = null;
            session.CommitEdit();
        }
        panning = false;
        panWithSpace = false;
        Capture = false;
        UpdateCursor();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture) CancelGesture();
    }

    public void CancelGesture()
    {
        if (drawing)
        {
            drawing = false;
            preview = null;
            smudge = null;
            session.CancelEdit();
        }
        panning = false;
        panWithSpace = false;
        Capture = false;
        UpdateCursor();
        RefreshImage();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            session.Changed -= SessionChanged;
            bitmap?.Dispose();
        }
        base.Dispose(disposing);
    }
}
