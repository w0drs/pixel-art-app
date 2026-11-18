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
