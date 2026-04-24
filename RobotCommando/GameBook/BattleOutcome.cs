#nullable enable
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public sealed class BattleOutcome
{
    [XmlAttribute("win")]
    public int Win { get; set; }

    [XmlAttribute("lose")]
    public int Lose { get; set; }

    [XmlAttribute("escape")]
    public int Escape { get; set; }

    [XmlIgnore]
    public bool WinSpecified { get; set; }

    [XmlIgnore]
    public bool LoseSpecified { get; set; }

    [XmlIgnore]
    public bool EscapeSpecified { get; set; }
}
