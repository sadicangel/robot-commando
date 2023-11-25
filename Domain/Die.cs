namespace RobotCommando;

public sealed record class Die(int Sides)
{
    public int Roll(int times = 1) => Random.Shared.Next(times, Sides * times + 1);
}