namespace RobotCommando.Models;

public record Player(
    string Name,
    int Skill,
    int Stamina,
    int Luck)
{
    public Player(string name) : this(name, 6 + Die.Roll(1), 12 + Die.Roll(2), 6 + Die.Roll(1)) { }

    public int MaxSkill { get; init; } = Skill;
    public int MaxStamina { get; init; } = Stamina;
    public int MaxLuck { get; init; } = Luck;
}
