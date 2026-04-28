using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace RobotCommando.GameBook;

public static class GameBookDsl
{
    private static readonly ConcurrentDictionary<string, ExpressionNode> ConditionCache = [];
    private static readonly ConcurrentDictionary<string, EffectStatement> EffectCache = [];

    public static bool EvaluateCondition<TContext>(string text, TContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var expression = ConditionCache.GetOrAdd(text, static value => ParseLambdaExpression(value, "bool"));
        var result = expression.Evaluate(new EvaluationScope(context));

        return result is bool boolean
            ? boolean
            : throw new InvalidOperationException($"Condition DSL must return bool: '{text}'.");
    }

    public static void ExecuteEffect<TContext>(string text, TContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var statement = EffectCache.GetOrAdd(text, static value => ParseLambdaEffect(value));
        statement.Execute(new EvaluationScope(context));
    }

    private static ExpressionNode ParseLambdaExpression(string text, string expectedReturnType)
    {
        var body = GetExpressionBody(text, expectedReturnType);
        var parser = new Parser(body);
        return parser.ParseExpressionDocument();
    }

    private static EffectStatement ParseLambdaEffect(string text)
    {
        var body = GetExpressionBody(text, "void");
        var parser = new Parser(body);
        return parser.ParseEffectDocument();
    }

    private static string GetExpressionBody(string text, string expectedReturnType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var arrowIndex = text.IndexOf("=>", StringComparison.Ordinal);
        if (arrowIndex < 0)
        {
            return text.Trim();
        }

        var parameter = text[..arrowIndex].Trim();
        if (!string.Equals(parameter, "context", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"DSL parameter must be named 'context': '{text}'.");
        }

        return text[(arrowIndex + 2)..].Trim();
    }

    private sealed class Parser
    {
        private readonly Token[] _tokens;
        private int _position;

        public Parser(string text)
        {
            _tokens = Tokenize(text).ToArray();
        }

        public ExpressionNode ParseExpressionDocument()
        {
            var expression = ParseExpression();
            Expect(TokenKind.End);
            return expression;
        }

        public EffectStatement ParseEffectDocument()
        {
            var statement = ParseEffect();
            Expect(TokenKind.End);
            return statement;
        }

        private EffectStatement ParseEffect()
        {
            var target = ParseExpression();

            if (Match(TokenKind.PlusPlus))
            {
                return new CompoundAssignmentStatement(target, new LiteralNode(1), BinaryOperator.Add);
            }

            if (Match(TokenKind.MinusMinus))
            {
                return new CompoundAssignmentStatement(target, new LiteralNode(1), BinaryOperator.Subtract);
            }

            if (Match(TokenKind.Equals))
            {
                return new AssignmentStatement(target, ParseExpression());
            }

            if (Match(TokenKind.PlusEquals))
            {
                return new CompoundAssignmentStatement(target, ParseExpression(), BinaryOperator.Add);
            }

            if (Match(TokenKind.MinusEquals))
            {
                return new CompoundAssignmentStatement(target, ParseExpression(), BinaryOperator.Subtract);
            }

            return new ExpressionEffectStatement(target);
        }

        private ExpressionNode ParseExpression() => ParseConditional();

        private ExpressionNode ParseConditional()
        {
            var condition = ParseOr();
            if (!Match(TokenKind.Question))
            {
                return condition;
            }

            var whenTrue = ParseExpression();
            Expect(TokenKind.Colon);
            var whenFalse = ParseExpression();
            return new ConditionalNode(condition, whenTrue, whenFalse);
        }

        private ExpressionNode ParseOr()
        {
            var left = ParseAnd();
            while (Match(TokenKind.BarBar))
            {
                left = new BinaryNode(left, ParseAnd(), BinaryOperator.Or);
            }

            return left;
        }

        private ExpressionNode ParseAnd()
        {
            var left = ParseEquality();
            while (Match(TokenKind.AmpAmp))
            {
                left = new BinaryNode(left, ParseEquality(), BinaryOperator.And);
            }

            return left;
        }

