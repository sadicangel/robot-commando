namespace RobotCommando;

public sealed record class Choice(
    string Text,
    int Link,
    Func<Game, bool>? CanSelect = null,
    Action<Game>? OnSelect = null,
    bool ShowOnlyWhenSelectable = true);