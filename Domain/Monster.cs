namespace RobotCommando;

public sealed record class Monster(
    Guid Id,
    string Tag,
    string Name,
    string Description,
    string Icon,
    MechaType Type,
    Armor Armor,
    Skill Skill,
    Speed Speed,
    IReadOnlyList<Ability> Abilities,
    BattleResult BattleResult)
: IMecha;
