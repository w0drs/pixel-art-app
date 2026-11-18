using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using PixelArt.Core;

namespace PixelArt.App;

public static class ImageFiles
{
    public static PixelDocument Open(string path)
    {
        if (Path.GetExtension(path).Equals(JsonDocumentStore.Extension, StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return new JsonDocumentStore().Load(path);
        using var image = Image.FromFile(path);
        return Import(image);
    }

    // Большие картинки уменьшаются пропорционально до 256 x 256 без сглаживания.
    public static PixelDocument Import(Image image)
    {
        double ratio = Math.Min(1.0, 256.0 / Math.Max(image.Width, image.Height));
        int width = Math.Max(1, (int)Math.Round(image.Width * ratio));
        int height = Math.Max(1, (int)Math.Round(image.Height * ratio));
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(image, new Rectangle(0, 0, width, height));
        }
        var document = new PixelDocument(width, height);
        var colors = new HashSet<int>();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int color = bitmap.GetPixel(x, y).ToArgb();
            // У полностью прозрачных пикселей нормализуем скрытые RGB-компоненты.
            if ((color >>> 24) == 0) color = 0;
            document.SetPixel(x, y, color);
            if (colors.Count < 256) colors.Add(color);
        }
        document.Palette.Clear();
        document.Palette.AddRange(colors);
        if (document.Palette.Count < 2) document.SetPaletteSize(2);
        return document;
    }

    public static Bitmap ToBitmap(PixelDocument document)
    {
        var bitmap = new Bitmap(document.Width, document.Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < document.Height; y++)
        for (int x = 0; x < document.Width; x++)
            bitmap.SetPixel(x, y, Color.FromArgb(document.GetPixel(x, y)));
        return bitmap;
    }

    public static void ExportPng(PixelDocument document, string path, int scale)
    {
        if (scale is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(scale), "Масштаб PNG: от 1 до 16.");
        using var source = ToBitmap(document);
        using var target = new Bitmap(document.Width * scale, document.Height * scale, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(target))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(source, new Rectangle(0, 0, target.Width, target.Height));
        }
        string fullPath = Path.GetFullPath(path);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            target.Save(temporary, ImageFormat.Png);
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
