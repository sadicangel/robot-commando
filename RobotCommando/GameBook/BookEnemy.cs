#nullable enable
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public sealed class BookEnemy : BookEntity
{
    [XmlAttribute("stamina")]
    public int Stamina { get; set; }

    [XmlAttribute("staminaMax")]
    public int StaminaMax { get; set; }

    [XmlAttribute("skill")]
    public int Skill { get; set; }

    [XmlAttribute("skillMax")]
    public int SkillMax { get; set; }

    [XmlElement("battleOutcome")]
    public BattleOutcome? BattleOutcome { get; set; }

    public bool ShouldSerializeBattleOutcome() => BattleOutcome is not null;
}
