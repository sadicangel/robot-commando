namespace RobotCommando.GameBook;

public sealed class ConditionExpression<TContext> : DslExpression<TContext>
{
    public bool Evaluate(TContext context) => GameBookDsl.EvaluateCondition(Text, context);

    public static implicit operator ConditionExpression<TContext>?(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : new ConditionExpression<TContext> { Text = text };
}
