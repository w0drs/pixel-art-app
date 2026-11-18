using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using PixelArt.App;
using PixelArt.Core;

namespace PixelArt.App.Tests;

internal static class Program
{
    private static int passed;
    private static int failed;

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        string folder = Path.Combine(Path.GetTempPath(), "PixelArt.App.Tests-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            Test("CLI: новый холст и параметры", () =>
            {
                var options = CommandLine.Parse(["--new", "12", "9", "--save", "out.pixelart"]);
                Equal(12, options.CreateDocument().Width); Equal(9, options.Height); Check(options.Headless);
            });
            Test("CLI: поворот, сдвиг, PNG и масштаб", () =>
            {
                var options = CommandLine.Parse(["in.pixelart", "--rotate", "left", "--move", "-2", "3", "--export", "out.png", "--scale", "8"]);
                Equal("in.pixelart", options.InputPath); Equal(false, options.RotateClockwise); Equal(-2, options.MoveX); Equal(8, options.Scale);
            });
            Test("CLI: неверные параметры отклоняются", () =>
            {
                string[][] bad = [["--new", "0", "1"], ["--new", "1"], ["--unknown"],
                    ["--new", "3", "3", "a.png"], ["a.png", "b.png"], ["--open", "a.png", "--open", "b.png"],
                    ["--scale", "2"], ["--export", "a.png", "--scale", "17"], ["--rotate", "up"],
                    ["--export", "a.jpg"], ["--save", "a.png"], ["--new", "3", "3", "--new", "2", "2"]];
                foreach (var args in bad) Throws<ArgumentException>(() => CommandLine.Parse(args));
            });
            Test("CLI: справка не требует открытия окна", () => Check(CommandLine.Parse(["--help"]).Headless));
            Test("PNG: точный размер, пиксели без сглаживания, прозрачность", () =>
            {
                var doc = new PixelDocument(2, 2);
                doc.SetPixels([unchecked((int)0xFFFF0000), unchecked((int)0xFF00FF00), 0, unchecked((int)0x800000FF)]);
                string path = Path.Combine(folder, "scaled.png");
                ImageFiles.ExportPng(doc, path, 4);
                using var bitmap = new Bitmap(path);
                Equal(8, bitmap.Width); Equal(8, bitmap.Height);
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++) Equal(doc.GetPixel(x / 4, y / 4), bitmap.GetPixel(x, y).ToArgb());
                Check(ImageFiles.Open(path).GetPixel(0, 0) == doc.GetPixel(0, 0));
            });
            Test("PNG: перезапись и недопустимый масштаб", () =>
            {
                var doc = new PixelDocument(1, 1); string path = Path.Combine(folder, "overwrite.png");
                ImageFiles.ExportPng(doc, path, 1); doc.SetPixel(0, 0, -1); ImageFiles.ExportPng(doc, path, 2);
                using var result = new Bitmap(path); Equal(2, result.Width); Equal(-1, result.GetPixel(1, 1).ToArgb());
                Throws<ArgumentOutOfRangeException>(() => ImageFiles.ExportPng(doc, path, 0));
            });
            Test("Импорт: уменьшение с сохранением пропорций", () =>
            {
                using var image = new Bitmap(800, 400);
                using (var g = Graphics.FromImage(image)) g.Clear(Color.Red);
                var doc = ImageFiles.Import(image);
                Equal(256, doc.Width); Equal(128, doc.Height); Equal(Color.Red.ToArgb(), doc.GetPixel(255, 127));
            });
            Test("Импорт: узкое изображение", () =>
            {
                using var image = new Bitmap(1, 1000);
                var doc = ImageFiles.Import(image); Equal(1, doc.Width); Equal(256, doc.Height);
            });
            Test("Импорт: палитра не больше 256 цветов", () =>
            {
                using var image = new Bitmap(32, 32);
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++) image.SetPixel(x, y, Color.FromArgb(x * 8, y * 8, 40));
                Equal(256, ImageFiles.Import(image).Palette.Count);
            });
            Test("Проект открывается через общий импорт", () =>
            {
                var doc = new PixelDocument(5, 7); string path = Path.Combine(folder, "test.pixelart");
                new JsonDocumentStore().Save(doc, path); Check(ImageFiles.Open(path).ContentEquals(doc));
                var options = CommandLine.Parse([path, "--rotate", "right", "--move", "1", "0"]);
                Equal(7, options.CreateDocument().Width);
            });
            Test("UI: штрих, Undo, фигура и отмена жеста", () =>
            {
                var session = new EditorSession(new PixelDocument(8, 8));
                using var form = new Form { ClientSize = new Size(400, 400), ShowInTaskbar = false };
                var canvas = new CanvasControl(session) { Dock = DockStyle.Fill, PrimaryColor = Color.Red.ToArgb() };
                form.Controls.Add(canvas); form.Show(); Application.DoEvents(); canvas.FitToWindow();
                Point At(int x, int y) => new(36 + x * 41 + 20, 36 + y * 41 + 20);
                Mouse(canvas, "OnMouseDown", At(0, 0)); Mouse(canvas, "OnMouseMove", At(3, 2)); Mouse(canvas, "OnMouseUp", At(3, 2));
                Equal(Color.Red.ToArgb(), session.Document.GetPixel(3, 2)); Check(session.CanUndo);
                session.Undo(); Check(!session.IsDirty);
                canvas.Tool = EditorTool.Rectangle;
                Mouse(canvas, "OnMouseDown", At(1, 1)); Mouse(canvas, "OnMouseMove", At(6, 6));
                Equal(0, session.Document.GetPixel(1, 1)); // До отпускания рисуется только предпросмотр.
                Mouse(canvas, "OnMouseUp", At(6, 6)); Equal(Color.Red.ToArgb(), session.Document.GetPixel(1, 1));
                session.Undo(); canvas.Tool = EditorTool.Pencil;
                Mouse(canvas, "OnMouseDown", At(2, 2)); canvas.CancelGesture();
                Equal(0, session.Document.GetPixel(2, 2)); Check(!session.IsEditing);
                form.Close();
            });
            Test("UI: пробел временно включает панорамирование", () =>
            {
                var session = new EditorSession(new PixelDocument(8, 8));
                using var form = new Form { ClientSize = new Size(400, 400), ShowInTaskbar = false };
                var canvas = new CanvasControl(session) { Dock = DockStyle.Fill, Tool = EditorTool.Pencil };
                form.Controls.Add(canvas); form.Show(); Application.DoEvents(); canvas.FitToWindow(); canvas.Focus();
                Key(canvas, "OnKeyDown", Keys.Space);
                Key(canvas, "OnKeyDown", Keys.Space); // Автоповтор не выключает руку.
                Equal(Cursors.Hand, canvas.Cursor);
                Mouse(canvas, "OnMouseDown", new Point(56, 56));
                Mouse(canvas, "OnMouseMove", new Point(138, 97));
                Key(canvas, "OnKeyUp", Keys.Space); // Отпускание пробела останавливает панорамирование.
                Mouse(canvas, "OnMouseMove", new Point(150, 110));
                Mouse(canvas, "OnMouseUp", new Point(150, 110));
                Equal(EditorTool.Pencil, canvas.Tool); Check(!session.IsDirty); Check(!session.CanUndo);
                Equal(Cursors.Default, canvas.Cursor);
                // После сдвига прежний пиксель (0,0) находится уже в новой позиции.
                Mouse(canvas, "OnMouseDown", new Point(138, 97));
                Mouse(canvas, "OnMouseUp", new Point(138, 97));
                Equal(canvas.PrimaryColor, session.Document.GetPixel(0, 0));
                session.Undo(); Check(!session.IsDirty);
                Key(canvas, "OnKeyDown", Keys.Space);
                typeof(CanvasControl).GetMethod("OnLostFocus", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(canvas, [EventArgs.Empty]);
                Equal(Cursors.Default, canvas.Cursor);
                form.Close();
            });
            Test("UI: размеры карандаша и ластика независимы, Undo работает", () =>
            {
                var session = new EditorSession(new PixelDocument(8, 8));
                using var form = new Form { ClientSize = new Size(400, 400), ShowInTaskbar = false };
                var canvas = new CanvasControl(session) { Dock = DockStyle.Fill, PencilSize = 3, EraserSize = 1 };
                form.Controls.Add(canvas); form.Show(); Application.DoEvents(); canvas.FitToWindow();
                var center = new Point(36 + 3 * 41 + 20, 36 + 3 * 41 + 20);
                Mouse(canvas, "OnMouseDown", center);
                canvas.PencilSize = 5; // Размер фиксируется в начале штриха.
                Mouse(canvas, "OnMouseUp", center);
                Equal(9, session.Document.CopyPixels().Count(p => p != 0));
                canvas.Tool = EditorTool.Eraser;
                Mouse(canvas, "OnMouseDown", center); Mouse(canvas, "OnMouseUp", center);
                Equal(8, session.Document.CopyPixels().Count(p => p != 0));
                session.Undo(); canvas.EraserSize = 3;
                Mouse(canvas, "OnMouseDown", center); Mouse(canvas, "OnMouseUp", center);
                Equal(0, session.Document.CopyPixels().Count(p => p != 0));
                session.Undo(); Equal(9, session.Document.CopyPixels().Count(p => p != 0));
                form.Close();
            });
            Test("UI: меню фигур, повторный выбор и числовые размеры", () =>
            {
                using var form = new MainForm(new PixelDocument(8, 8), null, new AppSettings());
                form.Show(); Application.DoEvents();
                var controls = Descendants(form).ToArray();
                var canvas = controls.OfType<CanvasControl>().Single();
                var split = controls.OfType<ToolStrip>().SelectMany(strip => strip.Items.OfType<ToolStripSplitButton>()).Single();
                Equal(4, split.DropDownItems.Count);
                ((ToolStripMenuItem)split.DropDownItems[2]).PerformClick(); Equal(EditorTool.Ellipse, canvas.Tool);
                controls.OfType<Button>().Single(button => button.AccessibleName == "Карандаш · B").PerformClick();
                Equal(EditorTool.Pencil, canvas.Tool);
                split.PerformButtonClick(); Equal(EditorTool.Ellipse, canvas.Tool);
                var pencilSize = controls.OfType<NumericUpDown>().Single(input => input.Name == "PencilSize");
                var eraserSize = controls.OfType<NumericUpDown>().Single(input => input.Name == "EraserSize");
                pencilSize.Value = 4; eraserSize.Value = 7;
                Equal(4, canvas.PencilSize); Equal(7, canvas.EraserSize);
                var processKey = typeof(MainForm).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance)!;
                canvas.Focus();
                processKey.Invoke(form, [new Message(), Keys.R]); Equal(EditorTool.Rectangle, canvas.Tool);
                processKey.Invoke(form, [new Message(), Keys.Space]); Equal(Cursors.Hand, canvas.Cursor);
                Key(canvas, "OnKeyUp", Keys.Space); Equal(Cursors.Default, canvas.Cursor);
                Equal(EditorTool.Rectangle, canvas.Tool);
                form.Hide();
            });
            Test("Иконки всех инструментов созданы и не пустые", () =>
            {
                foreach (var tool in Enum.GetValues<EditorTool>())
                {
                    using var image = ToolIcons.Create(tool);
                    Equal(28, image.Width); Equal(28, image.Height);
                    int visible = 0;
                    for (int y = 0; y < image.Height; y++)
                    for (int x = 0; x < image.Width; x++) if (image.GetPixel(x, y).A > 0) visible++;
                    Check(visible > 20);
                }
            });
            Test("UI: масштаб и сетка не меняют документ", () =>
            {
                var doc = new PixelDocument(7, 4); var copy = doc.Clone();
                var session = new EditorSession(doc);
                using var canvas = new CanvasControl(session) { Size = new Size(400, 300) };
                canvas.FitToWindow(); canvas.ChangeZoom(1.25f); canvas.ShowGrid = false;
                using var image = new Bitmap(400, 300); canvas.DrawToBitmap(image, new Rectangle(0, 0, 400, 300));
                Check(session.Document.ContentEquals(copy)); Check(!session.IsDirty);
            });
            Test("UI: главная форма, меню, палитра и снимок", () =>
            {
                string root = FindRoot();
                string sample = Path.Combine(root, "samples", "landscape.pixelart");
                var doc = File.Exists(sample) ? ImageFiles.Open(sample) : new PixelDocument(32, 32);
                using var form = new MainForm(doc, File.Exists(sample) ? sample : null, new AppSettings());
                form.Show(); Application.DoEvents();
                Check(form.MainMenuStrip?.Items.Count == 5);
                Check(form.Controls.OfType<Panel>().Any());
                using var screenshot = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(screenshot, new Rectangle(0, 0, form.Width, form.Height));
                Directory.CreateDirectory(Path.Combine(root, "artifacts"));
                screenshot.Save(Path.Combine(root, "artifacts", "interface.png"), ImageFormat.Png);
                form.Hide(); // Dispose закроет тестовую форму без записи пользовательских настроек.
            });
            Test("CLI: реальный процесс, файлы и код возврата", () =>
            {
                string input = Path.Combine(folder, "input.pixelart");
                string output = Path.Combine(folder, "output.png");
                new JsonDocumentStore().Save(new PixelDocument(3, 2), input);
                var success = RunCli([input, "--rotate", "right", "--export", output, "--scale", "3"]);
                Equal(0, success.Code); Check(success.Output.Contains("PNG"));
                using var bitmap = new Bitmap(output); Equal(6, bitmap.Width); Equal(9, bitmap.Height);
                var error = RunCli(["--new", "0", "2", "--save", Path.Combine(folder, "bad.pixelart")]);
                Equal(1, error.Code); Check(error.Error.Length > 0);
                Equal(1, RunCli(["--unknown"]).Code);
                Equal(1, RunCli([Path.Combine(folder, "missing.png")]).Code);
                Equal(0, RunCli(["--help"]).Code);
            });
            Test("Диалоги чисел: отрицательный сдвиг и размеры", () =>
            {
                using var move = new NumberDialog("Move", "X", -3, -256, 256, "Y", 7);
                Equal(-3, move.First); Equal(7, move.Second);
                using var size = new NumberDialog("Size", "W", 1000, 1, 256); Equal(256, size.First);
            });
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine($"\nPassed: {passed}; failed: {failed}");
        return failed == 0 ? 0 : 1;
    }

    private static (int Code, string Output, string Error) RunCli(string[] args)
    {
        string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet.exe");
        string host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? (File.Exists(local) ? local : "dotnet");
        var start = new ProcessStartInfo(host) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(FindRoot(), "src", "PixelArt.App", "bin", "Release", "net10.0-windows", "PixelArt.App.dll"));
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(true); throw new Exception("CLI не завершился за 30 секунд."); }
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PixelArt.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("PixelArt.sln не найден.");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static void Key(CanvasControl canvas, string method, Keys key) =>
        typeof(CanvasControl).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(canvas, [new KeyEventArgs(key)]);

    private static void Mouse(CanvasControl canvas, string method, Point point) =>
        typeof(CanvasControl).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(canvas, [new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)]);

    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex); }
    }
    private static void Check(bool condition) { if (!condition) throw new Exception("Условие не выполнено."); }
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Ожидалось {expected}, получено {actual}.");
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Ожидалось исключение " + typeof(T).Name);
    }
}
