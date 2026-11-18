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
    public int SmudgeSize { get; set; } = 8;
    public int SmudgeStrength { get; set; } = 80;
    public List<string> ProjectFolders { get; set; } = new();
    public string? SelectedProjectFolder { get; set; }
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
            settings.SmudgeSize = Math.Clamp(settings.SmudgeSize, 1, PixelArt.Core.RasterDrawing.MaxBrushSize);
            settings.SmudgeStrength = Math.Clamp(settings.SmudgeStrength, 0, 100);
            if (settings.Background is not ("Светлый" or "Темный")) settings.Background = "Светлый";
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void NormalizeProjectFolders(string defaultFolder)
    {
        var normalized = new List<string>();
        foreach (string path in ProjectFolders ?? new List<string>())
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            try
            {
                string full = PixelArt.Core.ProjectLibrary.NormalizePath(path);
                if (!PixelArt.Core.ProjectLibrary.SamePath(full, defaultFolder) &&
                    !normalized.Any(existing => PixelArt.Core.ProjectLibrary.SamePath(existing, full))) normalized.Add(full);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException) { }
        }
        ProjectFolders = normalized;
        try
        {
            SelectedProjectFolder = string.IsNullOrWhiteSpace(SelectedProjectFolder)
                ? defaultFolder : PixelArt.Core.ProjectLibrary.NormalizePath(SelectedProjectFolder);
            if (!PixelArt.Core.ProjectLibrary.SamePath(SelectedProjectFolder, defaultFolder) &&
                !normalized.Any(path => PixelArt.Core.ProjectLibrary.SamePath(path, SelectedProjectFolder))) SelectedProjectFolder = defaultFolder;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            SelectedProjectFolder = defaultFolder;
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        string temporary = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, SettingsPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
