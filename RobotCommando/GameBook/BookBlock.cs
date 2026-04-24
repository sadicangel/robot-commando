#nullable enable
using System.Collections.Generic;
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

[XmlRoot("block")]
public sealed class BookBlock
{
    [XmlAttribute("id")]
    public int Id { get; set; }

    [XmlAttribute("location")]
    public WorldLocation Location { get; set; }

    [XmlElement("text")]
    public string Text { get; set; } = string.Empty;

    [XmlElement("revisitText")]
    public string? RevisitText { get; set; }

    [XmlArray("choices")]
    [XmlArrayItem("choice")]
    public List<BookChoice> Choices { get; } = [];

    [XmlArray("items")]
    [XmlArrayItem("item")]
    public List<BookItem> Items { get; } = [];

    [XmlArray("robots")]
    [XmlArrayItem("robot")]
    public List<BookRobot> Robots { get; } = [];

    [XmlArray("enemies")]
    [XmlArrayItem("enemy")]
    public List<BookEnemy> Enemies { get; } = [];

    [XmlArray("monsters")]
    [XmlArrayItem("monster")]
    public List<BookMonster> Monsters { get; } = [];

    [XmlArray("effects")]
    [XmlArrayItem("effect")]
    public List<EffectExpression<GameState>> Effects { get; } = [];

    public bool ShouldSerializeRevisitText() => !string.IsNullOrWhiteSpace(RevisitText);

    public bool ShouldSerializeChoices() => Choices.Count > 0;

    public bool ShouldSerializeItems() => Items.Count > 0;

    public bool ShouldSerializeRobots() => Robots.Count > 0;

    public bool ShouldSerializeEnemies() => Enemies.Count > 0;

    public bool ShouldSerializeMonsters() => Monsters.Count > 0;

    public bool ShouldSerializeEffects() => Effects.Count > 0;
}
