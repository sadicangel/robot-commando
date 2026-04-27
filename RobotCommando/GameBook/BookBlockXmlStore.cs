using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public static class BookBlockXmlStore
{
    private static readonly XmlSerializer BlockSerializer = new(typeof(BookBlock));
    private static readonly XmlSerializer BookSerializer = new(typeof(BookDocument));
    private static readonly XmlSerializerNamespaces Namespaces = CreateNamespaces();

    public static BookBlock Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return (BookBlock)(BlockSerializer.Deserialize(stream) ?? throw new InvalidOperationException($"Failed to deserialize block '{path}'."));
    }

    public static BookDocument LoadBook(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return (BookDocument)(BookSerializer.Deserialize(stream) ?? throw new InvalidOperationException($"Failed to deserialize book '{path}'."));
    }

    public static void Save(string path, BookBlock block)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(block);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            NewLineHandling = NewLineHandling.Entitize,
        };

        using var writer = XmlWriter.Create(path, settings);
        BlockSerializer.Serialize(writer, block, Namespaces);
    }

    public static void SaveBook(string path, IEnumerable<BookBlock> blocks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(blocks);

        var document = new BookDocument();
        document.Blocks.AddRange(blocks.OrderBy(block => block.Id));

        SaveBook(path, document);
    }

    public static void SaveBook(string path, BookDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            NewLineHandling = NewLineHandling.Entitize,
        };

        using var writer = XmlWriter.Create(path, settings);
        BookSerializer.Serialize(writer, document, Namespaces);
    }

    private static XmlSerializerNamespaces CreateNamespaces()
    {
        XmlSerializerNamespaces namespaces = new();
        namespaces.Add(string.Empty, string.Empty);
        return namespaces;
    }
}