        private ExpressionNode ParseEquality()
        {
            var left = ParseRelational();
            while (true)
            {
                if (Match(TokenKind.EqualsEquals))
                {
                    left = new BinaryNode(left, ParseRelational(), BinaryOperator.Equal);
                    continue;
                }

                if (Match(TokenKind.BangEquals))
                {
                    left = new BinaryNode(left, ParseRelational(), BinaryOperator.NotEqual);
                    continue;
                }

                if (MatchIdentifier("is"))
                {
                    var negated = MatchIdentifier("not");
                    Expect(TokenKind.Null);
                    left = new IsNullNode(left, negated);
                    continue;
                }

                return left;
            }
        }

        private ExpressionNode ParseRelational()
        {
            var left = ParseAdditive();
            while (true)
            {
                if (Match(TokenKind.Greater))
                {
                    left = new BinaryNode(left, ParseAdditive(), BinaryOperator.Greater);
                    continue;
                }

                if (Match(TokenKind.GreaterEquals))
                {
                    left = new BinaryNode(left, ParseAdditive(), BinaryOperator.GreaterOrEqual);
                    continue;
                }

                if (Match(TokenKind.Less))
                {
                    left = new BinaryNode(left, ParseAdditive(), BinaryOperator.Less);
                    continue;
                }

                if (Match(TokenKind.LessEquals))
                {
                    left = new BinaryNode(left, ParseAdditive(), BinaryOperator.LessOrEqual);
                    continue;
                }

                return left;
            }
        }

        private ExpressionNode ParseAdditive()
        {
            var left = ParseUnary();
            while (true)
            {
                if (Match(TokenKind.Plus))
                {
                    left = new BinaryNode(left, ParseUnary(), BinaryOperator.Add);
                    continue;
                }

                if (Match(TokenKind.Minus))
                {
                    left = new BinaryNode(left, ParseUnary(), BinaryOperator.Subtract);
                    continue;
                }

                return left;
            }
        }

        private ExpressionNode ParseUnary()
        {
            if (Match(TokenKind.Bang))
            {
                return new UnaryNode(ParseUnary(), UnaryOperator.Not);
            }

            if (Match(TokenKind.Minus))
            {
                return new UnaryNode(ParseUnary(), UnaryOperator.Negate);
            }

            return ParsePostfix();
        }

        private ExpressionNode ParsePostfix()
        {
            var expression = ParsePrimary();

            while (true)
            {
                if (Match(TokenKind.Dot))
                {
                    var memberName = Expect(TokenKind.Identifier).Text;
                    if (Match(TokenKind.OpenParen))
                    {
                        if (string.Equals(memberName, "Any", StringComparison.Ordinal))
                        {
                            expression = ParseAnyCall(expression);
                        }
                        else
                        {
                            expression = new MethodCallNode(expression, memberName, ParseArgumentsAfterOpenParen());
                        }
                    }
                    else
                    {
                        expression = new MemberAccessNode(expression, memberName);
                    }

                    continue;
                }

                if (Match(TokenKind.OpenBracket))
                {
                    var index = ParseExpression();
                    Expect(TokenKind.CloseBracket);
                    expression = new IndexAccessNode(expression, index);
                    continue;
                }

                return expression;
            }
        }

        private ExpressionNode ParseAnyCall(ExpressionNode source)
        {
            var parameter = Expect(TokenKind.Identifier).Text;
            Expect(TokenKind.Arrow);
            var predicate = ParseExpression();
            Expect(TokenKind.CloseParen);
            return new AnyNode(source, parameter, predicate);
        }

        private List<ExpressionNode> ParseArgumentsAfterOpenParen()
        {
            var arguments = new List<ExpressionNode>();
            if (Match(TokenKind.CloseParen))
            {
                return arguments;
            }

            do
            {
                arguments.Add(ParseExpression());
            }
            while (Match(TokenKind.Comma));

            Expect(TokenKind.CloseParen);
            return arguments;
        }

