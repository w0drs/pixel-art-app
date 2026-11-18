using System.Drawing.Drawing2D;

namespace PixelArt.App;

// Иконки рисуются один раз при создании окна, без внешних файлов и шрифтов-символов.
public static class ToolIcons
{
    public static Bitmap Create(EditorTool tool)
    {
        var image = new Bitmap(28, 28);
        using var g = Graphics.FromImage(image);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(24, 65, 62), 2) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var accent = new SolidBrush(Color.FromArgb(229, 166, 71));
        using var ink = new SolidBrush(pen.Color);
        switch (tool)
        {
            case EditorTool.Pencil:
                Point[] pencil = [new(5, 19), new(19, 5), new(24, 10), new(10, 24), new(4, 25)];
                g.FillPolygon(accent, pencil);
                g.DrawPolygon(pen, pencil);
                g.DrawLine(pen, 16, 8, 21, 13);
                g.FillPolygon(ink, [new Point(5, 19), new Point(10, 24), new Point(4, 25)]);
                break;
            case EditorTool.Eraser:
                Point[] eraser = [new(4, 17), new(15, 5), new(25, 14), new(16, 24), new(11, 24)];
                g.FillPolygon(accent, eraser);
                g.DrawPolygon(pen, eraser);
                g.DrawLine(pen, 10, 11, 20, 20);
                g.DrawLine(pen, 11, 24, 25, 24);
                break;
            case EditorTool.Fill:
                g.FillPolygon(accent, [new Point(3, 14), new Point(13, 4), new Point(23, 14), new Point(13, 24)]);
                g.DrawPolygon(pen, [new Point(3, 14), new Point(13, 4), new Point(23, 14), new Point(13, 24)]);
                g.DrawLine(pen, 5, 14, 21, 14);
                g.DrawLine(pen, 8, 3, 16, 11);
                g.FillEllipse(ink, 22, 19, 5, 7);
                break;
            case EditorTool.Picker:
                g.DrawLine(pen, 7, 20, 18, 9);
                g.DrawLine(pen, 10, 23, 21, 12);
                g.DrawLine(pen, 7, 20, 6, 25);
                g.DrawLine(pen, 6, 25, 10, 23);
                g.DrawLine(pen, 15, 7, 23, 15);
                g.FillEllipse(accent, 18, 3, 7, 7);
                g.DrawEllipse(pen, 18, 3, 7, 7);
                break;
            case EditorTool.Smudge:
                g.FillEllipse(accent, 2, 15, 15, 10);
                g.DrawLines(pen, [new Point(10, 22), new Point(11, 16), new Point(19, 4),
                    new Point(22, 3), new Point(25, 6), new Point(24, 9), new Point(20, 12), new Point(14, 23)]);
                g.DrawLine(pen, 3, 18, 7, 18);
                g.DrawLine(pen, 2, 23, 6, 23);
                g.DrawLine(pen, 19, 23, 25, 23);
                break;
            case EditorTool.Line:
                g.DrawLine(pen, 5, 23, 23, 5);
                g.FillRectangle(accent, 3, 21, 5, 5);
                g.FillRectangle(accent, 21, 3, 5, 5);
                break;
            case EditorTool.Rectangle:
                g.DrawRectangle(pen, 4, 6, 20, 16);
                break;
            case EditorTool.Ellipse:
                g.DrawEllipse(pen, 3, 5, 22, 18);
                break;
            case EditorTool.Grid:
                g.DrawRectangle(pen, 4, 4, 20, 20);
                for (int i = 1; i < 3; i++)
                {
                    g.DrawLine(pen, 4 + i * 7, 4, 4 + i * 7, 24);
                    g.DrawLine(pen, 4, 4 + i * 7, 24, 4 + i * 7);
                }
                break;
            case EditorTool.Pan:
                using (var path = new GraphicsPath())
                {
                    path.AddLines([new Point(9, 24), new Point(4, 15), new Point(6, 13), new Point(10, 17),
                        new Point(10, 6), new Point(13, 6), new Point(13, 13), new Point(13, 3),
                        new Point(16, 3), new Point(16, 13), new Point(16, 5), new Point(19, 5),
                        new Point(19, 14), new Point(19, 9), new Point(22, 9), new Point(22, 20), new Point(19, 25)]);
                    g.DrawPath(pen, path);
                }
                break;
        }
        return image;
    }
}
