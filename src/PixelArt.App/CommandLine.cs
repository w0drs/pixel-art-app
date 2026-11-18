using PixelArt.Core;

namespace PixelArt.App;

public sealed class CommandLine
{
    public string? InputPath { get; private set; }
    public string? SavePath { get; private set; }
    public string? ExportPath { get; private set; }
    public int Width { get; private set; } = 32;
    public int Height { get; private set; } = 32;
    public int Scale { get; private set; } = 1;
    public bool? RotateClockwise { get; private set; }
    public int MoveX { get; private set; }
    public int MoveY { get; private set; }
    public bool ShowHelp { get; private set; }
    public bool HideGrid { get; private set; }
    public bool Headless => SavePath != null || ExportPath != null || ShowHelp;

    public const string HelpText = """
        PixelArt — редактор пиксельных изображений (.NET 10, Windows)

        PixelArt.App.exe [файл | --open файл] [параметры]
        --new W H           Новый прозрачный холст, размеры 1..256
        --rotate right|left Поворот на +90 / -90 градусов
        --move DX DY        Сдвиг пикселей (края обрезаются)
        --save файл         Сохранить проект .pixelart (или .json)
        --export файл.png   Экспортировать PNG
        --scale N           Размер пикселя PNG, 1..16 (по умолчанию 1)
        --no-grid           Скрыть сетку при запуске интерфейса
        --help              Эта справка; -h также поддерживается

        Если есть --save или --export, окно не открывается.
        Порядок операций: открыть/создать → повернуть → сдвинуть → сохранить/экспортировать.
        Пример: PixelArt.App.exe art.pixelart --rotate right --export out.png --scale 8
        Пример: PixelArt.App.exe --new 32 32 --save blank.pixelart
        Пути с пробелами заключайте в двойные кавычки. Код возврата: 0 — успех, 1 — ошибка.
        """;

    public static CommandLine Parse(string[] args)
    {
        var result = new CommandLine();
        var seen = new HashSet<string>();
        bool newCanvas = false;
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            if (option.StartsWith('-') && !seen.Add(option))
                throw new ArgumentException($"Параметр {option} указан повторно.");
            string Next()
            {
                if (++i >= args.Length) throw new ArgumentException($"После {option} не хватает значения.");
                return args[i];
            }
            int Number(int min, int max)
            {
                if (!int.TryParse(Next(), out int value) || value < min || value > max)
                    throw new ArgumentException($"{option}: ожидается целое число от {min} до {max}.");
                return value;
            }
            switch (option)
            {
                case "--help": case "-h": result.ShowHelp = true; break;
                case "--new":
                    newCanvas = true;
                    result.Width = Number(1, 256); result.Height = Number(1, 256); break;
                case "--open":
                    if (result.InputPath != null) throw new ArgumentException("Укажите только один входной файл.");
                    result.InputPath = Next(); break;
                case "--save": result.SavePath = Next(); break;
                case "--export": result.ExportPath = Next(); break;
                case "--scale": result.Scale = Number(1, 16); break;
                case "--no-grid": result.HideGrid = true; break;
                case "--rotate":
                    result.RotateClockwise = Next() switch
                    {
                        "right" => true, "left" => false,
                        _ => throw new ArgumentException("--rotate: используйте right или left.")
                    }; break;
                case "--move": result.MoveX = Number(-256, 256); result.MoveY = Number(-256, 256); break;
                default:
                    if (option.StartsWith('-')) throw new ArgumentException($"Неизвестный параметр: {option}");
                    if (result.InputPath != null) throw new ArgumentException("Укажите только один входной файл.");
                    result.InputPath = option; break;
            }
        }
        if (newCanvas && result.InputPath != null) throw new ArgumentException("--new нельзя совмещать с входным файлом.");
        if (seen.Contains("--scale") && result.ExportPath == null)
            throw new ArgumentException("--scale используется вместе с --export.");
        if (result.SavePath != null && Path.GetExtension(result.SavePath).ToLowerInvariant() is not (".pixelart" or ".json"))
            throw new ArgumentException("Проект сохраняется с расширением .pixelart или .json.");
        if (result.ExportPath != null && !Path.GetExtension(result.ExportPath).Equals(".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Для экспорта укажите файл .png.");
        return result;
    }

    public PixelDocument CreateDocument()
    {
        var document = InputPath == null ? new PixelDocument(Width, Height) : ImageFiles.Open(InputPath);
        if (RotateClockwise.HasValue) document = RasterDrawing.Rotate(document, RotateClockwise.Value);
        if (MoveX != 0 || MoveY != 0) document = RasterDrawing.Move(document, MoveX, MoveY);
        return document;
    }
}