        private ExpressionNode ParsePrimary()
        {
            if (Match(TokenKind.OpenParen))
            {
                var expression = ParseExpression();
                Expect(TokenKind.CloseParen);
                return expression;
            }

            if (Match(TokenKind.True))
            {
                return new LiteralNode(true);
            }

            if (Match(TokenKind.False))
            {
                return new LiteralNode(false);
            }

            if (Match(TokenKind.Null))
            {
                return new LiteralNode(null);
            }

            if (Current.Kind == TokenKind.Number)
            {
                return new LiteralNode(int.Parse(Advance().Text, CultureInfo.InvariantCulture));
            }

            if (Current.Kind == TokenKind.String)
            {
                return new LiteralNode(Advance().Text);
            }

            if (Current.Kind == TokenKind.Identifier)
            {
                return new IdentifierNode(Advance().Text);
            }

            throw Error("Expected an expression.");
        }

        private bool Match(TokenKind kind)
        {
            if (Current.Kind != kind)
            {
                return false;
            }

            Advance();
            return true;
        }

        private bool MatchIdentifier(string text)
        {
            if (Current.Kind != TokenKind.Identifier || !string.Equals(Current.Text, text, StringComparison.Ordinal))
            {
                return false;
            }

            Advance();
            return true;
        }

        private Token Expect(TokenKind kind)
        {
            if (Current.Kind == kind)
            {
                return Advance();
            }

            throw Error($"Expected {kind}.");
        }

        private Token Advance() => _tokens[_position++];

        private Token Current => _tokens[_position];

        private InvalidOperationException Error(string message)
            => new($"{message} Near token '{Current.Text}'.");
    }

    private static IEnumerable<Token> Tokenize(string text)
    {
        var position = 0;
        while (position < text.Length)
        {
            var character = text[position];
            if (char.IsWhiteSpace(character))
            {
                position++;
                continue;
            }

            if (char.IsLetter(character) || character == '_')
            {
                var start = position++;
                while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] == '_'))
                {
                    position++;
                }

