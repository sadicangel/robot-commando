#nullable enable
namespace RobotCommando.GameBook;

public sealed class EffectExpression<TContext> : DslExpression<TContext>
{
    public static implicit operator EffectExpression<TContext>?(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : new() { Text = text };
}
