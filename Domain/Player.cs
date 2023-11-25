
namespace RobotCommando;

public sealed record class Player(
    Guid Id,
    string Tag,
    string Name,
    string Description,
    string Icon,
    Stamina Stamina,
    Skill Skill,
    Luck Luck,
    int MechaSkill)
: IHumanoid;