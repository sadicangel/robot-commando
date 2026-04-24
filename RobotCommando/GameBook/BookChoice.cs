#nullable enable
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public sealed class BookChoice
{
    [XmlAttribute("to")]
    public int To { get; set; }

    [XmlAttribute("showWhenDisabled")]
    public bool ShowWhenDisabled { get; set; }

    [XmlElement("text")]
    public string Text { get; set; } = string.Empty;

    [XmlElement("condition")]
    public ConditionExpression<GameState>? Condition { get; set; }

    [XmlElement("effect")]
    public EffectExpression<GameState>? Effect { get; set; }

    public bool ShouldSerializeShowWhenDisabled() => ShowWhenDisabled;

    public bool ShouldSerializeCondition() => Condition is not null;

    public bool ShouldSerializeEffect() => Effect is not null;
}
