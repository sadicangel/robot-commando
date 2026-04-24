#nullable enable
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public enum RobotFrame
{
    [XmlEnum("Unspecified")]
    Unspecified,

    [XmlEnum("Humanoid")]
    Humanoid,

    [XmlEnum("Dinosaur")]
    Dinosaur,
}
