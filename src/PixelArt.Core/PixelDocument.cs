namespace PixelArt.Core;

// Цвет хранится как ARGB (тот же int, который использует System.Drawing.Color).
// Ноль означает полностью прозрачный пиксель. Библиотека не зависит от WinForms.
public sealed class PixelDocument
{
    public const int MaxSize = 256;
    private readonly int[] pixels;

    public int Width { get; }
    public int Height { get; }
    public List<int> Palette { get; } = new();

    public PixelDocument(int width, int height)
    {
        if (width < 1 || width > MaxSize || height < 1 || height > MaxSize)
            throw new ArgumentOutOfRangeException(nameof(width), "Размер холста: от 1 до 256 пикселей.");
        Width = width;
        Height = height;
        pixels = new int[width * height];
        SetPaletteSize(16);
    }

    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public int GetPixel(int x, int y)
    {
        if (!Contains(x, y)) throw new ArgumentOutOfRangeException(nameof(x));
        return pixels[y * Width + x];
    }

    public void SetPixel(int x, int y, int color)
    {
        // Выходящая за холст часть фигуры просто обрезается.
        if (Contains(x, y)) pixels[y * Width + x] = color;
    }

    public int[] CopyPixels() => (int[])pixels.Clone();

    public void SetPixels(int[] colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (colors.Length != pixels.Length) throw new ArgumentException("Неверное число пикселей.");
        colors.CopyTo(pixels, 0);
    }

    public PixelDocument Clone()
    {
        var copy = new PixelDocument(Width, Height);
        copy.SetPixels(pixels);
        copy.Palette.Clear();
        copy.Palette.AddRange(Palette);
        return copy;
    }

    public bool ContentEquals(PixelDocument other) =>
        Width == other.Width && Height == other.Height &&
        pixels.SequenceEqual(other.pixels) && Palette.SequenceEqual(other.Palette);

    public void SetPaletteSize(int count)
    {
        if (count is < 2 or > 256) throw new ArgumentOutOfRangeException(nameof(count));
        int[] basic = [
            unchecked((int)0xFF182833), unchecked((int)0xFFF7F3E8),
            unchecked((int)0xFFE65950), unchecked((int)0xFFF4AD44),
            unchecked((int)0xFFF6DA75), unchecked((int)0xFF93B85C),
            unchecked((int)0xFF368568), unchecked((int)0xFF205B60),
            unchecked((int)0xFF409EBD), unchecked((int)0xFF4168A0),
            unchecked((int)0xFF554E85), unchecked((int)0xFFB47BA5),
            unchecked((int)0xFFEBA9A0), unchecked((int)0xFFA76C50),
            unchecked((int)0xFF786956), unchecked((int)0xFFADB5B6)
        ];
        // При изменении размера уже настроенные пользователем цвета сохраняются.
        while (Palette.Count < count)
        {
            int i = Palette.Count;
            if (i < basic.Length) Palette.Add(basic[i]);
            else
            {
                int n = i - 16;
                int r = n % 6 * 51;
                int g = n / 6 % 6 * 51;
                int b = n / 36 % 6 * 51;
                Palette.Add(unchecked((int)0xFF000000) | r << 16 | g << 8 | b);
            }
        }
        if (Palette.Count > count) Palette.RemoveRange(count, Palette.Count - count);
    }
}
