namespace PixelArt.Core;

public sealed class EditorSession
{
    public const int HistoryLimit = 100;
    private readonly List<PixelDocument> undo = new();
    private readonly List<PixelDocument> redo = new();
    private PixelDocument? beforeEdit;
    private PixelDocument? saved;

    public PixelDocument Document { get; private set; }
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public bool IsEditing => beforeEdit != null;
    public bool IsDirty => saved == null || !Document.ContentEquals(saved);
    public event EventHandler? Changed;

    public EditorSession(PixelDocument document)
    {
        Document = document;
        saved = document.Clone();
    }

    public void Load(PixelDocument document, bool markAsSaved = true)
    {
        Document = document;
        saved = markAsSaved ? document.Clone() : null;
        undo.Clear();
        redo.Clear();
        beforeEdit = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkSaved()
    {
        saved = Document.Clone();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Весь штрих мышью (от нажатия до отпускания) является одним действием.
    public void BeginEdit()
    {
        if (IsEditing) throw new InvalidOperationException("Редактирование уже начато.");
        beforeEdit = Document.Clone();
    }

    public void CommitEdit()
    {
        if (beforeEdit == null) return;
        if (!Document.ContentEquals(beforeEdit))
        {
            undo.Add(beforeEdit);
            if (undo.Count > HistoryLimit) undo.RemoveAt(0);
            redo.Clear();
        }
        beforeEdit = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void CancelEdit()
    {
        if (beforeEdit == null) return;
        Document = beforeEdit;
        beforeEdit = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Apply(Action<PixelDocument> action) => Transform(doc => { action(doc); return doc; });

    public void Transform(Func<PixelDocument, PixelDocument> action)
    {
        BeginEdit();
        try
        {
            Document = action(Document) ?? throw new InvalidOperationException("Нет документа.");
            CommitEdit();
        }
        catch
        {
            CancelEdit();
            throw;
        }
    }

    public void Undo()
    {
        if (IsEditing || !CanUndo) return;
        redo.Add(Document.Clone());
        Document = undo[^1];
        undo.RemoveAt(undo.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (IsEditing || !CanRedo) return;
        undo.Add(Document.Clone());
        Document = redo[^1];
        redo.RemoveAt(redo.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
