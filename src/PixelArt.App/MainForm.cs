using PixelArt.Core;

namespace PixelArt.App;

public sealed class MainForm : Form
{
    private readonly EditorSession session;
    private readonly AppSettings settings;
    private readonly CanvasControl canvas;
    private readonly PaletteControl palette;
    private readonly Label colorLabel = new() { AutoSize = true, MaximumSize = new Size(216, 0) };
    private readonly ToolStripStatusLabel documentStatus = new();
    private readonly ToolStripStatusLabel cursorStatus = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly ToolStripStatusLabel zoomStatus = new();
    private readonly Dictionary<EditorTool, Button> toolButtons = new();
    private readonly Dictionary<EditorTool, Bitmap> toolIcons = Enum.GetValues<EditorTool>().ToDictionary(tool => tool, ToolIcons.Create);
    private readonly Dictionary<EditorTool, ToolStripMenuItem> shapeItems = new();
    private readonly ToolStripSplitButton shapesButton = new();
    private EditorTool selectedShape = EditorTool.Rectangle;
    private readonly ComboBox paletteSize = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 212 };
    private readonly ToolStripMenuItem undoItem = new("Отменить");
    private readonly ToolStripMenuItem redoItem = new("Повторить");
    private readonly ToolStripMenuItem gridItem = new("Пиксельная сетка");
    private readonly ToolTip tips = new();
    private string? projectPath;
    private bool updating;

    public MainForm(PixelDocument document, string? sourcePath, AppSettings settings, bool initiallyModified = false)
    {
        this.settings = settings;
        session = new EditorSession(document);
        if (IsProject(sourcePath)) projectPath = Path.GetFullPath(sourcePath!);
        if (initiallyModified || (sourcePath != null && !IsProject(sourcePath))) session.Load(document, false);
        canvas = new CanvasControl(session)
        {
            Dock = DockStyle.Fill, ShowGrid = settings.ShowGrid,
            PencilSize = settings.PencilSize, EraserSize = settings.EraserSize,
            DarkBackground = settings.Background == "Темный"
        };
        palette = new PaletteControl(session);
        Text = "PixelArt";
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(247, 246, 239);
        ClientSize = new Size(1180, 780);
        MinimumSize = new Size(860, 620);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        BuildInterface();
        session.Changed += (_, _) => UpdateInterface();
        canvas.ColorPicked += color => { canvas.PrimaryColor = color; UpdateColors(); };
        canvas.StatusChanged += text => cursorStatus.Text = text;
        canvas.ViewChanged += (_, _) => zoomStatus.Text = $"{canvas.ZoomPercent}%";
        palette.ColorSelected += (index, secondary) =>
        {
            if (secondary) canvas.SecondaryColor = session.Document.Palette[index];
            else canvas.PrimaryColor = session.Document.Palette[index];
            UpdateColors();
        };
        palette.ColorEditRequested += index => Run(() => EditPaletteColor(index));
        Shown += (_, _) => { canvas.FitToWindow(); canvas.Focus(); };
        Deactivate += (_, _) => { canvas.SetSpacePan(false); canvas.CancelGesture(); };
        FormClosing += OnClosing;
        UpdateInterface();
        SelectTool(EditorTool.Pencil);
    }

    private static bool IsProject(string? path) => path != null &&
        (Path.GetExtension(path).Equals(".pixelart", StringComparison.OrdinalIgnoreCase) ||
         Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase));

    private void BuildInterface()
    {
        var body = new Panel { Dock = DockStyle.Fill };
        var sidebar = new FlowLayoutPanel
        {
            Dock = DockStyle.Left, Width = 250, Padding = new Padding(14, 12, 8, 12),
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            BackColor = Color.FromArgb(247, 246, 239)
        };
        body.Controls.Add(canvas);
        body.Controls.Add(sidebar);
        Controls.Add(body);
        var status = new StatusStrip { BackColor = Color.FromArgb(247, 246, 239), SizingGrip = true };
        status.Items.AddRange([documentStatus, cursorStatus, zoomStatus]);
        Controls.Add(status);
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(8, 5, 8, 5),
            BackColor = Color.FromArgb(240, 239, 230), RenderMode = ToolStripRenderMode.System };
        AddToolButton(toolbar, "Новый", NewDocument);
        AddToolButton(toolbar, "Открыть", OpenDocument);
        AddToolButton(toolbar, "Сохранить", () => SaveProject(false));
        toolbar.Items.Add(new ToolStripSeparator());
        AddToolButton(toolbar, "Отмена", session.Undo);
        AddToolButton(toolbar, "Повтор", session.Redo);
        toolbar.Items.Add(new ToolStripSeparator());
        AddToolButton(toolbar, "−90°", () => Rotate(false));
        AddToolButton(toolbar, "+90°", () => Rotate(true));
        AddToolButton(toolbar, "Сдвиг", MoveImage);
        toolbar.Items.Add(new ToolStripSeparator());
        AddToolButton(toolbar, "Вписать", canvas.FitToWindow);
        AddToolButton(toolbar, "Экспорт PNG", ExportPng);
        Controls.Add(toolbar);
        var menu = BuildMenu();
        Controls.Add(menu);
        MainMenuStrip = menu;
        var header = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = Color.FromArgb(24, 65, 62) };
        header.Controls.Add(new Label { Text = "PIXEL / ART", Font = new Font("Consolas", 23, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 240, 214), AutoSize = true, Location = new Point(16, 12) });
        header.Controls.Add(new Label { Text = "Маленький холст. Большие идеи.", AutoSize = true,
            ForeColor = Color.FromArgb(183, 212, 196), Location = new Point(292, 24) });
        Controls.Add(header);

        AddHeading(sidebar, "01 / ИНСТРУМЕНТЫ");
        var toolPanel = new FlowLayoutPanel { Width = 218, Height = 98, Margin = new Padding(0, 6, 0, 10) };
        (EditorTool Tool, string Name)[] tools = [
            (EditorTool.Pencil, "Карандаш · B"), (EditorTool.Eraser, "Ластик · E"),
            (EditorTool.Fill, "Заливка · F"), (EditorTool.Picker, "Пипетка · I"), (EditorTool.Pan, "Рука · H")
        ];
        foreach (var entry in tools)
        {
            var button = new Button { Image = toolIcons[entry.Tool], Width = 68, Height = 44, FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 4, 4), TabStop = false, AccessibleName = entry.Name };
            button.FlatAppearance.BorderColor = Color.FromArgb(213, 218, 204);
            tips.SetToolTip(button, entry.Name);
            button.Click += (_, _) => { canvas.CancelGesture(); SelectTool(entry.Tool); canvas.Focus(); };
            toolButtons.Add(entry.Tool, button);
            toolPanel.Controls.Add(button);
        }
        var shapeStrip = new ToolStrip { AutoSize = false, Width = 68, Height = 44, Padding = Padding.Empty,
            Margin = new Padding(0, 0, 4, 4), GripStyle = ToolStripGripStyle.Hidden, CanOverflow = false,
            ImageScalingSize = new Size(28, 28), Dock = DockStyle.None, BackColor = Color.FromArgb(251, 250, 244) };
        shapesButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        shapesButton.AutoSize = false;
        shapesButton.Size = new Size(66, 42);
        shapesButton.AccessibleName = "Фигуры";
        shapesButton.ButtonClick += (_, _) => { canvas.CancelGesture(); SelectTool(selectedShape); canvas.Focus(); };
        (EditorTool Tool, string Name)[] shapes = [
            (EditorTool.Line, "Линия · L"), (EditorTool.Rectangle, "Прямоугольник · R"),
            (EditorTool.Ellipse, "Эллипс · O"), (EditorTool.Grid, "Сетка · T")
        ];
        foreach (var entry in shapes)
        {
            var item = new ToolStripMenuItem(entry.Name, toolIcons[entry.Tool]);
            item.Click += (_, _) => { canvas.CancelGesture(); SelectTool(entry.Tool); canvas.Focus(); };
            shapesButton.DropDownItems.Add(item);
            shapeItems.Add(entry.Tool, item);
        }
        shapeStrip.Items.Add(shapesButton);
        toolPanel.Controls.Add(shapeStrip);
        sidebar.Controls.Add(toolPanel);
        AddBrushSize(sidebar, "Карандаш, px", settings.PencilSize, "PencilSize", size =>
        {
            canvas.PencilSize = settings.PencilSize = size;
        });
        AddBrushSize(sidebar, "Ластик, px", settings.EraserSize, "EraserSize", size =>
        {
            canvas.EraserSize = settings.EraserSize = size;
        });
        var filled = new CheckBox { Text = "Закрашенные фигуры", AutoSize = true };
        filled.CheckedChanged += (_, _) => canvas.FilledShapes = filled.Checked;
        sidebar.Controls.Add(filled);
        var stepPanel = new FlowLayoutPanel { Width = 216, Height = 36, Margin = new Padding(0, 5, 0, 10) };
        stepPanel.Controls.Add(new Label { Text = "Шаг сетки:", AutoSize = true, Margin = new Padding(0, 5, 8, 0) });
        var step = new NumericUpDown { Minimum = 1, Maximum = 256, Value = 4, Width = 75 };
        step.ValueChanged += (_, _) => canvas.GridStep = (int)step.Value;
        stepPanel.Controls.Add(step);
        sidebar.Controls.Add(stepPanel);
        tips.SetToolTip(step, "Шаг инструмента «Сетка», который рисует линии на изображении.");

        AddHeading(sidebar, "02 / ЦВЕТ И ПАЛИТРА");
        colorLabel.Margin = new Padding(0, 8, 0, 8);
        sidebar.Controls.Add(colorLabel);
        var colorButtons = new FlowLayoutPanel { Width = 216, Height = 36, Margin = Padding.Empty };
        AddSmallButton(colorButtons, "Свой цвет", () => ChooseColor(false));
        AddSmallButton(colorButtons, "Обмен · X", SwapColors);
        sidebar.Controls.Add(colorButtons);
        var transparent = new Button { Text = "Прозрачный цвет", Width = 212, Height = 30, FlatStyle = FlatStyle.Flat };
        transparent.Click += (_, _) => { canvas.PrimaryColor = 0; UpdateColors(); };
        sidebar.Controls.Add(transparent);
        paletteSize.Items.AddRange(["2 цвета", "4 цвета", "16 цветов", "256 цветов"]);
        paletteSize.Margin = new Padding(0, 9, 0, 8);
        paletteSize.SelectedIndexChanged += (_, _) =>
        {
            if (updating || paletteSize.SelectedIndex < 0) return;
            int count = new[] { 2, 4, 16, 256 }[paletteSize.SelectedIndex];
            Run(() => session.Apply(doc => doc.SetPaletteSize(count)));
        };
        sidebar.Controls.Add(paletteSize);
        sidebar.Controls.Add(palette);
        sidebar.Controls.Add(new Label { Text = "ЛКМ — основной цвет\nПКМ — второй цвет\nДвойной щелчок — изменить цвет\n\nКолесо — масштаб\nПробел + ЛКМ / средняя — сдвиг вида\nF1 — вся справка", AutoSize = true,
            ForeColor = Color.FromArgb(93, 111, 103), Margin = new Padding(0, 12, 0, 20), Font = new Font("Segoe UI", 9) });
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip { BackColor = Color.FromArgb(247, 246, 239) };
        var file = new ToolStripMenuItem("Файл");
        AddMenu(file, "Новый…", Keys.Control | Keys.N, NewDocument);
        AddMenu(file, "Открыть / импортировать…", Keys.Control | Keys.O, OpenDocument);
        AddMenu(file, "Сохранить проект", Keys.Control | Keys.S, () => SaveProject(false));
        AddMenu(file, "Сохранить проект как…", Keys.Control | Keys.Shift | Keys.S, () => SaveProject(true));
        AddMenu(file, "Экспорт PNG…", Keys.Control | Keys.E, ExportPng);
        file.DropDownItems.Add(new ToolStripSeparator());
        AddMenu(file, "Выход", Keys.Alt | Keys.F4, Close);
        var edit = new ToolStripMenuItem("Правка");
        undoItem.ShortcutKeys = Keys.Control | Keys.Z;
        undoItem.Click += (_, _) => Run(session.Undo);
        redoItem.ShortcutKeys = Keys.Control | Keys.Y;
        redoItem.Click += (_, _) => Run(session.Redo);
        edit.DropDownItems.AddRange([undoItem, redoItem]);
        AddMenu(edit, "Копировать изображение", Keys.Control | Keys.C, CopyImage);
        AddMenu(edit, "Импорт из буфера…", Keys.Control | Keys.V, PasteImage);
        var image = new ToolStripMenuItem("Изображение");
        AddMenu(image, "Повернуть −90°", Keys.None, () => Rotate(false));
        AddMenu(image, "Повернуть +90°", Keys.None, () => Rotate(true));
        AddMenu(image, "Сдвинуть изображение…", Keys.None, MoveImage);
        AddMenu(image, "Очистить холст…", Keys.None, () =>
        {
            if (MessageBox.Show(this, "Очистить весь холст? Действие можно отменить.", "Очистка",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                session.Apply(doc => doc.SetPixels(new int[doc.Width * doc.Height]));
        });
        var view = new ToolStripMenuItem("Вид");
        gridItem.Checked = canvas.ShowGrid;
        gridItem.ShortcutKeys = Keys.Control | Keys.G;
        gridItem.Click += (_, _) => ToggleGrid();
        view.DropDownItems.Add(gridItem);
        AddMenu(view, "Вписать в окно", Keys.Control | Keys.D0, canvas.FitToWindow);
        AddMenu(view, "Увеличить", Keys.Control | Keys.Oemplus, () => canvas.ChangeZoom(1.25f));
        AddMenu(view, "Уменьшить", Keys.Control | Keys.OemMinus, () => canvas.ChangeZoom(0.8f));
        AddMenu(view, "Светлый / темный фон", Keys.None, () =>
        {
            canvas.DarkBackground = !canvas.DarkBackground;
            settings.Background = canvas.DarkBackground ? "Темный" : "Светлый";
            canvas.Invalidate();
        });
        var help = new ToolStripMenuItem("Справка");
        AddMenu(help, "Управление и командная строка", Keys.F1, ShowHelp);
        menu.Items.AddRange([file, edit, image, view, help]);
        return menu;
    }

    private void AddMenu(ToolStripMenuItem parent, string text, Keys shortcut, Action action)
    {
        var item = new ToolStripMenuItem(text) { ShortcutKeys = shortcut };
        item.Click += (_, _) => Run(action);
        parent.DropDownItems.Add(item);
    }

    private void AddToolButton(ToolStrip strip, string text, Action action)
    {
        var button = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        button.Click += (_, _) => Run(action);
        strip.Items.Add(button);
    }

    private void AddSmallButton(FlowLayoutPanel parent, string text, Action action)
    {
        var button = new Button { Text = text, Width = 103, Height = 30, FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 5, 0), Font = new Font("Segoe UI", 9) };
        button.Click += (_, _) => Run(action);
        parent.Controls.Add(button);
    }

    private void AddBrushSize(FlowLayoutPanel parent, string label, int value, string name, Action<int> changed)
    {
        var row = new FlowLayoutPanel { Width = 216, Height = 34, Margin = new Padding(0, 0, 0, 4) };
        row.Controls.Add(new Label { Text = label, Width = 130, Margin = new Padding(0, 5, 8, 0) });
        var input = new NumericUpDown { Name = name, Minimum = 1, Maximum = RasterDrawing.MaxBrushSize, Value = value, Width = 70 };
        input.ValueChanged += (_, _) => changed((int)input.Value);
        tips.SetToolTip(input, "Сторона квадратного отпечатка в пикселях; размер каждого инструмента независимый.");
        row.Controls.Add(input);
        parent.Controls.Add(row);
    }

    private static void AddHeading(FlowLayoutPanel parent, string text) =>
        parent.Controls.Add(new Label { Text = text, AutoSize = true, Font = new Font("Consolas", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(39, 103, 88), Margin = new Padding(0, 4, 0, 6) });

    private void Run(Action action)
    {
        canvas.CancelGesture();
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or System.Runtime.InteropServices.ExternalException or NotSupportedException or OutOfMemoryException)
        {
            string message = ex is OutOfMemoryException
                ? "Недостаточно памяти или изображение повреждено/не поддерживается."
                : ex.Message;
            MessageBox.Show(this, message, "Не удалось выполнить действие", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SelectTool(EditorTool tool)
    {
        canvas.Tool = tool;
        canvas.SetSpacePan(false);
        foreach (var pair in toolButtons)
        {
            pair.Value.BackColor = pair.Key == tool ? Color.FromArgb(203, 231, 216) : Color.FromArgb(251, 250, 244);
            pair.Value.FlatAppearance.BorderSize = pair.Key == tool ? 2 : 1;
        }
        if (shapeItems.ContainsKey(tool)) selectedShape = tool;
        shapesButton.Image = toolIcons[selectedShape];
        shapesButton.ToolTipText = $"Фигуры: {shapeItems[selectedShape].Text}. Стрелка — выбрать фигуру.";
        shapesButton.BackColor = shapeItems.ContainsKey(tool) ? Color.FromArgb(203, 231, 216) : Color.FromArgb(251, 250, 244);
        foreach (var pair in shapeItems) pair.Value.Checked = pair.Key == selectedShape;
    }

    private void UpdateInterface()
    {
        updating = true;
        int count = session.Document.Palette.Count;
        paletteSize.SelectedIndex = Array.IndexOf(new[] { 2, 4, 16, 256 }, count);
        updating = false;
        palette.UpdatePalette();
        UpdateColors();
        undoItem.Enabled = session.CanUndo;
        redoItem.Enabled = session.CanRedo;
        string name = projectPath == null ? "Без имени" : Path.GetFileName(projectPath);
        Text = $"{(session.IsDirty ? "* " : "")}{name} — PixelArt";
        documentStatus.Text = $"{session.Document.Width} × {session.Document.Height} px  |  {count} цветов";
    }

    private void UpdateColors()
    {
        palette.PrimaryColor = canvas.PrimaryColor;
        palette.SecondaryColor = canvas.SecondaryColor;
        palette.Invalidate();
        string Hex(int color) => (color >>> 24) == 0 ? "прозрачный" : $"#{color & 0xFFFFFF:X6}";
        colorLabel.Text = $"ЛКМ: {Hex(canvas.PrimaryColor)}\nПКМ: {Hex(canvas.SecondaryColor)}";
    }

    private void SwapColors()
    {
        (canvas.PrimaryColor, canvas.SecondaryColor) = (canvas.SecondaryColor, canvas.PrimaryColor);
        UpdateColors();
    }

    private void ChooseColor(bool secondary)
    {
        using var dialog = new ColorDialog { FullOpen = true,
            Color = Color.FromArgb(secondary ? canvas.SecondaryColor : canvas.PrimaryColor) };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (secondary) canvas.SecondaryColor = dialog.Color.ToArgb();
        else canvas.PrimaryColor = dialog.Color.ToArgb();
        UpdateColors();
    }

    private void EditPaletteColor(int index)
    {
        using var dialog = new ColorDialog { FullOpen = true, Color = Color.FromArgb(session.Document.Palette[index]) };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        int old = session.Document.Palette[index];
        int color = dialog.Color.ToArgb();
        session.Apply(doc => doc.Palette[index] = color);
        if (canvas.PrimaryColor == old) canvas.PrimaryColor = color;
        if (canvas.SecondaryColor == old) canvas.SecondaryColor = color;
        UpdateColors();
    }

    private bool ConfirmDiscard()
    {
        if (!session.IsDirty) return true;
        var result = MessageBox.Show(this, "Сохранить изменения в проекте?", "Несохраненные изменения",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return result == DialogResult.No || (result == DialogResult.Yes && SaveProject(false));
    }

    private void NewDocument()
    {
        using var dialog = new NumberDialog("Новый холст", "Ширина (px)", settings.NewWidth, 1, 256,
            "Высота (px)", settings.NewHeight);
        if (dialog.ShowDialog(this) != DialogResult.OK || !ConfirmDiscard()) return;
        settings.NewWidth = dialog.First;
        settings.NewHeight = dialog.Second;
        projectPath = null;
        session.Load(new PixelDocument(dialog.First, dialog.Second));
        canvas.FitToWindow();
    }

    private void OpenDocument()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Открыть проект или импортировать изображение",
            Filter = "Проекты и изображения|*.pixelart;*.json;*.png;*.jpg;*.jpeg;*.bmp;*.gif|Проект PixelArt|*.pixelart;*.json|Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK || !ConfirmDiscard()) return;
        var document = ImageFiles.Open(dialog.FileName);
        projectPath = IsProject(dialog.FileName) ? dialog.FileName : null;
        session.Load(document, IsProject(dialog.FileName));
        canvas.FitToWindow();
    }

    private bool SaveProject(bool saveAs)
    {
        string? path = projectPath;
        if (saveAs || path == null)
        {
            using var dialog = new SaveFileDialog { Filter = "Проект PixelArt|*.pixelart", DefaultExt = "pixelart",
                FileName = path == null ? "drawing.pixelart" : Path.GetFileName(path), AddExtension = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            path = dialog.FileName;
        }
        if (!IsProject(path)) throw new ArgumentException("Укажите расширение проекта .pixelart или .json.");
        new JsonDocumentStore().Save(session.Document, path);
        projectPath = path;
        session.MarkSaved();
        return true;
    }

    private void ExportPng()
    {
        using var scale = new NumberDialog("Экспорт PNG", "Размер пикселя", settings.ExportScale, 1, 16);
        if (scale.ShowDialog(this) != DialogResult.OK) return;
        using var dialog = new SaveFileDialog { Filter = "Изображение PNG|*.png", DefaultExt = "png", AddExtension = true,
            FileName = projectPath == null ? "drawing.png" : Path.GetFileNameWithoutExtension(projectPath) + ".png" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!Path.GetExtension(dialog.FileName).Equals(".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Для экспорта укажите расширение .png.");
        ImageFiles.ExportPng(session.Document, dialog.FileName, scale.First);
        settings.ExportScale = scale.First;
        cursorStatus.Text = $"PNG сохранен: {session.Document.Width * scale.First} × {session.Document.Height * scale.First}";
    }

    private void Rotate(bool clockwise)
    {
        session.Transform(doc => RasterDrawing.Rotate(doc, clockwise));
        canvas.FitToWindow();
    }

    private void MoveImage()
    {
        using var dialog = new NumberDialog("Сдвиг: края обрезаются, пустота прозрачная", "По X (вправо +)", 0, -256, 256,
            "По Y (вниз +)", 0);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            session.Transform(doc => RasterDrawing.Move(doc, dialog.First, dialog.Second));
    }

    private void CopyImage()
    {
        using var bitmap = ImageFiles.ToBitmap(session.Document);
        Clipboard.SetImage(bitmap);
        cursorStatus.Text = "Изображение скопировано";
    }

    private void PasteImage()
    {
        if (!Clipboard.ContainsImage())
        {
            MessageBox.Show(this, "В буфере обмена нет изображения.", "Импорт");
            return;
        }
        using var image = Clipboard.GetImage();
        if (image == null) return;
        var document = ImageFiles.Import(image);
        if (!ConfirmDiscard()) return;
        projectPath = null;
        session.Load(document, false);
        canvas.FitToWindow();
    }

    private void ToggleGrid()
    {
        canvas.ShowGrid = !canvas.ShowGrid;
        gridItem.Checked = canvas.ShowGrid;
        settings.ShowGrid = canvas.ShowGrid;
        canvas.Invalidate();
    }

    private void ShowHelp() => MessageBox.Show(this, CommandLine.HelpText + "\n\n" +
        "Инструменты: B карандаш, E ластик, F заливка, I пипетка, L линия, R прямоугольник, O эллипс, T сетка, H рука.\n" +
        "X — поменять цвета. G — показать/скрыть сетку. Esc — отменить текущий штрих.\n" +
        "ЛКМ / ПКМ — основной / второй цвет. Колесо — зум к указателю, пробел + ЛКМ или средняя кнопка — панорамирование.\n" +
        "Кнопки инструментов — с иконками и подсказками. Стрелка у фигур открывает список. Размер карандаша/ластика: 1–32 px.\n" +
        "Ctrl+N / O / S — новый / открыть / сохранить; Ctrl+Shift+S — сохранить как.\n" +
        "Ctrl+Z / Y — отменить / повторить; Ctrl+E — PNG; Ctrl+C / V — буфер обмена; Ctrl+0 — вписать.\n" +
        "Для окружности нарисуйте эллипс в квадрате. Импорт заменяет холст, большие картинки уменьшаются до 256×256.\n" +
        "Палитра не ограничивает цвета картинки. Ее изменение не перекрашивает готовые пиксели.\n" +
        "PNG не хранит палитру и историю. Для продолжения работы сохраняйте .pixelart.", "Справка PixelArt", MessageBoxButtons.OK, MessageBoxIcon.Information);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { canvas.SetSpacePan(false); canvas.CancelGesture(); return true; }
        // Буквенные инструменты не должны мешать вводу в числовые поля и списки.
        if (ActiveControl is not (NumericUpDown or ComboBox or TextBoxBase) && (keyData & Keys.Modifiers) == Keys.None)
        {
            if (keyData == Keys.Space) { canvas.Focus(); canvas.SetSpacePan(true); return true; }
            var tool = keyData switch
            {
                Keys.B => EditorTool.Pencil, Keys.E => EditorTool.Eraser, Keys.F => EditorTool.Fill,
                Keys.I => EditorTool.Picker, Keys.L => EditorTool.Line, Keys.R => EditorTool.Rectangle,
                Keys.O => EditorTool.Ellipse, Keys.T => EditorTool.Grid, Keys.H => EditorTool.Pan,
                _ => (EditorTool?)null
            };
            if (tool != null) { canvas.CancelGesture(); SelectTool(tool.Value); return true; }
            if (keyData == Keys.X) { SwapColors(); return true; }
            if (keyData == Keys.G) { ToggleGrid(); return true; }
        }
        if (keyData == (Keys.Control | Keys.Shift | Keys.Z)) { Run(session.Redo); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        canvas.CancelGesture();
        try
        {
            if (!ConfirmDiscard()) { e.Cancel = true; return; }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, "Ошибка сохранения", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            e.Cancel = true;
            return;
        }
        try { settings.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Не удалось сохранить настройки: " + ex.Message, "Настройки");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            tips.Dispose();
            foreach (var image in toolIcons.Values) image.Dispose();
        }
        base.Dispose(disposing);
    }
}
