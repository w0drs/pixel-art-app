namespace PixelArt.Core;

public static class RasterDrawing
{
    public const int MaxBrushSize = 32;

    public static void Line(PixelDocument doc, int x0, int y0, int x1, int y1, int color) =>
        BrushStroke(doc, x0, y0, x1, y1, color, 1);

    // Брезенхэм + квадратный отпечаток: даже быстрый широкий штрих не имеет пропусков.
    public static void BrushStroke(PixelDocument doc, int x0, int y0, int x1, int y1, int color, int size)
    {
        if (size < 1 || size > MaxBrushSize) throw new ArgumentOutOfRangeException(nameof(size));
        int offset = (size - 1) / 2;
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            for (int y = y0 - offset; y < y0 - offset + size; y++)
            for (int x = x0 - offset; x < x0 - offset + size; x++)
                doc.SetPixel(x, y, color);
            if (x0 == x1 && y0 == y1) break;
            int twice = 2 * error;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }

    public static void Rectangle(PixelDocument doc, int x0, int y0, int x1, int y1,
        int color, bool filled)
    {
        int left = Math.Min(x0, x1), right = Math.Max(x0, x1);
        int top = Math.Min(y0, y1), bottom = Math.Max(y0, y1);
        if (filled)
        {
            for (int y = top; y <= bottom; y++) Line(doc, left, y, right, y, color);
        }
        else
        {
            Line(doc, left, top, right, top, color);
            Line(doc, left, bottom, right, bottom, color);
            Line(doc, left, top, left, bottom, color);
            Line(doc, right, top, right, bottom, color);
        }
    }

    public static void Ellipse(PixelDocument doc, int x0, int y0, int x1, int y1,
        int color, bool filled)
    {
        int left = Math.Min(x0, x1), right = Math.Max(x0, x1);
        int top = Math.Min(y0, y1), bottom = Math.Max(y0, y1);
        double cx = (left + right) / 2.0, cy = (top + bottom) / 2.0;
        double rx = (right - left + 1) / 2.0, ry = (bottom - top + 1) / 2.0;
        bool Inside(int x, int y) =>
            Math.Pow((x - cx) / rx, 2) + Math.Pow((y - cy) / ry, 2) <= 1.0;
        for (int y = top; y <= bottom; y++)
        for (int x = left; x <= right; x++)
        {
            if (Inside(x, y) && (filled || !Inside(x - 1, y) || !Inside(x + 1, y) ||
                !Inside(x, y - 1) || !Inside(x, y + 1))) doc.SetPixel(x, y, color);
        }
    }

    // В отличие от сетки интерфейса, эти линии становятся частью изображения.
    public static void Grid(PixelDocument doc, int x0, int y0, int x1, int y1, int color, int step)
    {
        if (step < 1) throw new ArgumentOutOfRangeException(nameof(step));
        int left = Math.Min(x0, x1), right = Math.Max(x0, x1);
        int top = Math.Min(y0, y1), bottom = Math.Max(y0, y1);
        for (int x = left; x <= right; x += step) Line(doc, x, top, x, bottom, color);
        for (int y = top; y <= bottom; y += step) Line(doc, left, y, right, y, color);
        Rectangle(doc, left, top, right, bottom, color, false);
    }

    public static void FloodFill(PixelDocument doc, int x, int y, int color)
    {
        if (!doc.Contains(x, y)) return;
        int old = doc.GetPixel(x, y);
        if (old == color) return;
        // Очередь вместо рекурсии: заливка большого холста не переполнит стек.
        var queue = new Queue<(int X, int Y)>();
        doc.SetPixel(x, y, color);
        queue.Enqueue((x, y));
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            Visit(p.X - 1, p.Y); Visit(p.X + 1, p.Y);
            Visit(p.X, p.Y - 1); Visit(p.X, p.Y + 1);
        }
        void Visit(int px, int py)
        {
            if (!doc.Contains(px, py) || doc.GetPixel(px, py) != old) return;
            doc.SetPixel(px, py, color);
            queue.Enqueue((px, py));
        }
    }

    public static PixelDocument Rotate(PixelDocument source, bool clockwise)
    {
        var result = CreateLike(source, source.Height, source.Width);
        for (int y = 0; y < source.Height; y++)
        for (int x = 0; x < source.Width; x++)
        {
            int nx = clockwise ? source.Height - 1 - y : y;
            int ny = clockwise ? x : source.Width - 1 - x;
            result.SetPixel(nx, ny, source.GetPixel(x, y));
        }
        return result;
    }

    public static PixelDocument Move(PixelDocument source, int dx, int dy)
    {
        var result = CreateLike(source, source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
        for (int x = 0; x < source.Width; x++)
            result.SetPixel(x + dx, y + dy, source.GetPixel(x, y));
        return result;
    }

    private static PixelDocument CreateLike(PixelDocument source, int width, int height)
    {
        var result = new PixelDocument(width, height);
        result.Palette.Clear();
        result.Palette.AddRange(source.Palette);
        return result;
    }
}
