namespace PixelArt.Core;

// Другой интерфейс (веб, консоль и т. п.) может использовать то же хранилище.
public interface IDocumentStore
{
    PixelDocument Load(string path);
    void Save(PixelDocument document, string path);
}
