#nullable enable
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public sealed class ItemTrigger
{
    [XmlElement("condition")]
    public ConditionExpression<GameState>? Condition { get; set; }

    [XmlElement("effect")]
    public EffectExpression<GameState>? Effect { get; set; }

    public bool ShouldSerializeCondition() => Condition is not null;

    public bool ShouldSerializeEffect() => Effect is not null;
}
