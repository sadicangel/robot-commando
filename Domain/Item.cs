namespace RobotCommando;

public sealed record class Item(
    Guid Id,
    string Tag,
    string Name,
    string Description,
    string Icon,
    Func<Game, bool>? OnItemAdd = null,
    Func<Game, bool>? OnItemRemove = null,
    Func<Game, bool>? OnItemUse = null)
: IItem;
