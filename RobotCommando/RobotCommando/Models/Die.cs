namespace RobotCommando.Models;

public static class Die
{
    public static int Roll(int times) => Enumerable.Range(0, times).Sum(_ => Random.Shared.Next(1, 7));
}
