#nullable enable
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public abstract class BookEntity
{
    [XmlAttribute("tag")]
    public string Tag { get; set; } = string.Empty;

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("icon")]
    public string? Icon { get; set; }

    [XmlElement("description")]
    public string? Description { get; set; }

    public bool ShouldSerializeIcon() => !string.IsNullOrWhiteSpace(Icon);

    public bool ShouldSerializeDescription() => !string.IsNullOrWhiteSpace(Description);
}
