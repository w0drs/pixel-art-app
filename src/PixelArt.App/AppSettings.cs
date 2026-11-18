using System.Text.Json;

namespace PixelArt.App;

public sealed class AppSettings
{
    public bool ShowGrid { get; set; } = true;
    public string Background { get; set; } = "Светлый";
    public int ExportScale { get; set; } = 4;
    public int NewWidth { get; set; } = 32;
    public int NewHeight { get; set; } = 32;
    public int PencilSize { get; set; } = 1;
    public int EraserSize { get; set; } = 1;
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelArt", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new();
            settings.ExportScale = Math.Clamp(settings.ExportScale, 1, 16);
            settings.NewWidth = Math.Clamp(settings.NewWidth, 1, 256);
            settings.NewHeight = Math.Clamp(settings.NewHeight, 1, 256);
            settings.PencilSize = Math.Clamp(settings.PencilSize, 1, PixelArt.Core.RasterDrawing.MaxBrushSize);
            settings.EraserSize = Math.Clamp(settings.EraserSize, 1, PixelArt.Core.RasterDrawing.MaxBrushSize);
            if (settings.Background is not ("Светлый" or "Темный")) settings.Background = "Светлый";
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
