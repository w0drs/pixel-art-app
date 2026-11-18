using System.Text.Json;

namespace PixelArt.Core;

public sealed class JsonDocumentStore : IDocumentStore
{
    public const string Extension = ".pixelart";
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public PixelDocument Load(string path)
    {
        if (new FileInfo(path).Length > 8 * 1024 * 1024)
            throw new InvalidDataException("Файл проекта слишком большой (максимум 8 МБ).");
        try
        {
            var data = JsonSerializer.Deserialize<DocumentData>(File.ReadAllText(path));
            if (data == null || data.Version != 1) throw new InvalidDataException("Неизвестная версия проекта.");
            if (data.Width is < 1 or > 256 || data.Height is < 1 or > 256 ||
                data.Pixels == null || data.Pixels.Length != data.Width * data.Height ||
                data.Palette == null || data.Palette.Length is < 2 or > 256)
                throw new InvalidDataException("В файле неверные размеры, пиксели или палитра.");
            var document = new PixelDocument(data.Width, data.Height);
            document.SetPixels(data.Pixels);
            document.Palette.Clear();
            document.Palette.AddRange(data.Palette);
            return document;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Не удалось прочитать JSON проекта.", ex);
        }
    }

    public void Save(PixelDocument document, string path)
    {
        if (document.Palette.Count is < 2 or > 256)
            throw new InvalidDataException("Палитра должна содержать от 2 до 256 цветов.");
        var data = new DocumentData
        {
            Version = 1, Width = document.Width, Height = document.Height,
            Pixels = document.CopyPixels(), Palette = document.Palette.ToArray()
        };
        // Сначала пишем рядом во временный файл: ошибка записи не испортит старый проект.
        string target = Path.GetFullPath(path);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(data, Options));
            File.Move(temporary, target, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

public sealed class DocumentData
{
    public int Version { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int[]? Pixels { get; set; }
    public int[]? Palette { get; set; }
}
