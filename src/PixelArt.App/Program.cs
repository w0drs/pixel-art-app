using System.Runtime.InteropServices;
using System.Text;
using PixelArt.Core;

namespace PixelArt.App;

internal static class Program
{
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        // WinExe не открывает лишнюю консоль. Для CLI используем консоль родителя,
        // а при перенаправлении вывода — уже унаследованные потоки.
        if (args.Length > 0) AttachConsole(unchecked((uint)-1));
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            var options = CommandLine.Parse(args);
            if (options.ShowHelp) { Console.WriteLine(CommandLine.HelpText); return 0; }
            var document = options.CreateDocument();
            if (options.SavePath != null)
            {
                new JsonDocumentStore().Save(document, options.SavePath);
                Console.WriteLine("Проект сохранен: " + Path.GetFullPath(options.SavePath));
            }
            if (options.ExportPath != null)
            {
                ImageFiles.ExportPng(document, options.ExportPath, options.Scale);
                Console.WriteLine("PNG сохранен: " + Path.GetFullPath(options.ExportPath));
            }
            if (!options.Headless)
            {
                var settings = AppSettings.Load();
                if (options.HideGrid) settings.ShowGrid = false;
                bool modified = options.RotateClockwise.HasValue || options.MoveX != 0 || options.MoveY != 0;
                Application.Run(new MainForm(document, options.InputPath, settings, modified));
            }
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or NotSupportedException or ExternalException or OutOfMemoryException)
        {
            string message = ex is OutOfMemoryException ? "Недостаточно памяти или формат изображения поврежден/не поддерживается." : ex.Message;
            Console.Error.WriteLine("Ошибка: " + message);
            // Ошибки запуска с параметрами не должны блокировать скрипты диалоговым окном.
            if (args.Length == 0) MessageBox.Show(message, "Ошибка PixelArt", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
