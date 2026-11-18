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
    private Control smudgeSizeRow = null!;
    private Control smudgeStrengthRow = null!;
    private readonly ToolStripMenuItem undoItem = new("Отменить");
    private readonly ToolStripMenuItem redoItem = new("Повторить");
    private readonly ToolStripMenuItem gridItem = new("Пиксельная сетка");
    private readonly ToolTip tips = new();
    private readonly ProjectLibrary library;
    private LibraryProject libraryProject;
    private PixelDocument lastDiskDocument;
    private readonly System.Windows.Forms.Timer autoSaveTimer = new() { Interval = 1500 };
    private readonly ToolStripStatusLabel saveStatus = new();
    private string? autoSaveError;

    public MainForm(ProjectLibrary library, LibraryProject project, AppSettings settings, PixelDocument? initialDocument = null)
    {
        this.library = library;
        libraryProject = project;
        this.settings = settings;
        session = new EditorSession(library.Load(project.Id));
        lastDiskDocument = session.Document.Clone();
        if (initialDocument != null && !initialDocument.ContentEquals(session.Document)) session.Load(initialDocument, false);
        canvas = new CanvasControl(session)
        {
            Dock = DockStyle.Fill, ShowGrid = settings.ShowGrid,
            PencilSize = settings.PencilSize, EraserSize = settings.EraserSize,
            SmudgeSize = settings.SmudgeSize, SmudgeStrength = settings.SmudgeStrength,
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
        session.Changed += (_, _) =>
        {
            autoSaveError = null;
            UpdateInterface();
            autoSaveTimer.Stop();
            if (session.IsDirty) autoSaveTimer.Start();
        };
        autoSaveTimer.Tick += (_, _) => AutoSave();
        if (session.IsDirty) autoSaveTimer.Start();
        canvas.ColorPicked += color =>
        {
            canvas.PrimaryColor = color;
            SelectTool(EditorTool.Pencil);
            UpdateColors();
        };
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
        status.ShowItemToolTips = true;
        status.Items.AddRange([documentStatus, saveStatus, cursorStatus, zoomStatus]);
        Controls.Add(status);
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(8, 5, 8, 5),
            BackColor = Color.FromArgb(240, 239, 230), RenderMode = ToolStripRenderMode.System };
        AddToolButton(toolbar, "← Главная", Close);
        toolbar.Items.Add(new ToolStripSeparator());
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

        AddHeading(sidebar, "01 / ИНСТРУМЕНТЫ");
        var toolPanel = new FlowLayoutPanel { Width = 218, Height = 98, Margin = new Padding(0, 6, 0, 10) };
        (EditorTool Tool, string Name)[] tools = [
            (EditorTool.Pencil, "Карандаш · B"), (EditorTool.Eraser, "Ластик · E"),
            (EditorTool.Smudge, "Смещение пятна · S"), (EditorTool.Fill, "Заливка · F"),
            (EditorTool.Picker, "Пипетка · I"), (EditorTool.Pan, "Рука · H")
        ];
        foreach (var entry in tools)
        {
            var button = new Button { Image = toolIcons[entry.Tool], Width = 50, Height = 44, FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 4, 4), TabStop = false, AccessibleName = entry.Name };
            button.FlatAppearance.BorderColor = Color.FromArgb(213, 218, 204);
            tips.SetToolTip(button, entry.Name);
            button.Click += (_, _) => { canvas.CancelGesture(); SelectTool(entry.Tool); canvas.Focus(); };
            toolButtons.Add(entry.Tool, button);
            toolPanel.Controls.Add(button);
        }
        var shapeStrip = new ToolStrip { AutoSize = false, Width = 50, Height = 44, Padding = Padding.Empty,
            Margin = new Padding(0, 0, 4, 4), GripStyle = ToolStripGripStyle.Hidden, CanOverflow = false,
            ImageScalingSize = new Size(28, 28), Dock = DockStyle.None, BackColor = Color.FromArgb(251, 250, 244) };
        shapesButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        shapesButton.AutoSize = false;
        shapesButton.Size = new Size(48, 42);
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
        smudgeSizeRow = AddBrushSize(sidebar, "Смещение, px", settings.SmudgeSize, "SmudgeSize", size =>
        {
            canvas.SmudgeSize = settings.SmudgeSize = size;
        }, hint: "Диаметр круглой кисти «Смещение пятна» в пикселях.");
        smudgeStrengthRow = AddBrushSize(sidebar, "Сила пятна, %", settings.SmudgeStrength, "SmudgeStrength", strength =>
        {
            canvas.SmudgeStrength = settings.SmudgeStrength = strength;
        }, minimum: 0, maximum: 100, hint: "0% — без изменений, 100% — сильный перенос подхваченных цветов. Основной цвет не используется.");
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
        var addColor = new Button { Text = "Добавить цвет…", Name = "AddPaletteColor", Width = 212, Height = 30,
            FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 9, 0, 4) };
        addColor.Click += (_, _) => Run(ChoosePaletteColor);
        sidebar.Controls.Add(addColor);
        var addCurrent = new Button { Text = "+ Текущий цвет в палитру", Name = "AddCurrentPaletteColor", Width = 212, Height = 30,
            FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 0, 8) };
        addCurrent.Click += (_, _) => Run(() => AddPaletteColor(canvas.PrimaryColor));
        tips.SetToolTip(addCurrent, "Добавить основной цвет, в том числе выбранный пипеткой или прозрачный. Палитра хранится в проекте.");
        sidebar.Controls.Add(addCurrent);
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
        AddMenu(file, "Сохранить копию проекта…", Keys.Control | Keys.Shift | Keys.S, () => SaveProject(true));
        AddMenu(file, "Экспорт PNG…", Keys.Control | Keys.E, ExportPng);
        file.DropDownItems.Add(new ToolStripSeparator());
        AddMenu(file, "На главный экран", Keys.Control | Keys.Home, Close);
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

    private Control AddBrushSize(FlowLayoutPanel parent, string label, int value, string name, Action<int> changed,
        int minimum = 1, int maximum = RasterDrawing.MaxBrushSize, string? hint = null)
    {
        var row = new FlowLayoutPanel { Width = 216, Height = 34, Margin = new Padding(0, 0, 0, 4) };
        row.Controls.Add(new Label { Text = label, Width = 130, Margin = new Padding(0, 5, 8, 0) });
        var input = new NumericUpDown { Name = name, Minimum = minimum, Maximum = maximum, Value = Math.Clamp(value, minimum, maximum), Width = 70 };
        input.ValueChanged += (_, _) => changed((int)input.Value);
        tips.SetToolTip(input, hint ?? "Сторона квадратного отпечатка в пикселях; размер каждого инструмента независимый.");
        row.Controls.Add(input);
        parent.Controls.Add(row);
        return row;
    }

    private static void AddHeading(FlowLayoutPanel parent, string text) =>
        parent.Controls.Add(new Label { Text = text, AutoSize = true, Font = new Font("Consolas", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(39, 103, 88), Margin = new Padding(0, 4, 0, 6) });

    private void Run(Action action)
    {
        canvas.CancelGesture();
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
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
        smudgeSizeRow.Visible = smudgeStrengthRow.Visible = tool == EditorTool.Smudge;
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
        int count = session.Document.Palette.Count;
        palette.UpdatePalette();
        UpdateColors();
        undoItem.Enabled = session.CanUndo;
        redoItem.Enabled = session.CanRedo;
        Text = $"{(session.IsDirty ? "* " : "")}{libraryProject.Name} — Редактор";
        saveStatus.Text = autoSaveError != null ? "Ошибка сохранения" : session.IsDirty ? "Ожидает сохранения…" : "Сохранено";
        saveStatus.ForeColor = autoSaveError != null ? Color.Firebrick : Color.FromArgb(39, 103, 88);
        saveStatus.ToolTipText = autoSaveError ?? "Автосохранение в библиотеку. Ctrl+S — сохранить сейчас.";
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

    private void ChoosePaletteColor()
    {
        using var dialog = new ColorDialog { FullOpen = true, Color = Color.FromArgb(canvas.PrimaryColor) };
        if (dialog.ShowDialog(this) == DialogResult.OK) AddPaletteColor(dialog.Color.ToArgb());
    }

    private void AddPaletteColor(int color)
    {
        session.Apply(doc => doc.AddPaletteColor(color));
        canvas.PrimaryColor = color;
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

    private void SaveBeforeLeaving()
    {
        canvas.CancelGesture();
        if (session.IsDirty) SaveProject(false);
    }

    private void NewDocument()
    {
        using var dialog = new ProjectDialog("Новый проект", "Новый проект", settings.NewWidth, settings.NewHeight);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        SaveBeforeLeaving();
        settings.NewWidth = dialog.WidthPixels;
        settings.NewHeight = dialog.HeightPixels;
        ReplaceProject(new PixelDocument(dialog.WidthPixels, dialog.HeightPixels), dialog.ProjectName);
    }

    private void OpenDocument()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Импортировать изображение или проект в библиотеку",
            Filter = "Проекты и изображения|*.pixelart;*.json;*.png;*.jpg;*.jpeg;*.bmp;*.gif"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        SaveBeforeLeaving();
        var document = ImageFiles.Open(dialog.FileName);
        string name = new string(Path.GetFileNameWithoutExtension(dialog.FileName).Where(c => !char.IsControl(c)).Take(80).ToArray()).Trim();
        ReplaceProject(document, name.Length == 0 ? "Импортированный проект" : name);
    }

    private void ReplaceProject(PixelDocument document, string name)
    {
        var next = library.Create(name, document);
        autoSaveTimer.Stop();
        libraryProject = next;
        lastDiskDocument = document.Clone();
        autoSaveError = null;
        session.Load(document);
        canvas.FitToWindow();
        canvas.Focus();
    }

    public void SaveCurrentProject()
    {
        if (session.IsEditing) throw new InvalidOperationException("Сначала завершите штрих.");
        // Синхронизатор или другой редактор мог изменить файл после его открытия.
        if (!library.Load(libraryProject.Id).ContentEquals(lastDiskDocument))
            throw new IOException("Проект на диске изменен другим приложением. Автозапись остановлена. Сохраните свою копию через Ctrl+Shift+S и откройте актуальный проект заново.");
        library.Save(libraryProject.Id, session.Document);
        lastDiskDocument = session.Document.Clone();
        autoSaveTimer.Stop();
        autoSaveError = null;
        session.MarkSaved();
    }

    private bool SaveProject(bool saveCopy)
    {
        if (!saveCopy) { SaveCurrentProject(); return true; }
        using var dialog = new SaveFileDialog { Filter = "Проект PixelArt|*.pixelart", DefaultExt = "pixelart",
            FileName = ExportName() + ".pixelart", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        if (!IsProject(dialog.FileName)) throw new ArgumentException("Укажите расширение проекта .pixelart или .json.");
        // Внешняя копия не меняет ID/путь библиотечного проекта и не подменяет автосохранение.
        new JsonDocumentStore().Save(session.Document, dialog.FileName);
        cursorStatus.Text = "Копия проекта сохранена";
        return true;
    }

    private string ExportName() => string.Join("_", libraryProject.Name.Split(Path.GetInvalidFileNameChars()));

    private void AutoSave()
    {
        autoSaveTimer.Stop();
        if (!session.IsDirty || session.IsEditing) return;
        try { SaveCurrentProject(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            autoSaveError = ex.Message;
            UpdateInterface();
        }
    }

    private void ExportPng()
    {
        using var scale = new NumberDialog("Экспорт PNG", "Размер пикселя", settings.ExportScale, 1, 16);
        if (scale.ShowDialog(this) != DialogResult.OK) return;
        using var dialog = new SaveFileDialog { Filter = "Изображение PNG|*.png", DefaultExt = "png", AddExtension = true,
            FileName = ExportName() + ".png" };
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
        SaveBeforeLeaving();
        ReplaceProject(document, "Из буфера обмена");
    }

    private void ToggleGrid()
    {
        canvas.ShowGrid = !canvas.ShowGrid;
        gridItem.Checked = canvas.ShowGrid;
        settings.ShowGrid = canvas.ShowGrid;
        canvas.Invalidate();
    }

    private void ShowHelp() => MessageBox.Show(this, CommandLine.HelpText + "\n\n" +
        "Инструменты: B карандаш, E ластик, S смещение пятна, F заливка, I пипетка, L линия, R прямоугольник, O эллипс, T сетка, H рука.\n" +
        "X — поменять цвета. G — показать/скрыть сетку. Esc — отменить текущий штрих.\n" +
        "ЛКМ / ПКМ — основной / второй цвет. Колесо — зум к указателю, пробел + ЛКМ или средняя кнопка — панорамирование.\n" +
        "Кнопки инструментов — с иконками и подсказками. Стрелка у фигур открывает список. Размер карандаша/ластика: 1–32 px.\n" +
        "Ctrl+N / O / S — новый / открыть / сохранить; Ctrl+Shift+S — сохранить внешнюю копию.\n" +
        "Ctrl+Z / Y — отменить / повторить; Ctrl+E — PNG; Ctrl+C / V — буфер обмена; Ctrl+0 — вписать.\n" +
        "Для окружности нарисуйте эллипс в квадрате. Импорт создает отдельный проект, картинки уменьшаются до 256×256.\n" +
        "Смещение пятна переносит цвета с холста. При выборе S появляются размер 1–32 px и сила 0–100%.\n" +
        "После пипетки включается карандаш. «Добавить цвет» и «+ Текущий цвет» дополняют палитру проекта.\n" +
        "Палитра сохраняется в проекте. Ее изменение не перекрашивает готовые пиксели.\n" +
        "Изменения автоматически сохраняются в библиотеку. «Главная» или Ctrl+Home — вернуться к проектам.\n" +
        "PNG не хранит палитру и историю. Ctrl+Shift+S — отдельная копия .pixelart.", "Справка PixelArt", MessageBoxButtons.OK, MessageBoxIcon.Information);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { canvas.SetSpacePan(false); canvas.CancelGesture(); return true; }
        // Буквенные инструменты не должны мешать вводу в числовые поля и списки.
        if (ActiveControl is not (NumericUpDown or ComboBox or TextBoxBase) && (keyData & Keys.Modifiers) == Keys.None)
        {
            if (keyData == Keys.Space) { canvas.Focus(); canvas.SetSpacePan(true); return true; }
            var tool = keyData switch
            {
                Keys.B => EditorTool.Pencil, Keys.E => EditorTool.Eraser, Keys.S => EditorTool.Smudge, Keys.F => EditorTool.Fill,
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
            autoSaveTimer.Stop();
            SaveBeforeLeaving();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            var result = MessageBox.Show(this, "Не удалось сохранить проект: " + ex.Message +
                "\n\nНажмите «Нет», чтобы остаться в редакторе. Ctrl+Shift+S позволяет сохранить внешнюю копию.\nЗакрыть редактор БЕЗ сохранения?",
                "Ошибка сохранения", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes) { e.Cancel = true; return; }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            autoSaveTimer.Dispose();
            tips.Dispose();
            foreach (var image in toolIcons.Values) image.Dispose();
        }
        base.Dispose(disposing);
    }
}
