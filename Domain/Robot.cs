namespace RobotCommando;

public sealed record class Robot(
    Guid Id,
    string Tag,
    string Name,
    string Description,
    string Icon,
    MechaType Type,
    Armor Armor,
    Bonus Bonus,
    Speed Speed,
    IReadOnlyList<Ability> Abilities)
: IMecha;