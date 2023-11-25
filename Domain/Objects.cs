namespace RobotCommando;

public interface IObject
{
    Guid Id { get; }
    string Tag { get; }
    string Name { get; }
    string Description { get; }
    string Icon { get; }
}

public enum MechaType { Unspecified, Dinosaur, Humanoid }

public interface IMecha : IObject
{
    MechaType Type { get; }
    Armor Armor { get; }
    Speed Speed { get; }
    IReadOnlyList<Ability> Abilities { get; }
}

public interface IHumanoid : IObject
{
    Stamina Stamina { get; }
    Skill Skill { get; }
}

public enum AbilityType { World, Active, Passive }

public interface IAbility : IObject
{
    AbilityType Type { get; }
}

public interface IItem : IObject
{
    Func<Game, bool>? OnItemAdd { get; }
    Func<Game, bool>? OnItemRemove { get; }
    Func<Game, bool>? OnItemUse { get; }
}

public readonly record struct SwapIndices(int Index1, int Index2);

public interface IInventory : IObject, IReadOnlyList<IReadOnlyList<Item>>
{
    event Action<Inventory, Item>? ItemAdded;
    event Action<Inventory, Item>? ItemRemoved;
    event Action<Inventory, SwapIndices>? ItemMoved;

    void Add(Item item);

    void Remove(Item item);

    bool Contains(Item item);
}

public enum Location { Unknown, Current, Farm, CapitalCity, CityOfIndustry, CityOfKnowledge, CityOfPleasure, CityOfStorms, CityOfTheGuardians, CityOfTheJungle, CityOfWorship }

public interface IPage : IObject
{
    int Number { get; }
    Location Location { get; }
    string Text { get; }
    string? TextRevisited { get; }
    bool IsVisited { get; }
    IReadOnlyList<Choice> Choices { get; }
    IReadOnlyList<Item> Items { get; }
    IReadOnlyList<Robot> Robots { get; }
    IReadOnlyList<Monster> Monsters { get; }
    IReadOnlyList<Enemy> Enemies { get; }
    IReadOnlyList<Event> Events { get; }
}