                var identifier = text[start..position];
                yield return identifier switch
                {
                    "true" => new Token(TokenKind.True, identifier),
                    "false" => new Token(TokenKind.False, identifier),
                    "null" => new Token(TokenKind.Null, identifier),
                    _ => new Token(TokenKind.Identifier, identifier),
                };
                continue;
            }

            if (char.IsDigit(character))
            {
                var start = position++;
                while (position < text.Length && char.IsDigit(text[position]))
                {
                    position++;
                }

                yield return new Token(TokenKind.Number, text[start..position]);
                continue;
            }

            if (character == '"')
            {
                position++;
                var value = new StringBuilder();
                while (position < text.Length && text[position] != '"')
                {
                    if (text[position] == '\\')
                    {
                        position++;
                        if (position >= text.Length)
                        {
                            throw new InvalidOperationException("Unterminated escape sequence in DSL string literal.");
                        }

                        value.Append(text[position] switch
                        {
                            '"' => '"',
                            '\\' => '\\',
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            var escaped => escaped,
                        });
                        position++;
                        continue;
                    }

                    value.Append(text[position++]);
                }

                if (position >= text.Length || text[position] != '"')
                {
                    throw new InvalidOperationException("Unterminated DSL string literal.");
                }

                position++;
                yield return new Token(TokenKind.String, value.ToString());
                continue;
            }

            if (TryReadOperator(text, ref position, out var token))
            {
                yield return token;
                continue;
            }

            throw new InvalidOperationException($"Unexpected DSL character '{character}'.");
        }

        yield return new Token(TokenKind.End, "<end>");
    }

    private static bool TryReadOperator(string text, ref int position, out Token token)
    {
        foreach (var (literal, kind) in OperatorTokens)
        {
            if (text.AsSpan(position).StartsWith(literal, StringComparison.Ordinal))
            {
                position += literal.Length;
                token = new Token(kind, literal);
                return true;
            }
        }

        token = default;
        return false;
    }

    private static readonly (string Literal, TokenKind Kind)[] OperatorTokens =
    [
        ("=>", TokenKind.Arrow),
        ("&&", TokenKind.AmpAmp),
        ("||", TokenKind.BarBar),
        ("==", TokenKind.EqualsEquals),
        ("!=", TokenKind.BangEquals),
        (">=", TokenKind.GreaterEquals),
        ("<=", TokenKind.LessEquals),
        ("+=", TokenKind.PlusEquals),
        ("-=", TokenKind.MinusEquals),
        ("++", TokenKind.PlusPlus),
        ("--", TokenKind.MinusMinus),
        (".", TokenKind.Dot),
        ("(", TokenKind.OpenParen),
        (")", TokenKind.CloseParen),
        ("[", TokenKind.OpenBracket),
        ("]", TokenKind.CloseBracket),
        (",", TokenKind.Comma),
        ("?", TokenKind.Question),
        (":", TokenKind.Colon),
        ("=", TokenKind.Equals),
        ("!", TokenKind.Bang),
        (">", TokenKind.Greater),
        ("<", TokenKind.Less),
        ("+", TokenKind.Plus),
        ("-", TokenKind.Minus),
    ];

    private abstract class ExpressionNode
    {
        public abstract object? Evaluate(EvaluationScope scope);

        public virtual void Assign(EvaluationScope scope, object? value)
            => throw new InvalidOperationException("DSL assignment target is not assignable.");
    }

    private sealed class LiteralNode(object? value) : ExpressionNode
    {
        public override object? Evaluate(EvaluationScope scope) => value;
    }

    private sealed class IdentifierNode(string name) : ExpressionNode
    {
        public override object? Evaluate(EvaluationScope scope)
        {
            if (string.Equals(name, "context", StringComparison.Ordinal))
            {
                return scope.Context;
            }

            if (scope.TryGetLocal(name, out var local))
            {
                return local;
            }

            return ResolveKnownType(name) ?? throw new InvalidOperationException($"Unknown DSL identifier '{name}'.");
        }
    }

    private sealed class MemberAccessNode(ExpressionNode target, string memberName) : ExpressionNode
    {
        public override object? Evaluate(EvaluationScope scope)
        {
            var instance = target.Evaluate(scope);
            if (instance is KnownTypeReference knownType)
            {
                return knownType.ResolveMember(memberName);
            }

            return GetProperty(instance, memberName).GetValue(instance);
        }

        public override void Assign(EvaluationScope scope, object? value)
        {
            var instance = target.Evaluate(scope);
            var property = GetProperty(instance, memberName);
            property.SetValue(instance, ConvertTo(value, property.PropertyType));
        }
    }

    private sealed class IndexAccessNode(ExpressionNode target, ExpressionNode index) : ExpressionNode
    {
        public override object? Evaluate(EvaluationScope scope)
        {
            var instance = target.Evaluate(scope);
            var numericIndex = Convert.ToInt32(index.Evaluate(scope), CultureInfo.InvariantCulture);

            return instance switch
            {
                System.Collections.IList list => list[numericIndex],
                _ => throw new InvalidOperationException("DSL index access requires a list target."),
            };
        }
    }

    private sealed class MethodCallNode(ExpressionNode target, string methodName, IReadOnlyList<ExpressionNode> arguments) : ExpressionNode
    {
        public override object? Evaluate(EvaluationScope scope)
        {
            var instance = target.Evaluate(scope);
            ArgumentNullException.ThrowIfNull(instance);

            var values = arguments.Select(argument => argument.Evaluate(scope)).ToArray();
            return InvokeMethod(instance, methodName, values);
        }
    }

    private sealed class AnyNode(ExpressionNode source, string parameterName, ExpressionNode predicate) : ExpressionNode
    {
        public override object? Evaluate(EvaluationScope scope)
        {
            var enumerable = source.Evaluate(scope) as System.Collections.IEnumerable
                ?? throw new InvalidOperationException("DSL Any requires an enumerable target.");

            foreach (var item in enumerable)
            {
                if (ToBool(predicate.Evaluate(scope.Push(parameterName, item))))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class UnaryNode(ExpressionNode operand, UnaryOperator operatorKind) : ExpressionNode
    {
        public override object? Evaluate(EvaluationScope scope)
            => operatorKind switch
            {
                UnaryOperator.Not => !ToBool(operand.Evaluate(scope)),
                UnaryOperator.Negate => -ToInt(operand.Evaluate(scope)),
                _ => throw new InvalidOperationException("Unknown unary DSL operator."),
            };
    }

    private sealed class BinaryNode(ExpressionNode left, ExpressionNode right, BinaryOperator operatorKind) : ExpressionNode
    {
        public override object? Evaluate(EvaluationScope scope)
            => operatorKind switch
            {
                BinaryOperator.And => ToBool(left.Evaluate(scope)) && ToBool(right.Evaluate(scope)),
                BinaryOperator.Or => ToBool(left.Evaluate(scope)) || ToBool(right.Evaluate(scope)),
                BinaryOperator.Equal => EqualsValue(left.Evaluate(scope), right.Evaluate(scope)),
                BinaryOperator.NotEqual => !EqualsValue(left.Evaluate(scope), right.Evaluate(scope)),
                BinaryOperator.Greater => ToInt(left.Evaluate(scope)) > ToInt(right.Evaluate(scope)),
                BinaryOperator.GreaterOrEqual => ToInt(left.Evaluate(scope)) >= ToInt(right.Evaluate(scope)),
                BinaryOperator.Less => ToInt(left.Evaluate(scope)) < ToInt(right.Evaluate(scope)),
                BinaryOperator.LessOrEqual => ToInt(left.Evaluate(scope)) <= ToInt(right.Evaluate(scope)),
                BinaryOperator.Add => ToInt(left.Evaluate(scope)) + ToInt(right.Evaluate(scope)),
                BinaryOperator.Subtract => ToInt(left.Evaluate(scope)) - ToInt(right.Evaluate(scope)),
                _ => throw new InvalidOperationException("Unknown binary DSL operator."),
            };
    }

    private sealed class IsNullNode(ExpressionNode operand, bool negated) : ExpressionNode
    {
        public override object? Evaluate(EvaluationScope scope)
        {
            var isNull = operand.Evaluate(scope) is null;
            return negated ? !isNull : isNull;
        }
    }

    private sealed class ConditionalNode(ExpressionNode condition, ExpressionNode whenTrue, ExpressionNode whenFalse) : ExpressionNode
    {
        public override object? Evaluate(EvaluationScope scope)
            => ToBool(condition.Evaluate(scope)) ? whenTrue.Evaluate(scope) : whenFalse.Evaluate(scope);
    }

    private abstract class EffectStatement
    {
        public abstract void Execute(EvaluationScope scope);
    }

    private sealed class AssignmentStatement(ExpressionNode target, ExpressionNode value) : EffectStatement
    {
        public override void Execute(EvaluationScope scope) => target.Assign(scope, value.Evaluate(scope));
    }

    private sealed class CompoundAssignmentStatement(ExpressionNode target, ExpressionNode value, BinaryOperator operatorKind) : EffectStatement
    {
        public override void Execute(EvaluationScope scope)
        {
            var current = target.Evaluate(scope);
            var updated = operatorKind switch
            {
                BinaryOperator.Add => ToInt(current) + ToInt(value.Evaluate(scope)),
                BinaryOperator.Subtract => ToInt(current) - ToInt(value.Evaluate(scope)),
                _ => throw new InvalidOperationException("Unknown compound assignment operator."),
            };
            target.Assign(scope, updated);
        }
    }

    private sealed class ExpressionEffectStatement(ExpressionNode expression) : EffectStatement
    {
        public override void Execute(EvaluationScope scope)
        {
            var result = expression.Evaluate(scope);
            if (result is not null)
            {
                throw new InvalidOperationException("DSL void effect must assign, mutate, or call a void method.");
            }
        }
    }

    private sealed class EvaluationScope(object context, ImmutableDictionary<string, object?>? locals = null)
    {
        public object Context { get; } = context;

        public bool TryGetLocal(string name, out object? value)
            => (locals ?? ImmutableDictionary<string, object?>.Empty).TryGetValue(name, out value);

        public EvaluationScope Push(string name, object? value)
        {
            var nextLocals = (locals ?? ImmutableDictionary<string, object?>.Empty).SetItem(name, value);
            return new EvaluationScope(Context, nextLocals);
        }
    }

    private sealed class KnownTypeReference(Type type)
    {
        public object ResolveMember(string memberName)
        {
            if (type.IsEnum && Enum.TryParse(type, memberName, out var value))
            {
                return value;
            }

            throw new InvalidOperationException($"Unknown DSL type member '{type.Name}.{memberName}'.");
        }
    }

    private static KnownTypeReference? ResolveKnownType(string name)
        => string.Equals(name, nameof(WorldLocation), StringComparison.Ordinal)
            ? new KnownTypeReference(typeof(WorldLocation))
            : null;

    private static PropertyInfo GetProperty(object? instance, string memberName)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var property = instance switch
        {
            GameState => typeof(GameState).GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public),
            PlayerState => typeof(PlayerState).GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public),
            RobotState => typeof(RobotState).GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public),
            RobotAbilityState => typeof(RobotAbilityState).GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public),
            InventoryState => typeof(InventoryState).GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public),
            InventoryEntryState => typeof(InventoryEntryState).GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public),
            WorldState => typeof(WorldState).GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public),
            CityState => typeof(CityState).GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public),
            PageState => typeof(PageState).GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public),
            PageChoiceState => typeof(PageChoiceState).GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public),
            DiceState => typeof(DiceState).GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public),
            _ => null,
        };

        return property ?? throw new InvalidOperationException($"Unknown DSL property '{memberName}' on '{instance.GetType().Name}'.");
    }

    private static object? InvokeMethod(object instance, string methodName, object?[] values)
        => (instance, methodName, values.Length) switch
        {
            (RobotState robot, "HasAbility", 1) => robot.Abilities.Any(ability =>
                string.Equals(ability.Name, (string)ConvertTo(values[0], typeof(string))!, StringComparison.OrdinalIgnoreCase)),
            (InventoryState inventory, nameof(InventoryState.Contains), 1) => inventory.Contains((string)ConvertTo(values[0], typeof(string))!),
            (InventoryState inventory, nameof(InventoryState.GetQuantity), 1) => inventory.GetQuantity((string)ConvertTo(values[0], typeof(string))!),
            (WorldState world, nameof(WorldState.HasFact), 1) => world.HasFact((string)ConvertTo(values[0], typeof(string))!),
            (WorldState world, nameof(WorldState.AddFact), 1) => Run(() => world.AddFact((string)ConvertTo(values[0], typeof(string))!)),
            (DiceState die, nameof(DiceState.Roll), 0) => die.Roll(),
            (DiceState die, nameof(DiceState.Roll), 1) => die.Roll(ToInt(values[0])),
            _ => throw new InvalidOperationException($"Unknown DSL method '{methodName}' on '{instance.GetType().Name}'."),
        };

    private static object? Run(Action action)
    {
        action();
        return null;
    }

    private static object? ConvertTo(object? value, Type targetType)
    {
        if (value is null)
        {
            return targetType.IsValueType
                ? throw new InvalidOperationException($"DSL cannot assign null to '{targetType.Name}'.")
                : null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType.IsEnum)
        {
            return value is string text
                ? Enum.Parse(targetType, text)
                : Enum.ToObject(targetType, value);
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    private static bool ToBool(object? value)
        => value is bool boolean
            ? boolean
            : throw new InvalidOperationException("DSL value is not a bool.");

    private static int ToInt(object? value)
        => Convert.ToInt32(value, CultureInfo.InvariantCulture);

    private static bool EqualsValue(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left.GetType().IsEnum && right is string text)
        {
            right = Enum.Parse(left.GetType(), text);
        }
        else if (right.GetType().IsEnum && left is string leftText)
        {
            left = Enum.Parse(right.GetType(), leftText);
        }
        else if (left.GetType() != right.GetType() && left is IConvertible && right is IConvertible)
        {
            right = Convert.ChangeType(right, left.GetType(), CultureInfo.InvariantCulture);
        }

        return left.Equals(right);
    }

    private readonly record struct Token(TokenKind Kind, string Text);

    private enum TokenKind
    {
        Identifier,
        Number,
        String,
        True,
        False,
        Null,
        Arrow,
        Dot,
        OpenParen,
        CloseParen,
        OpenBracket,
        CloseBracket,
        Comma,
        Question,
        Colon,
        Equals,
        EqualsEquals,
        Bang,
        BangEquals,
        AmpAmp,
        BarBar,
        Greater,
        GreaterEquals,
        Less,
        LessEquals,
        Plus,
        PlusEquals,
        PlusPlus,
        Minus,
        MinusEquals,
        MinusMinus,
        End,
    }

    private enum UnaryOperator
    {
        Not,
        Negate,
    }

    private enum BinaryOperator
    {
        And,
        Or,
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
        Add,
        Subtract,
    }
}
