#nullable enable
using System.Xml.Serialization;

namespace RobotCommando.GameBook;

public abstract class DslExpression<TContext>
{
    [XmlText]
    public string Text { get; set; } = string.Empty;

    public override string ToString() => Text;
}
