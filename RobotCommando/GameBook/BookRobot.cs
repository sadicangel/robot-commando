#nullable enable
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public sealed class BookRobot : BookEntity
{
    [XmlAttribute("frame")]
    public RobotFrame Frame { get; set; }

    [XmlAttribute("armor")]
    public int Armor { get; set; }

    [XmlAttribute("armorMax")]
    public int ArmorMax { get; set; }

    [XmlAttribute("speed")]
    public SpeedBand Speed { get; set; }

    [XmlAttribute("speedMax")]
    public SpeedBand SpeedMax { get; set; }

    [XmlAttribute("combatBonus")]
    public int CombatBonus { get; set; }

    [XmlAttribute("combatBonusMax")]
    public int CombatBonusMax { get; set; }
}
