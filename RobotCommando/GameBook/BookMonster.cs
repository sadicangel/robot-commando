#nullable enable
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public sealed class BookMonster : BookEntity
{
    [XmlAttribute("frame")]
    public RobotFrame Frame { get; set; }

    [XmlAttribute("armor")]
    public int Armor { get; set; }

    [XmlAttribute("armorMax")]
    public int ArmorMax { get; set; }

    [XmlAttribute("skill")]
    public int Skill { get; set; }

    [XmlAttribute("skillMax")]
    public int SkillMax { get; set; }

    [XmlAttribute("speed")]
    public SpeedBand Speed { get; set; }

    [XmlAttribute("speedMax")]
    public SpeedBand SpeedMax { get; set; }

    [XmlElement("battleOutcome")]
    public BattleOutcome? BattleOutcome { get; set; }

    public bool ShouldSerializeBattleOutcome() => BattleOutcome is not null;
}
