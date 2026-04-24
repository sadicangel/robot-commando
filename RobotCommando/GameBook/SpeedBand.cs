#nullable enable
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public enum SpeedBand
{
    [XmlEnum("Static")]
    Static,

    [XmlEnum("Slow")]
    Slow,

    [XmlEnum("Average")]
    Average,

    [XmlEnum("Fast")]
    Fast,

    [XmlEnum("Very Fast")]
    VeryFast,

    [XmlEnum("Ultra Fast")]
    UltraFast,
}
