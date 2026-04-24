#nullable enable
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public sealed class BookItem : BookEntity
{
    [XmlElement("onAcquire")]
    public ItemTrigger? OnAcquire { get; set; }

    [XmlElement("onDiscard")]
    public ItemTrigger? OnDiscard { get; set; }

    [XmlElement("onUse")]
    public ItemTrigger? OnUse { get; set; }

    public bool ShouldSerializeOnAcquire() => OnAcquire is not null;

    public bool ShouldSerializeOnDiscard() => OnDiscard is not null;

    public bool ShouldSerializeOnUse() => OnUse is not null;
}
