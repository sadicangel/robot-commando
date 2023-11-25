namespace RobotCommando;

public sealed record class Ability(
    Guid Id,
    string Tag,
    string Name,
    string Description,
    string Icon,
    AbilityType Type)
: IAbility;
