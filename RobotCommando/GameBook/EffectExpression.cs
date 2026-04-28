namespace RobotCommando.GameBook;

public sealed class EffectExpression<TContext> : DslExpression<TContext>
{
    public void Execute(TContext context) => GameBookDsl.ExecuteEffect(Text, context);

    public static implicit operator EffectExpression<TContext>?(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : new EffectExpression<TContext> { Text = text };
}
