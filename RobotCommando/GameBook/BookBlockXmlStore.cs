#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public static class BookBlockXmlStore
{
    private static readonly XmlSerializer Serializer = new(typeof(BookBlock));
    private static readonly XmlSerializerNamespaces Namespaces = CreateNamespaces();

    public static BookBlock Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return (BookBlock)(Serializer.Deserialize(stream) ?? throw new InvalidOperationException($"Failed to deserialize block '{path}'."));
    }

    public static IReadOnlyList<BookBlock> LoadDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Could not find block directory '{directoryPath}'.");
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.xml", SearchOption.TopDirectoryOnly)
            .Select(Load)
            .OrderBy(block => block.Id)
            .ToArray();
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
        Serializer.Serialize(writer, block, Namespaces);
    }

    private static XmlSerializerNamespaces CreateNamespaces()
    {
        XmlSerializerNamespaces namespaces = new();
        namespaces.Add(string.Empty, string.Empty);
        return namespaces;
    }
}
