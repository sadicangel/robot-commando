namespace RobotCommando;

public sealed record class Page(
    Guid Id,
    string Tag,
    string Name,
    string Description,
    string Icon,
    int Number,
    Location Location,
    string Text,
    string TextRevisited,
    bool IsVisited,
    IReadOnlyList<Choice> Choices,
    IReadOnlyList<Item>? Items = null,
    IReadOnlyList<Robot>? Robots = null,
    IReadOnlyList<Monster>? Monsters = null,
    IReadOnlyList<Enemy>? Enemies = null,
    IReadOnlyList<Event>? Events = null)
: IPage
{
    public IReadOnlyList<Item> Items { get; } = Items ?? Array.Empty<Item>();
    public IReadOnlyList<Robot> Robots { get; } = Robots ?? Array.Empty<Robot>();
    public IReadOnlyList<Monster> Monsters { get; } = Monsters ?? Array.Empty<Monster>();
    public IReadOnlyList<Enemy> Enemies { get; } = Enemies ?? Array.Empty<Enemy>();
    public IReadOnlyList<Event> Events { get; } = Events ?? Array.Empty<Event>();
}
