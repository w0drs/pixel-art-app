using PixelArt.Core;

// Простой тестовый раннер без пакетов NuGet. Любая ошибка дает код возврата 1.
int passed = 0, failed = 0;
string temporary = Path.Combine(Path.GetTempPath(), "PixelArt.Tests-" + Guid.NewGuid());
Directory.CreateDirectory(temporary);
try
{
    Test("Размеры холста проверяются", () =>
    {
        Throws<ArgumentOutOfRangeException>(() => new PixelDocument(0, 1));
        Throws<ArgumentOutOfRangeException>(() => new PixelDocument(257, 1));
        Throws<ArgumentOutOfRangeException>(() => new PixelDocument(1, -1));
        Equal(256, new PixelDocument(256, 256).Height);
    });
    Test("Новый холст прозрачный", () => Equal(0, new PixelDocument(2, 3).GetPixel(1, 2)));
    Test("Запись за границы обрезается", () =>
    {
        var doc = new PixelDocument(2, 2);
        doc.SetPixel(-1, 0, 7); doc.SetPixel(2, 0, 7);
        Equal(0, doc.CopyPixels().Sum());
        Throws<ArgumentOutOfRangeException>(() => doc.GetPixel(2, 0));
    });
    Test("Клонирование независимое", () =>
    {
        var doc = new PixelDocument(3, 2);
        var copy = doc.Clone();
        copy.SetPixel(0, 0, 5); copy.Palette[0] = 8;
        Equal(0, doc.GetPixel(0, 0));
        Check(doc.Palette[0] != copy.Palette[0]);
    });
    Test("Массив пикселей копируется", () =>
    {
        var doc = new PixelDocument(1, 1);
        var pixels = new[] { 3 }; doc.SetPixels(pixels); pixels[0] = 9;
        Equal(3, doc.GetPixel(0, 0));
        var exported = doc.CopyPixels(); exported[0] = 2;
        Equal(3, doc.GetPixel(0, 0));
        Throws<ArgumentException>(() => doc.SetPixels([1, 2]));
    });
    Test("Линия во всех восьми направлениях", () =>
    {
        (int X, int Y)[] ends = [(0, 0), (4, 0), (8, 0), (0, 4), (8, 4), (0, 8), (4, 8), (8, 8)];
        foreach (var end in ends)
        {
            var doc = new PixelDocument(9, 9);
            RasterDrawing.Line(doc, 4, 4, end.X, end.Y, 7);
            Equal(7, doc.GetPixel(4, 4)); Equal(7, doc.GetPixel(end.X, end.Y));
            Equal(5, doc.CopyPixels().Count(p => p == 7));
        }
    });
    Test("Квадратная кисть: четный и нечетный размер", () =>
    {
        foreach (int size in new[] { 1, 2, 3, 4, 32 })
        {
            var doc = new PixelDocument(64, 64);
            RasterDrawing.BrushStroke(doc, 32, 32, 32, 32, 7, size);
            Equal(size * size, doc.CopyPixels().Count(p => p == 7));
        }
    });
    Test("Широкий штрих без пропусков в обе стороны", () =>
    {
        var doc = new PixelDocument(20, 20);
        var reverse = doc.Clone();
        RasterDrawing.BrushStroke(doc, 3, 3, 16, 16, 7, 3);
        RasterDrawing.BrushStroke(reverse, 16, 16, 3, 3, 7, 3);
        Check(doc.ContentEquals(reverse));
        for (int i = 3; i <= 16; i++)
        for (int offset = -1; offset <= 1; offset++) Equal(7, doc.GetPixel(i + offset, i));
    });
    Test("Кисть обрезается по краям, ластик очищает", () =>
    {
        var doc = new PixelDocument(4, 4);
        RasterDrawing.BrushStroke(doc, 0, 0, 0, 0, 7, 3);
        Equal(4, doc.CopyPixels().Count(p => p == 7));
        RasterDrawing.BrushStroke(doc, 0, 0, 0, 0, 0, 3);
        Check(doc.CopyPixels().All(p => p == 0));
    });
    Test("Неверный размер кисти отклоняется", () =>
    {
        var doc = new PixelDocument(4, 4);
        Throws<ArgumentOutOfRangeException>(() => RasterDrawing.BrushStroke(doc, 0, 0, 1, 1, 7, 0));
        Throws<ArgumentOutOfRangeException>(() => RasterDrawing.BrushStroke(doc, 0, 0, 1, 1, 7, 33));
    });
    Test("Точка как линия", () =>
    {
        var doc = new PixelDocument(2, 2);
        RasterDrawing.Line(doc, 1, 1, 1, 1, 7);
        Equal(1, doc.CopyPixels().Count(p => p == 7));
    });
    Test("Прямоугольник в обратном направлении", () =>
    {
        var doc = new PixelDocument(5, 5);
        RasterDrawing.Rectangle(doc, 4, 4, 0, 0, 1, false);
        Equal(16, doc.CopyPixels().Count(p => p == 1)); Equal(0, doc.GetPixel(2, 2));
    });
    Test("Закрашенный прямоугольник", () =>
    {
        var doc = new PixelDocument(3, 2);
        RasterDrawing.Rectangle(doc, 0, 0, 2, 1, 7, true);
        Check(doc.CopyPixels().All(p => p == 7));
    });
    Test("Эллипс симметричен и замкнут", () =>
    {
        var doc = new PixelDocument(15, 11);
        RasterDrawing.Ellipse(doc, 1, 1, 13, 9, 1, false);
        for (int y = 0; y < doc.Height; y++)
        for (int x = 0; x < doc.Width; x++)
        {
            Equal(doc.GetPixel(x, y), doc.GetPixel(14 - x, y));
            Equal(doc.GetPixel(x, y), doc.GetPixel(x, 10 - y));
        }
        RasterDrawing.FloodFill(doc, 7, 5, 2);
        Equal(2, doc.GetPixel(7, 5)); Equal(0, doc.GetPixel(0, 0));
    });
    Test("Эллипс шириной в один пиксель", () =>
    {
        var doc = new PixelDocument(3, 8);
        RasterDrawing.Ellipse(doc, 1, 0, 1, 7, 7, false);
        Equal(8, doc.CopyPixels().Count(p => p == 7));
    });
    Test("Закрашенный эллипс", () =>
    {
        var doc = new PixelDocument(7, 7);
        RasterDrawing.Ellipse(doc, 0, 0, 6, 6, 4, true);
        Equal(4, doc.GetPixel(3, 3)); Equal(0, doc.GetPixel(0, 0));
    });
    Test("Заливка не пересекает границу", () =>
    {
        var doc = new PixelDocument(7, 7);
        RasterDrawing.Rectangle(doc, 1, 1, 5, 5, 1, false);
        RasterDrawing.FloodFill(doc, 3, 3, 2);
        Equal(9, doc.CopyPixels().Count(p => p == 2)); Equal(0, doc.GetPixel(0, 0));
    });
    Test("Большая заливка без рекурсии", () =>
    {
        var doc = new PixelDocument(256, 256);
        RasterDrawing.FloodFill(doc, 0, 0, 3);
        RasterDrawing.FloodFill(doc, 0, 0, 3);
        RasterDrawing.FloodFill(doc, -1, 0, 4);
        Check(doc.CopyPixels().All(p => p == 3));
    });
    Test("Нарисованная сетка", () =>
    {
        var doc = new PixelDocument(9, 9);
        RasterDrawing.Grid(doc, 8, 8, 0, 0, 5, 4);
        Equal(5, doc.GetPixel(4, 2)); Equal(0, doc.GetPixel(2, 2)); Equal(5, doc.GetPixel(8, 7));
        Throws<ArgumentOutOfRangeException>(() => RasterDrawing.Grid(doc, 0, 0, 8, 8, 5, 0));
    });
    Test("Поворот прямоугольного холста", () =>
    {
        var doc = new PixelDocument(3, 2); doc.SetPixels([1, 2, 3, 4, 5, 6]);
        var right = RasterDrawing.Rotate(doc, true);
        Equal(2, right.Width); Equal(3, right.Height);
        Check(right.CopyPixels().SequenceEqual(new[] { 4, 1, 5, 2, 6, 3 }));
        Check(RasterDrawing.Rotate(right, false).ContentEquals(doc));
        var four = doc;
        for (int i = 0; i < 4; i++) four = RasterDrawing.Rotate(four, true);
        Check(four.ContentEquals(doc));
    });
    Test("Сдвиг обрезает края, освобождает прозрачность", () =>
    {
        var doc = new PixelDocument(3, 2); doc.SetPixels([1, 2, 3, 4, 5, 6]);
        Check(RasterDrawing.Move(doc, 1, -1).CopyPixels().SequenceEqual(new[] { 0, 4, 5, 0, 0, 0 }));
        Check(RasterDrawing.Move(doc, 256, 0).CopyPixels().All(p => p == 0));
    });
    Test("Палитра сохраняет пользовательские цвета", () =>
    {
        var doc = new PixelDocument(1, 1); doc.Palette[0] = 42;
        foreach (int size in new[] { 2, 4, 16, 256 })
        { doc.SetPaletteSize(size); Equal(size, doc.Palette.Count); Equal(42, doc.Palette[0]); }
        Throws<ArgumentOutOfRangeException>(() => doc.SetPaletteSize(1));
    });
    Test("Штрих отменяется целиком и повторяется", () =>
    {
        var session = new EditorSession(new PixelDocument(3, 1));
        session.BeginEdit(); session.Document.SetPixel(0, 0, 1); session.Document.SetPixel(1, 0, 2); session.CommitEdit();
        Check(session.IsDirty); session.Undo(); Check(!session.IsDirty); Check(!session.CanUndo);
        session.Redo(); Equal(2, session.Document.GetPixel(1, 0));
    });
    Test("Нет изменений — нет лишней истории", () =>
    {
        var session = new EditorSession(new PixelDocument(1, 1));
        session.Apply(_ => { }); Check(!session.CanUndo); Check(!session.IsDirty);
    });
    Test("Отмена незавершенного штриха", () =>
    {
        var session = new EditorSession(new PixelDocument(1, 1));
        session.BeginEdit(); session.Document.SetPixel(0, 0, 9); session.CancelEdit();
        Equal(0, session.Document.GetPixel(0, 0)); Check(!session.CanUndo);
    });
    Test("Ошибка действия восстанавливает документ", () =>
    {
        var session = new EditorSession(new PixelDocument(1, 1));
        Throws<InvalidOperationException>(() => session.Apply(doc => { doc.SetPixel(0, 0, 3); throw new InvalidOperationException(); }));
        Equal(0, session.Document.GetPixel(0, 0)); Check(!session.IsEditing);
    });
    Test("Новое действие очищает повтор", () =>
    {
        var session = new EditorSession(new PixelDocument(1, 1));
        session.Apply(doc => doc.SetPixel(0, 0, 1)); session.Undo();
        session.Apply(doc => doc.SetPixel(0, 0, 2)); Check(!session.CanRedo);
    });
    Test("История ограничена 100 действиями", () =>
    {
        var session = new EditorSession(new PixelDocument(1, 1));
        for (int i = 1; i <= 120; i++) { int color = i; session.Apply(doc => doc.SetPixel(0, 0, color)); }
        for (int i = 0; i < 101; i++) session.Undo();
        Equal(20, session.Document.GetPixel(0, 0)); Check(!session.CanUndo);
    });
    Test("Сохраненное состояние узнается после Undo/Redo", () =>
    {
        var session = new EditorSession(new PixelDocument(1, 1));
        session.Apply(doc => doc.SetPixel(0, 0, 1)); session.MarkSaved();
        session.Apply(doc => doc.SetPixel(0, 0, 2)); session.Undo(); Check(!session.IsDirty);
        session.Redo(); Check(session.IsDirty);
    });
    Test("Поворот и палитра участвуют в Undo", () =>
    {
        var session = new EditorSession(new PixelDocument(2, 3));
        session.Apply(doc => doc.Palette[0] = 3);
        session.Transform(doc => RasterDrawing.Rotate(doc, true)); Equal(3, session.Document.Width);
        session.Undo(); Equal(2, session.Document.Width); Equal(3, session.Document.Palette[0]);
        session.Undo(); Check(!session.IsDirty);
    });
    Test("Загрузка очищает историю и отслеживает импорт", () =>
    {
        var session = new EditorSession(new PixelDocument(1, 1));
        session.Apply(doc => doc.SetPixel(0, 0, 1)); session.Load(new PixelDocument(2, 2), false);
        Check(!session.CanUndo); Check(session.IsDirty); session.MarkSaved(); Check(!session.IsDirty);
    });
    Test("Событие Changed вызывается при действии и Undo", () =>
    {
        var session = new EditorSession(new PixelDocument(1, 1)); int events = 0;
        session.Changed += (_, _) => events++;
        session.Apply(doc => doc.SetPixel(0, 0, 2)); session.Undo(); Equal(2, events);
    });
    Test("JSON: точное сохранение, прозрачность и перезапись", () =>
    {
        var doc = new PixelDocument(2, 3); doc.SetPixels([0, unchecked((int)0x80123456), -1, 4, 5, 6]);
        doc.Palette[0] = unchecked((int)0xFFABCDEF);
        IDocumentStore store = new JsonDocumentStore();
        string path = Path.Combine(temporary, "проект.pixelart");
        store.Save(doc, path); Check(store.Load(path).ContentEquals(doc));
        doc.SetPixel(0, 0, 9); store.Save(doc, path); Check(store.Load(path).ContentEquals(doc));
        Equal(1, Directory.GetFiles(temporary).Length);
    });
    Test("Библиотека: создание, одинаковые имена и повторное открытие", () =>
    {
        string root = Path.Combine(temporary, "library-create");
        var library = new ProjectLibrary(root);
        Equal(0, library.GetProjects().Count); Check(!Directory.Exists(root));
        var first = library.Create("Один проект", new PixelDocument(3, 4));
        var second = library.Create("Один проект", new PixelDocument(2, 2));
        Check(first.Id != second.Id);
        File.SetLastWriteTimeUtc(first.FilePath, DateTime.UtcNow.AddMinutes(-10));
        var reopened = new ProjectLibrary(root);
        Equal(2, reopened.GetProjects().Count); Equal(second.Id, reopened.GetProjects()[0].Id);
        Equal(3, reopened.Load(first.Id).Width);
        Equal(first.Id, reopened.FindByPath(first.FilePath)!.Id);
    });
    Test("Библиотека: сохранение и переименование без смены ID", () =>
    {
        var library = new ProjectLibrary(Path.Combine(temporary, "library-save"));
        var project = library.Create("Старое имя", new PixelDocument(2, 2));
        var doc = library.Load(project.Id); doc.SetPixel(1, 1, 7);
        library.Save(project.Id, doc); library.Rename(project.Id, "Новое имя / : допустимо");
        var renamed = library.GetProjects().Single();
        Equal(project.Id, renamed.Id); Equal(project.FilePath, renamed.FilePath);
        Equal("Новое имя / : допустимо", renamed.Name); Equal(7, library.Load(project.Id).GetPixel(1, 1));
    });
    Test("Библиотека: удаление не затрагивает другие проекты и внешние файлы", () =>
    {
        var library = new ProjectLibrary(Path.Combine(temporary, "library-delete"));
        var first = library.Create("Удалить", new PixelDocument(1, 1));
        var second = library.Create("Оставить", new PixelDocument(2, 2));
        string external = Path.Combine(temporary, "external.pixelart");
        new JsonDocumentStore().Save(new PixelDocument(3, 3), external);
        library.Delete(first.Id);
        Equal(second.Id, library.GetProjects().Single().Id); Check(File.Exists(external));
        Throws<FileNotFoundException>(() => library.Load(first.Id));
    });
    Test("Библиотека: проверка ID и названия", () =>
    {
        var library = new ProjectLibrary(Path.Combine(temporary, "library-validation"));
        foreach (string id in new[] { "..", "../outside", "", "not-a-guid" })
        {
            Throws<ArgumentException>(() => library.Load(id));
            Throws<ArgumentException>(() => library.Delete(id));
            Throws<ArgumentException>(() => library.Rename(id, "Имя"));
        }
        foreach (string name in new[] { "", "   ", "a\nb", new string('a', 81) })
            Throws<ArgumentException>(() => library.Create(name, new PixelDocument(1, 1)));
        Equal("Имя", ProjectLibrary.ValidateName("  Имя  "));
    });
    Test("Библиотека: поврежденный проект остается видимым и удаляемым", () =>
    {
        var library = new ProjectLibrary(Path.Combine(temporary, "library-corrupt"));
        var project = library.Create("Проект", new PixelDocument(1, 1));
        File.WriteAllText(project.FilePath, "{");
        File.Delete(Path.Combine(Path.GetDirectoryName(project.FilePath)!, "name.txt"));
        Equal("Без имени", library.GetProjects().Single().Name);
        Throws<InvalidDataException>(() => library.Load(project.Id));
        library.Delete(project.Id); Equal(0, library.GetProjects().Count);
    });
    Test("Библиотека: неудачная запись не повреждает сохранение", () =>
    {
        var library = new ProjectLibrary(Path.Combine(temporary, "library-error"));
        var project = library.Create("Исходный", new PixelDocument(1, 1));
        var invalid = new PixelDocument(1, 1); invalid.Palette.Clear();
        Throws<InvalidDataException>(() => library.Save(project.Id, invalid));
        Equal(16, library.Load(project.Id).Palette.Count);
        Throws<InvalidDataException>(() => library.Create("Не создан", invalid));
        Equal(1, library.GetProjects().Count); Equal(1, Directory.GetDirectories(library.RootPath).Length);
    });
    Test("Палитра: добавление цвета и повтор без дубликата", () =>
    {
        var doc = new PixelDocument(1, 1);
        int custom = unchecked((int)0xFF173DC7);
        Equal(16, doc.AddPaletteColor(custom)); Equal(17, doc.Palette.Count);
        Equal(16, doc.AddPaletteColor(custom)); Equal(17, doc.Palette.Count);
        Equal(17, doc.AddPaletteColor(0)); Equal(18, doc.Palette.Count);
    });
    Test("Палитра: лимит не мешает выбрать уже существующий цвет", () =>
    {
        var doc = new PixelDocument(1, 1);
        while (doc.Palette.Count < 256) doc.AddPaletteColor(doc.Palette.Count);
        Equal(0, doc.AddPaletteColor(doc.Palette[0]));
        Throws<InvalidOperationException>(() => doc.AddPaletteColor(-1));
        Equal(256, doc.Palette.Count);
    });
    Test("Палитра: пользовательские цвета, Undo и JSON", () =>
    {
        var session = new EditorSession(new PixelDocument(1, 1));
        int custom = unchecked((int)0x80123456);
        session.Apply(doc => doc.AddPaletteColor(custom));
        session.Apply(doc => doc.AddPaletteColor(custom));
        session.Undo(); Equal(16, session.Document.Palette.Count); Check(!session.CanUndo);
        session.Redo(); Equal(custom, session.Document.Palette[^1]);
        string path = Path.Combine(temporary, "custom-palette.pixelart");
        var store = new JsonDocumentStore(); store.Save(session.Document, path);
        Check(store.Load(path).ContentEquals(session.Document));
    });
    Test("Папки: поиск вложенных .pixelart и совместимых .json", () =>
    {
        string root = Path.Combine(temporary, "folder-scan"); Directory.CreateDirectory(Path.Combine(root, "nested"));
        var store = new JsonDocumentStore();
        store.Save(new PixelDocument(2, 3), Path.Combine(root, "first.pixelart"));
        store.Save(new PixelDocument(4, 5), Path.Combine(root, "nested", "second.json"));
        File.WriteAllText(Path.Combine(root, "settings.json"), "{\"theme\":\"light\"}");
        File.WriteAllText(Path.Combine(root, "invalid.json"), "{");
        File.WriteAllText(Path.Combine(root, "broken.pixelart"), "{");
        var library = new ProjectLibrary(root, true);
        var found = library.GetProjects(); Equal(3, found.Count);
        Check(found.All(project => project.Id.StartsWith("file:")));
        Equal(5, library.Load(found.Single(project => project.Name == "second").Id).Height);
        Check(library.FindByPath(Path.Combine(root, "settings.json")) == null);
        Equal(0, library.ScanWarnings.Count);
    });
    Test("Папки: сохранение отдельного файла на месте без копии", () =>
    {
        string root = Path.Combine(temporary, "folder-save"); Directory.CreateDirectory(root);
        string file = Path.Combine(root, "art.json"); new JsonDocumentStore().Save(new PixelDocument(2, 2), file);
        var library = new ProjectLibrary(root, true); var project = library.GetProjects().Single();
        var doc = library.Load(project.Id); doc.SetPixel(0, 0, 9); library.Save(project.Id, doc);
        Equal(9, new JsonDocumentStore().Load(file).GetPixel(0, 0));
        Equal(1, Directory.GetFiles(root).Length); Equal(0, Directory.GetDirectories(root).Length);
    });
    Test("Папки: переименование файла без перезаписи соседнего проекта", () =>
    {
        string root = Path.Combine(temporary, "folder-rename"); Directory.CreateDirectory(root);
        var store = new JsonDocumentStore();
        store.Save(new PixelDocument(1, 1), Path.Combine(root, "first.pixelart"));
        store.Save(new PixelDocument(2, 2), Path.Combine(root, "second.pixelart"));
        var library = new ProjectLibrary(root, true); var project = library.GetProjects().Single(p => p.Name == "first");
        Throws<IOException>(() => library.Rename(project.Id, "second"));
        Throws<ArgumentException>(() => library.Rename(project.Id, "../outside"));
        library.Rename(project.Id, "renamed");
        Check(!File.Exists(project.FilePath)); Check(File.Exists(Path.Combine(root, "renamed.pixelart")));
        Equal(2, store.Load(Path.Combine(root, "second.pixelart")).Width);
        Throws<FileNotFoundException>(() => library.Load(project.Id));
    });
    Test("Папки: удаление не трогает соседние файлы и корневую папку", () =>
    {
        string root = Path.Combine(temporary, "folder-delete"); Directory.CreateDirectory(root);
        var library = new ProjectLibrary(root, true);
        string flat = Path.Combine(root, "art.pixelart"); new JsonDocumentStore().Save(new PixelDocument(1, 1), flat);
        File.WriteAllText(Path.Combine(root, "notes.txt"), "keep");
        library.Delete(library.FindByPath(flat)!.Id); Check(File.Exists(Path.Combine(root, "notes.txt")));
        var managed = library.Create("Managed", new PixelDocument(1, 1));
        string managedFolder = Path.GetDirectoryName(managed.FilePath)!;
        File.WriteAllText(Path.Combine(managedFolder, "other.txt"), "keep too");
        var singleProjectFolder = new ProjectLibrary(managedFolder, true);
        singleProjectFolder.Delete(singleProjectFolder.GetProjects().Single().Id);
        Check(Directory.Exists(managedFolder)); Check(File.Exists(Path.Combine(managedFolder, "other.txt")));
        Check(!File.Exists(managed.FilePath));
    });
    Test("Папки: отключенный диск не создает пустую папку вместо исходной", () =>
    {
        string root = Path.Combine(temporary, "missing-drive"); var library = new ProjectLibrary(root, true);
        Throws<DirectoryNotFoundException>(() => library.GetProjects());
        Throws<DirectoryNotFoundException>(() => library.Create("Do not create", new PixelDocument(1, 1)));
        Check(!Directory.Exists(root));
        Equal(0, new ProjectLibrary(root).GetProjects().Count);
    });
    Test("Папки: нельзя обратиться к файлу за пределами выбранной папки", () =>
    {
        string root = Path.Combine(temporary, "folder-boundary"); Directory.CreateDirectory(root);
        string outside = Path.Combine(temporary, "outside.pixelart");
        new JsonDocumentStore().Save(new PixelDocument(1, 1), outside);
        var library = new ProjectLibrary(root, true);
        foreach (string id in new[] { "file:../outside.pixelart", "file:" + outside, "file:notes.txt" })
        {
            Throws<ArgumentException>(() => library.Load(id));
            Throws<ArgumentException>(() => library.Delete(id));
        }
        Check(File.Exists(outside)); Check(library.FindByPath(outside) == null);
    });
    Test("Папки: вложенная библиотека сохраняет имя и открывается без дублей", () =>
    {
        string root = Path.Combine(temporary, "nested-library"); Directory.CreateDirectory(root);
        var nested = new ProjectLibrary(Path.Combine(root, "inside"));
        var project = nested.Create("Вложенный проект", new PixelDocument(3, 3));
        var library = new ProjectLibrary(root, true);
        var found = library.GetProjects().Single(); Equal("Вложенный проект", found.Name);
        Check(found.Id.StartsWith("file:")); Equal(found.Id, library.FindByPath(project.FilePath)!.Id);
        library.Rename(found.Id, "Другое имя"); Equal("Другое имя", nested.GetProjects().Single().Name);
        var created = library.Create("Новый здесь", new PixelDocument(1, 1));
        Check(Path.GetDirectoryName(Path.GetDirectoryName(created.FilePath)!) == root);
        Equal(2, library.GetProjects().Count);
    });
    Test("Папки: повторное сканирование видит добавление и удаление файлов", () =>
    {
        string root = Path.Combine(temporary, "folder-refresh"); Directory.CreateDirectory(root);
        var library = new ProjectLibrary(root, true); Equal(0, library.GetProjects().Count);
        string file = Path.Combine(root, "new.pixelart"); new JsonDocumentStore().Save(new PixelDocument(1, 1), file);
        Equal(1, library.GetProjects().Count);
        Check(ProjectLibrary.SamePath(root, root + Path.DirectorySeparatorChar));
        File.Delete(file); Equal(0, library.GetProjects().Count);
    });
    Test("Smudge: перенос цвета на 100% без пропусков", () =>
    {
        int red = unchecked((int)0xFFFF0000), blue = unchecked((int)0xFF0000FF);
        var doc = new PixelDocument(8, 1); doc.SetPixels([red, blue, blue, blue, blue, blue, blue, blue]);
        var brush = new SmudgeBrush(doc, 0, 0, 1, 100); brush.MoveTo(7, 0);
        Check(doc.CopyPixels().All(color => color == red));
    });
    Test("Smudge: сила 50% смешивает и постепенно подхватывает новый цвет", () =>
    {
        int red = unchecked((int)0xFFFF0000), blue = unchecked((int)0xFF0000FF);
        var doc = new PixelDocument(4, 1); doc.SetPixels([red, blue, blue, blue]);
        new SmudgeBrush(doc, 0, 0, 1, 50).MoveTo(3, 0);
        Equal(red, doc.GetPixel(0, 0));
        Equal(unchecked((int)0xFF800080), doc.GetPixel(1, 0));
        Equal(unchecked((int)0xFF4000C0), doc.GetPixel(2, 0));
        Equal(unchecked((int)0xFF2000E0), doc.GetPixel(3, 0));
    });
    Test("Smudge: прозрачность без темных и скрытых цветных ореолов", () =>
    {
        var doc = new PixelDocument(2, 1); doc.SetPixels([unchecked((int)0x80FF0000), 0x0000FF00]);
        new SmudgeBrush(doc, 0, 0, 1, 50).MoveTo(1, 0);
        Equal(unchecked((int)0x40FF0000), doc.GetPixel(1, 0));
        doc.SetPixels([unchecked((int)0x80FF0000), unchecked((int)0x800000FF)]);
        new SmudgeBrush(doc, 0, 0, 1, 50).MoveTo(1, 0);
        Equal(unchecked((int)0x80800080), doc.GetPixel(1, 0));
    });
    Test("Smudge: пустая кисть не стирает цвет, а подхватывает его", () =>
    {
        int red = unchecked((int)0xFFFF0000);
        var doc = new PixelDocument(3, 1); doc.SetPixels([0, red, 0]);
        new SmudgeBrush(doc, 0, 0, 1, 50).MoveTo(2, 0);
        Equal(red, doc.GetPixel(1, 0)); Equal(unchecked((int)0x80FF0000), doc.GetPixel(2, 0));
        var empty = new PixelDocument(8, 8); var before = empty.Clone();
        new SmudgeBrush(empty, 0, 0, 8, 80).MoveTo(7, 7); Check(empty.ContentEquals(before));
    });
    Test("Smudge: щелчок, повтор той же точки и нулевая сила не меняют пиксели", () =>
    {
        var doc = new PixelDocument(3, 1); doc.SetPixels([-1, unchecked((int)0xFF0000FF), 0]);
        var before = doc.Clone(); var brush = new SmudgeBrush(doc, 0, 0, 1, 50);
        brush.MoveTo(0, 0); Check(doc.ContentEquals(before));
        brush.MoveTo(1, 0); var once = doc.Clone(); brush.MoveTo(1, 0); Check(doc.ContentEquals(once));
        new SmudgeBrush(doc, 0, 0, 3, 0).MoveTo(2, 0); Check(doc.ContentEquals(once));
    });
    Test("Smudge: результат не зависит от числа событий на том же пути", () =>
    {
        foreach (bool diagonal in new[] { false, true })
        {
            var direct = new PixelDocument(12, 12);
            for (int y = 0; y < 12; y++)
            for (int x = 0; x < 12; x++) direct.SetPixel(x, y, unchecked((int)0xFF000000) | x * 20 << 16 | y * 20);
            var stepped = direct.Clone();
            new SmudgeBrush(direct, 0, 0, 5, 70).MoveTo(11, diagonal ? 11 : 0);
            var brush = new SmudgeBrush(stepped, 0, 0, 5, 70);
            for (int i = 1; i <= 11; i++) brush.MoveTo(i, diagonal ? i : 0);
            Check(direct.ContentEquals(stepped));
        }
    });
    Test("Smudge: обрезка по краям и круглый отпечаток", () =>
    {
        int red = unchecked((int)0xFFFF0000);
        var small = new PixelDocument(2, 2); small.SetPixel(0, 0, red);
        new SmudgeBrush(small, 0, 0, 32, 100).MoveTo(1, 1);
        Equal(red, small.GetPixel(1, 1));
        var doc = new PixelDocument(24, 15); RasterDrawing.Rectangle(doc, 0, 0, 8, 14, red, true);
        new SmudgeBrush(doc, 4, 7, 9, 100).MoveTo(14, 7);
        Equal(red, doc.GetPixel(14, 7)); Equal(0, doc.GetPixel(18, 3));
    });
    Test("Smudge: неверные размеры, сила и координаты отклоняются", () =>
    {
        var doc = new PixelDocument(4, 4);
        Throws<ArgumentOutOfRangeException>(() => new SmudgeBrush(doc, 0, 0, 0, 80));
        Throws<ArgumentOutOfRangeException>(() => new SmudgeBrush(doc, 0, 0, 33, 80));
        Throws<ArgumentOutOfRangeException>(() => new SmudgeBrush(doc, 0, 0, 1, -1));
        Throws<ArgumentOutOfRangeException>(() => new SmudgeBrush(doc, 0, 0, 1, 101));
        Throws<ArgumentOutOfRangeException>(() => new SmudgeBrush(doc, -1, 0, 1, 80));
        var brush = new SmudgeBrush(doc, 0, 0, 1, 80);
        Throws<ArgumentOutOfRangeException>(() => brush.MoveTo(4, 0));
        brush.MoveTo(3, 3); Check(doc.CopyPixels().All(pixel => pixel == 0));
    });
    Test("Поврежденные проекты отвергаются", () =>
    {
        var store = new JsonDocumentStore(); string path = Path.Combine(temporary, "bad.pixelart");
        foreach (string json in new[] { "{", "null", "{}",
            "{\"Version\":2}",
            "{\"Version\":1,\"Width\":257,\"Height\":1,\"Pixels\":[],\"Palette\":[0,1]}",
            "{\"Version\":1,\"Width\":2,\"Height\":2,\"Pixels\":[0],\"Palette\":[0,1]}" })
        { File.WriteAllText(path, json); Throws<InvalidDataException>(() => store.Load(path)); }
    });
}
finally { Directory.Delete(temporary, true); }
Console.WriteLine($"\nPassed: {passed}; failed: {failed}");
return failed == 0 ? 0 : 1;

void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
}
void Check(bool condition) { if (!condition) throw new Exception("Условие не выполнено."); }
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Ожидалось {expected}, получено {actual}."); }
void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("Ожидалось исключение " + typeof(T).Name);
}
