using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
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
            Test("PNG: точные RGB полупрозрачных пикселей при экспорте и повторном импорте", () =>
            {
                int[] alpha = [0, 1, 2, 16, 32, 63, 64, 127, 128, 129, 191, 200, 222, 253, 254, 255];
                var document = new PixelDocument(alpha.Length, 1);
                for (int x = 0; x < alpha.Length; x++) document.SetPixel(x, 0, alpha[x] == 0 ? 0 : (alpha[x] << 24) | 0x173DC7);
                foreach (int scale in new[] { 1, 4, 16 })
                {
                    string file = Path.Combine(folder, "alpha-" + scale + ".png"); ImageFiles.ExportPng(document, file, scale);
                    using var bitmap = new Bitmap(file);
                    var imported = ImageFiles.Open(file);
                    for (int y = 0; y < bitmap.Height; y++)
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int expected = document.GetPixel(x / scale, 0);
                        Equal(expected, bitmap.GetPixel(x, y).ToArgb()); Equal(expected, imported.GetPixel(x, y));
                    }
                }
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
                using var form = CreateEditor(new PixelDocument(8, 8), folder);
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
            Test("UI: пипетка выбирает цвет и включает карандаш без лишнего штриха", () =>
            {
                var document = new PixelDocument(8, 8);
                int color = Color.FromArgb(23, 61, 199).ToArgb(); document.SetPixel(0, 0, color);
                using var editor = CreateEditor(document, folder);
                editor.Show(); Application.DoEvents();
                var controls = Descendants(editor).ToArray();
                var canvas = controls.OfType<CanvasControl>().Single();
                controls.OfType<Button>().Single(button => button.AccessibleName == "Пипетка · I").PerformClick();
                float zoom = canvas.ZoomPercent / 100f;
                var point = new Point((int)((canvas.Width - 8 * zoom) / 2 + zoom / 2),
                    (int)((canvas.Height - 8 * zoom) / 2 + zoom / 2));
                Mouse(canvas, "OnMouseDown", point); Mouse(canvas, "OnMouseUp", point);
                Equal(color, canvas.PrimaryColor); Equal(EditorTool.Pencil, canvas.Tool);
                var session = GetSession(editor); Check(!session.CanUndo); Check(!session.IsDirty);
                var next = new Point(point.X + (int)zoom, point.Y);
                Mouse(canvas, "OnMouseDown", next); Mouse(canvas, "OnMouseUp", next);
                Equal(color, session.Document.GetPixel(1, 0)); Check(session.CanUndo);
                editor.Hide();
            });
            Test("UI: своя палитра сохраняется в проекте и не меняет другие проекты", () =>
            {
                var library = new ProjectLibrary(Path.Combine(folder, "custom-palette"));
                var project = library.Create("Мои цвета", new PixelDocument(2, 2));
                using var editor = new MainForm(library, project, new AppSettings());
                editor.Show(); Application.DoEvents();
                var controls = Descendants(editor).ToArray();
                Check(!controls.OfType<ComboBox>().Any());
                Check(controls.OfType<Button>().Any(button => button.Name == "AddPaletteColor"));
                var add = controls.OfType<Button>().Single(button => button.Name == "AddCurrentPaletteColor");
                var canvas = controls.OfType<CanvasControl>().Single();
                int color = Color.FromArgb(23, 61, 199).ToArgb();
                canvas.PrimaryColor = color; add.PerformClick();
                var session = GetSession(editor); Equal(color, session.Document.Palette[^1]);
                canvas.PrimaryColor = 0; add.PerformClick(); add.PerformClick();
                Equal(18, session.Document.Palette.Count);
                session.Undo(); Equal(17, session.Document.Palette.Count);
                session.Redo(); Equal(18, session.Document.Palette.Count);
                editor.SaveCurrentProject();
                var saved = library.Load(project.Id);
                Equal(color, saved.Palette[16]); Equal(0, saved.Palette[17]);
                Check(saved.CopyPixels().All(pixel => pixel == 0));
                var other = library.Create("Другой", new PixelDocument(2, 2));
                Equal(16, library.Load(other.Id).Palette.Count);
                editor.Hide();
                using var reopened = new MainForm(library, project, new AppSettings());
                Equal(18, GetSession(reopened).Document.Palette.Count);
                Equal(color, GetSession(reopened).Document.Palette[16]);
            });
            Test("UI: смещение пятна — штрих, Undo/Redo, отмена и новый захват", () =>
            {
                int red = Color.Red.ToArgb(), blue = Color.Blue.ToArgb();
                var document = new PixelDocument(8, 8);
                document.SetPixels(Enumerable.Repeat(blue, 64).ToArray()); document.SetPixel(1, 3, red);
                var before = document.Clone(); var session = new EditorSession(document);
                using var form = new Form { ClientSize = new Size(400, 400), ShowInTaskbar = false };
                var canvas = new CanvasControl(session) { Dock = DockStyle.Fill, Tool = EditorTool.Smudge, SmudgeSize = 1,
                    SmudgeStrength = 50, PrimaryColor = Color.Lime.ToArgb() };
                form.Controls.Add(canvas); form.Show(); Application.DoEvents(); canvas.FitToWindow();
                Point At(int x, int y) => new(36 + x * 41 + 20, 36 + y * 41 + 20);
                Mouse(canvas, "OnMouseDown", At(1, 3));
                canvas.SmudgeSize = 3; canvas.SmudgeStrength = 100; // Текущий штрих сохраняет начальные параметры.
                Mouse(canvas, "OnMouseMove", At(5, 3)); var moved = session.Document.Clone();
                Mouse(canvas, "OnMouseUp", At(5, 3)); Check(session.Document.ContentEquals(moved));
                Equal(unchecked((int)0xFF800080), session.Document.GetPixel(2, 3));
                Equal(blue, session.Document.GetPixel(2, 2));
                session.Undo(); Check(session.Document.ContentEquals(before)); Check(!session.CanUndo);
                session.Redo(); Check(session.Document.ContentEquals(moved));
                Mouse(canvas, "OnMouseDown", At(1, 3)); Mouse(canvas, "OnMouseMove", At(7, 3)); canvas.CancelGesture();
                Check(session.Document.ContentEquals(moved));
                session.Load(before.Clone()); canvas.SmudgeSize = 1;
                Mouse(canvas, "OnMouseDown", At(6, 3)); Mouse(canvas, "OnMouseUp", At(7, 3));
                Check(session.Document.ContentEquals(before)); Check(!session.CanUndo);
                form.Close();
            });
            Test("UI: смещение не меняет холст при щелчке, пустом штрихе и панорамировании", () =>
            {
                var document = new PixelDocument(8, 8); document.SetPixel(1, 1, Color.Red.ToArgb()); var before = document.Clone();
                var session = new EditorSession(document);
                using var form = new Form { ClientSize = new Size(400, 400), ShowInTaskbar = false };
                var canvas = new CanvasControl(session) { Dock = DockStyle.Fill, Tool = EditorTool.Smudge, SmudgeSize = 1 };
                form.Controls.Add(canvas); form.Show(); Application.DoEvents(); canvas.FitToWindow();
                Point At(int x, int y) => new(36 + x * 41 + 20, 36 + y * 41 + 20);
                Mouse(canvas, "OnMouseDown", At(1, 1)); Mouse(canvas, "OnMouseUp", At(1, 1));
                Mouse(canvas, "OnMouseDown", At(0, 6)); Mouse(canvas, "OnMouseUp", At(7, 6));
                canvas.SmudgeStrength = 0;
                Mouse(canvas, "OnMouseDown", At(1, 1)); Mouse(canvas, "OnMouseUp", At(6, 1));
                Key(canvas, "OnKeyDown", Keys.Space);
                Mouse(canvas, "OnMouseDown", At(1, 1)); Mouse(canvas, "OnMouseMove", At(4, 4)); Mouse(canvas, "OnMouseUp", At(4, 4));
                Key(canvas, "OnKeyUp", Keys.Space);
                Check(session.Document.ContentEquals(before)); Check(!session.CanUndo); Equal(EditorTool.Smudge, canvas.Tool);
                form.Close();
            });
            Test("UI: кнопка S, параметры смещения и сохранение настроек", () =>
            {
                var settings = new AppSettings(); var library = new ProjectLibrary(Path.Combine(folder, "smudge-settings"));
                var project = library.Create("Пятно", new PixelDocument(8, 8));
                using var editor = new MainForm(library, project, settings); editor.Show(); Application.DoEvents();
                var controls = Descendants(editor).ToArray(); var canvas = controls.OfType<CanvasControl>().Single();
                var size = controls.OfType<NumericUpDown>().Single(input => input.Name == "SmudgeSize");
                var strength = controls.OfType<NumericUpDown>().Single(input => input.Name == "SmudgeStrength");
                Check(!size.Visible && !strength.Visible);
                controls.OfType<Button>().Single(button => button.AccessibleName == "Смещение пятна · S").PerformClick();
                Equal(EditorTool.Smudge, canvas.Tool); Check(size.Visible && strength.Visible);
                size.Value = 12; strength.Value = 42;
                Equal(12, canvas.SmudgeSize); Equal(42, canvas.SmudgeStrength);
                controls.OfType<Button>().Single(button => button.AccessibleName == "Карандаш · B").PerformClick();
                Check(!size.Visible && !strength.Visible);
                var processKey = typeof(MainForm).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance)!;
                canvas.Focus(); processKey.Invoke(editor, [new Message(), Keys.S]); Equal(EditorTool.Smudge, canvas.Tool);
                var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
                Equal(12, restored.SmudgeSize); Equal(42, restored.SmudgeStrength);
                var oldSettings = JsonSerializer.Deserialize<AppSettings>("{}")!; Equal(8, oldSettings.SmudgeSize); Equal(80, oldSettings.SmudgeStrength);
                editor.Hide();
                using var reopened = new MainForm(library, project, restored);
                var restoredCanvas = Descendants(reopened).OfType<CanvasControl>().Single();
                Equal(12, restoredCanvas.SmudgeSize); Equal(42, restoredCanvas.SmudgeStrength);
            });
            Test("Smudge: результат сохраняется в проекте и PNG с прозрачностью", () =>
            {
                var document = new PixelDocument(64, 32);
                RasterDrawing.Ellipse(document, 6, 7, 25, 26, unchecked((int)0xFFE65950), true);
                RasterDrawing.Ellipse(document, 25, 7, 44, 26, unchecked((int)0xFF409EBD), true);
                string artifacts = Path.Combine(FindRoot(), "artifacts"); Directory.CreateDirectory(artifacts);
                ImageFiles.ExportPng(document, Path.Combine(artifacts, "smudge-before.png"), 8);
                var library = new ProjectLibrary(Path.Combine(folder, "smudge-persistence")); var project = library.Create("Смещение пятна", document);
                using var editor = new MainForm(library, project, new AppSettings()); var session = GetSession(editor);
                session.Apply(doc => new SmudgeBrush(doc, 16, 17, 9, 80).MoveTo(52, 17));
                editor.SaveCurrentProject(); var saved = library.Load(project.Id);
                Check(saved.ContentEquals(session.Document)); Equal(16, saved.Palette.Count);
                Check(!saved.ContentEquals(document));
                string png = Path.Combine(folder, "smudged.png"); ImageFiles.ExportPng(saved, png, 1);
                using var image = new Bitmap(png);
                Check(saved.CopyPixels().SequenceEqual(ImageFiles.Open(png).CopyPixels()));
                for (int y = 0; y < saved.Height; y++)
                for (int x = 0; x < saved.Width; x++) Equal(saved.GetPixel(x, y), image.GetPixel(x, y).ToArgb());
                ImageFiles.ExportPng(saved, Path.Combine(artifacts, "smudge-after.png"), 8);
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
                using var form = CreateEditor(doc, folder);
                form.Show(); Application.DoEvents();
                Check(form.MainMenuStrip?.Items.Count == 5);
                Check(form.Controls.OfType<Panel>().Any());
                using var screenshot = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(screenshot, new Rectangle(0, 0, form.Width, form.Height));
                Directory.CreateDirectory(Path.Combine(root, "artifacts"));
                screenshot.Save(Path.Combine(root, "artifacts", "interface.png"), ImageFormat.Png);
                form.Hide(); // Dispose закроет тестовую форму без записи пользовательских настроек.
            });
            Test("UI: автосохранение по таймеру и Undo после сохранения", () =>
            {
                var library = new ProjectLibrary(Path.Combine(folder, "autosave"));
                var project = library.Create("Автосохранение", new PixelDocument(3, 2));
                using var editor = new MainForm(library, project, new AppSettings());
                editor.Show(); Application.DoEvents();
                var session = GetSession(editor);
                session.Apply(doc => doc.SetPixel(1, 1, 7));
                Equal(0, library.Load(project.Id).GetPixel(1, 1));
                WaitFor(() => !session.IsDirty, 5000);
                Equal(7, library.Load(project.Id).GetPixel(1, 1));
                session.Undo(); Check(session.IsDirty); editor.SaveCurrentProject();
                Equal(0, library.Load(project.Id).GetPixel(1, 1)); Check(session.CanRedo);
                Check(!Descendants(editor).OfType<Label>().Any(label => label.Text == "PIXEL / ART"));
                editor.Hide();
            });
            Test("UI: ошибка автосохранения не скрывает изменения", () =>
            {
                var library = new ProjectLibrary(Path.Combine(folder, "autosave-error"));
                var project = library.Create("Защищенный", new PixelDocument(2, 2));
                using var editor = new MainForm(library, project, new AppSettings());
                var session = GetSession(editor);
                var attributes = File.GetAttributes(project.FilePath);
                try
                {
                    File.SetAttributes(project.FilePath, attributes | FileAttributes.ReadOnly);
                    session.Apply(doc => doc.SetPixel(0, 0, 9));
                    typeof(MainForm).GetMethod("AutoSave", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, null);
                    Check(session.IsDirty); Equal(0, library.Load(project.Id).GetPixel(0, 0));
                    Check(Descendants(editor).OfType<StatusStrip>().SelectMany(strip => strip.Items.OfType<ToolStripStatusLabel>())
                        .Any(label => label.Text == "Ошибка сохранения"));
                }
                finally { File.SetAttributes(project.FilePath, attributes); }
                editor.SaveCurrentProject(); Check(!session.IsDirty); Equal(9, library.Load(project.Id).GetPixel(0, 0));
            });
            Test("UI: главная, пустая библиотека, карточки, поврежденный файл", () =>
            {
                var library = new ProjectLibrary(Path.Combine(folder, "home-gallery"));
                using var home = new HomeForm(new AppSettings(), library);
                home.Show(); Application.DoEvents();
                Check(Descendants(home).OfType<Button>().Any(button => button.Name == "CreateProject"));
                Check(Descendants(home).OfType<Button>().Any(button => button.Name == "ImportProject"));
                Check(Descendants(home).OfType<Label>().Any(label => label.Text.StartsWith("ПОКА ЧИСТЫЙ ЛИСТ")));
                var sample = ImageFiles.Open(Path.Combine(FindRoot(), "samples", "landscape.pixelart"));
                var first = library.Create("Горное озеро", sample);
                var icon = new PixelDocument(16, 16);
                RasterDrawing.Rectangle(icon, 3, 3, 12, 12, Color.Coral.ToArgb(), true);
                RasterDrawing.Rectangle(icon, 6, 6, 9, 9, Color.Gold.ToArgb(), true);
                library.Create("Маленький значок", icon);
                library.Create("Чистый лист", new PixelDocument(32, 24));
                var broken = library.Create("Поврежденный", new PixelDocument(1, 1)); File.WriteAllText(broken.FilePath, "{");
                home.RefreshProjects();
                Equal(4, Descendants(home).Count(control => control.Name.StartsWith("ProjectCard:")));
                Check(Descendants(home).OfType<Label>().Any(label => label.Text == "Не удалось прочитать проект"));
                library.Delete(broken.Id); library.Rename(first.Id, "Тихий вечер"); home.RefreshProjects();
                Equal(3, Descendants(home).Count(control => control.Name.StartsWith("ProjectCard:")));
                Check(Descendants(home).OfType<Label>().Any(label => label.Text == "Тихий вечер"));
                using var image = new Bitmap(home.Width, home.Height);
                home.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                Directory.CreateDirectory(Path.Combine(FindRoot(), "artifacts"));
                image.Save(Path.Combine(FindRoot(), "artifacts", "home.png"), ImageFormat.Png);
                home.Hide();
            });
            Test("UI: открытие из главной, сохранение при возврате", () =>
            {
                var library = new ProjectLibrary(Path.Combine(folder, "home-navigation"));
                var project = library.Create("Переходы", new PixelDocument(2, 2));
                using var home = new HomeForm(new AppSettings(), library);
                home.Show(); Application.DoEvents();
                bool opened = false;
                using var closer = new System.Windows.Forms.Timer { Interval = 100 };
                closer.Tick += (_, _) =>
                {
                    var editor = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
                    if (editor == null) return;
                    closer.Stop(); opened = true;
                    GetSession(editor).Apply(doc => doc.SetPixel(0, 0, 5));
                    editor.Close();
                };
                closer.Start();
                Descendants(home).OfType<Button>().First(button => button.Text == "Открыть").PerformClick();
                Check(opened); Check(home.Visible); Equal(5, library.Load(project.Id).GetPixel(0, 0));
                home.Hide();
            });
            Test("UI: импорт при запуске — копия, библиотечный путь без дубликата", () =>
            {
                var library = new ProjectLibrary(Path.Combine(folder, "startup-library"));
                string external = Path.Combine(folder, "original.pixelart");
                new JsonDocumentStore().Save(new PixelDocument(2, 2), external);
                string original = File.ReadAllText(external);
                void OpenStartup(string path, int color)
                {
                    bool opened = false;
                    using var home = new HomeForm(new AppSettings(), library, ImageFiles.Open(path), path);
                    using var closer = new System.Windows.Forms.Timer { Interval = 100 };
                    closer.Tick += (_, _) =>
                    {
                        var editor = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
                        if (editor == null) return;
                        closer.Stop(); opened = true;
                        GetSession(editor).Apply(doc => doc.SetPixel(0, 0, color));
                        editor.Close();
                    };
                    closer.Start(); home.Show();
                    WaitFor(() => opened && home.Visible, 5000);
                    home.Hide();
                }
                OpenStartup(external, 7);
                Equal(original, File.ReadAllText(external));
                var project = library.GetProjects().Single(); Equal(7, library.Load(project.Id).GetPixel(0, 0));
                OpenStartup(project.FilePath, 8);
                Equal(1, library.GetProjects().Count); Equal(8, library.Load(project.Id).GetPixel(0, 0));
            });
            Test("UI: подключение, переключение, дубликаты и отключение папок", () =>
            {
                string root = Path.Combine(folder, "folder-ui"); Directory.CreateDirectory(root);
                var local = new ProjectLibrary(Path.Combine(root, "local")); local.Create("Локальный", new PixelDocument(1, 1));
                string first = Path.Combine(root, "Google Drive Projects"), second = Path.Combine(root, "Other Projects");
                Directory.CreateDirectory(first); Directory.CreateDirectory(second);
                var store = new JsonDocumentStore();
                string original = Path.Combine(first, "Cloud art.pixelart"); store.Save(new PixelDocument(3, 2), original);
                store.Save(new PixelDocument(2, 3), Path.Combine(second, "Other art.json"));
                string before = File.ReadAllText(original);
                var settings = new AppSettings();
                using var home = new HomeForm(settings, local); home.Show(); Application.DoEvents();
                Check(Descendants(home).OfType<Button>().Any(button => button.Name == "ConnectProjectFolder" && button.Text == "+"));
                home.ConnectProjectFolder(first); home.ConnectProjectFolder(first + Path.DirectorySeparatorChar);
                home.ConnectProjectFolder(second); Equal(2, settings.ProjectFolders.Count);
                var picker = Descendants(home).OfType<ComboBox>().Single(combo => combo.Name == "ProjectFolders");
                Equal(3, picker.Items.Count); picker.SelectedIndex = 1;
                Check(ProjectLibrary.SamePath(first, settings.SelectedProjectFolder!));
                Check(Descendants(home).OfType<Label>().Any(label => label.Text == "Cloud art"));
                Equal(before, File.ReadAllText(original)); Equal(0, Directory.GetDirectories(first).Length);
                using var image = new Bitmap(home.Width, home.Height);
                home.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                image.Save(Path.Combine(FindRoot(), "artifacts", "folders.png"), ImageFormat.Png);
                Descendants(home).OfType<Button>().Single(button => button.Name == "DisconnectProjectFolder").PerformClick();
                Equal(1, settings.ProjectFolders.Count); Check(File.Exists(original));
                Check(ProjectLibrary.SamePath(local.RootPath, settings.SelectedProjectFolder!));
                home.SelectProjectFolder(second);
                var restoredSettings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
                home.Hide();
                using var reopened = new HomeForm(restoredSettings, local); reopened.Show(); Application.DoEvents();
                Check(Descendants(reopened).OfType<Label>().Any(label => label.Text == "Other art"));
                Equal(2, Descendants(reopened).OfType<ComboBox>().Single().Items.Count);
                reopened.Hide();
            });
            Test("UI: недоступная папка остается в списке и восстанавливается", () =>
            {
                string root = Path.Combine(folder, "folder-offline"); Directory.CreateDirectory(root);
                string connected = Path.Combine(root, "Drive"), disconnected = Path.Combine(root, "Drive-moved"); Directory.CreateDirectory(connected);
                new JsonDocumentStore().Save(new PixelDocument(1, 1), Path.Combine(connected, "art.pixelart"));
                var settings = new AppSettings(); using var home = new HomeForm(settings, new ProjectLibrary(Path.Combine(root, "local")));
                home.Show(); Application.DoEvents(); home.ConnectProjectFolder(connected);
                Directory.Move(connected, disconnected); home.RefreshProjects();
                Check(!Descendants(home).OfType<Button>().Single(button => button.Name == "CreateProject").Enabled);
                Check(!Descendants(home).OfType<Button>().Single(button => button.Name == "ImportProject").Enabled);
                Check(Descendants(home).OfType<Label>().Any(label => label.Text == "ПАПКА НЕДОСТУПНА"));
                Equal(1, settings.ProjectFolders.Count); Check(!Directory.Exists(connected));
                Directory.Move(disconnected, connected); home.RefreshProjects();
                Check(Descendants(home).OfType<Button>().Single(button => button.Name == "CreateProject").Enabled);
                Equal(1, Descendants(home).Count(control => control.Name.StartsWith("ProjectCard:")));
                home.Hide();
            });
            Test("UI: найденный файл сохраняется в выбранной папке без копирования", () =>
            {
                string root = Path.Combine(folder, "folder-edit"); Directory.CreateDirectory(root);
                var local = new ProjectLibrary(Path.Combine(root, "local")); var localProject = local.Create("Не менять", new PixelDocument(1, 1));
                string connected = Path.Combine(root, "connected"); Directory.CreateDirectory(connected);
                string file = Path.Combine(connected, "existing.pixelart"); new JsonDocumentStore().Save(new PixelDocument(2, 2), file);
                var settings = new AppSettings(); using var home = new HomeForm(settings, local);
                home.Show(); Application.DoEvents(); home.ConnectProjectFolder(connected);
                bool opened = false; using var closer = new System.Windows.Forms.Timer { Interval = 100 };
                closer.Tick += (_, _) =>
                {
                    var editor = Application.OpenForms.OfType<MainForm>().FirstOrDefault(); if (editor == null) return;
                    closer.Stop(); opened = true; GetSession(editor).Apply(doc => doc.SetPixel(0, 0, 8)); editor.Close();
                };
                closer.Start(); Descendants(home).OfType<Button>().First(button => button.Text == "Открыть").PerformClick();
                Check(opened && home.Visible); Equal(8, new JsonDocumentStore().Load(file).GetPixel(0, 0));
                Equal(0, local.Load(localProject.Id).GetPixel(0, 0)); Equal(0, Directory.GetDirectories(connected).Length);
                var created = new ProjectLibrary(connected, true).Create("Новый в этой папке", new PixelDocument(1, 1));
                Check(created.FilePath.StartsWith(connected + Path.DirectorySeparatorChar));
                home.RefreshProjects(); Equal(2, Descendants(home).Count(control => control.Name.StartsWith("ProjectCard:")));
                home.Hide();
            });
            Test("UI: запуск файла из неактивной подключенной папки без дубликата", () =>
            {
                string root = Path.Combine(folder, "folder-startup"); Directory.CreateDirectory(root);
                var local = new ProjectLibrary(Path.Combine(root, "local"));
                string connected = Path.Combine(root, "Drive"); Directory.CreateDirectory(connected);
                string file = Path.Combine(connected, "art.json"); new JsonDocumentStore().Save(new PixelDocument(1, 1), file);
                var settings = new AppSettings { ProjectFolders = new List<string> { connected }, SelectedProjectFolder = local.RootPath };
                using var home = new HomeForm(settings, local, ImageFiles.Open(file), file);
                bool opened = false; using var closer = new System.Windows.Forms.Timer { Interval = 100 };
                closer.Tick += (_, _) =>
                {
                    var editor = Application.OpenForms.OfType<MainForm>().FirstOrDefault(); if (editor == null) return;
                    closer.Stop(); opened = true; editor.Close();
                };
                closer.Start(); home.Show(); WaitFor(() => opened && home.Visible, 5000);
                Check(ProjectLibrary.SamePath(connected, settings.SelectedProjectFolder!));
                Equal(0, Directory.GetDirectories(connected).Length); Equal(0, local.GetProjects().Count);
                home.Hide();
            });
            Test("UI: изменение файла снаружи не перезаписывается автосохранением", () =>
            {
                string root = Path.Combine(folder, "sync-conflict"); Directory.CreateDirectory(root);
                string file = Path.Combine(root, "shared.pixelart"); var store = new JsonDocumentStore();
                store.Save(new PixelDocument(2, 2), file); var library = new ProjectLibrary(root, true);
                var project = library.GetProjects().Single(); using var editor = new MainForm(library, project, new AppSettings());
                var session = GetSession(editor); session.Apply(doc => doc.SetPixel(0, 0, 7));
                var remote = new PixelDocument(2, 2); remote.SetPixel(0, 0, 9); store.Save(remote, file);
                typeof(MainForm).GetMethod("AutoSave", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, null);
                Equal(9, store.Load(file).GetPixel(0, 0)); Equal(7, session.Document.GetPixel(0, 0)); Check(session.IsDirty);
                Throws<IOException>(() => editor.SaveCurrentProject());
                // Одинаковое содержимое с новой датой записи не считается конфликтом.
                store.Save(new PixelDocument(2, 2), file); editor.SaveCurrentProject();
                Equal(7, store.Load(file).GetPixel(0, 0)); Check(!session.IsDirty);
            });
            Test("Настройки папок: старый JSON, дубликаты и недоступные пути", () =>
            {
                string local = Path.Combine(folder, "settings-local"), connected = Path.Combine(folder, "settings-drive");
                var old = JsonSerializer.Deserialize<AppSettings>("{}")!; old.NormalizeProjectFolders(local);
                Equal(0, old.ProjectFolders.Count); Check(ProjectLibrary.SamePath(local, old.SelectedProjectFolder!));
                var settings = new AppSettings { ProjectFolders = new List<string> { connected, connected + Path.DirectorySeparatorChar, local, null!, "\0" }, SelectedProjectFolder = "\0" };
                settings.NormalizeProjectFolders(local); Equal(1, settings.ProjectFolders.Count);
                Check(ProjectLibrary.SamePath(local, settings.SelectedProjectFolder!)); Check(!Directory.Exists(connected));
                settings.ProjectFolders = null!; settings.NormalizeProjectFolders(local); Equal(0, settings.ProjectFolders.Count);
            });
            Test("Диалог проекта: имя, размеры и переименование", () =>
            {
                using var create = new ProjectDialog("Создать", "Мой проект", 999, 12);
                Equal("Мой проект", create.ProjectName); Equal(256, create.WidthPixels); Equal(12, create.HeightPixels);
                using var rename = new ProjectDialog("Имя", "Новое имя", withSize: false);
                Equal(0, Descendants(rename).OfType<NumericUpDown>().Count());
                Equal("Новое имя", rename.ProjectName);
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
                string corrupt = Path.Combine(folder, "corrupt.pixelart"); File.WriteAllText(corrupt, "{");
                Equal(1, RunCli([corrupt, "--export", Path.Combine(folder, "invalid.png")]).Code);
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

    private static EditorSession GetSession(MainForm editor) =>
        (EditorSession)typeof(MainForm).GetField("session", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(editor)!;

    private static void WaitFor(Func<bool> condition, int timeout)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < timeout) { Application.DoEvents(); Thread.Sleep(10); }
        Check(condition());
    }

    private static MainForm CreateEditor(PixelDocument document, string folder)
    {
        var library = new ProjectLibrary(Path.Combine(folder, Guid.NewGuid().ToString("N")));
        return new MainForm(library, library.Create("Тестовый проект", document), new AppSettings());
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
