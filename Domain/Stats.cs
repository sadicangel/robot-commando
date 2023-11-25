namespace RobotCommando;

public enum SpeedValue { Static, Slow, Average, Fast, VeryFast, UltraFast }

public abstract record class Stat<T>(T Max, T Val);

public sealed record class Stamina(int Val) : Stat<int>(Val, Val);
public sealed record class Skill(int Val) : Stat<int>(Val, Val);
public sealed record class Luck(int Val) : Stat<int>(Val, Val);
public sealed record class Armor(int Val) : Stat<int>(Val, Val);
public sealed record class Bonus(int Val) : Stat<int>(Val, Val);
public sealed record class Speed(SpeedValue Val) : Stat<SpeedValue>(Val, Val);