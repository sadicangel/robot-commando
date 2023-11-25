
namespace RobotCommando;

public sealed record class Enemy(
    Guid Id,
    string Tag,
    string Name,
    string Description,
    string Icon,
    Stamina Stamina,
    Skill Skill,
    BattleResult BattleResult)
: IHumanoid;
