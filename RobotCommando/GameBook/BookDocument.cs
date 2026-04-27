using System.Xml.Serialization;

namespace RobotCommando.GameBook;

[XmlRoot("book")]
public sealed class BookDocument
{
    [XmlElement("block")]
    public List<BookBlock> Blocks { get; } = [];
}
