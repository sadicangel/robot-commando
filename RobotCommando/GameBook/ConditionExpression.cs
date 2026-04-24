namespace RobotCommando.GameBook;

public sealed class ConditionExpression<TContext> : DslExpression<TContext>
{
    public static implicit operator ConditionExpression<TContext>?(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : new ConditionExpression<TContext> { Text = text };
}
