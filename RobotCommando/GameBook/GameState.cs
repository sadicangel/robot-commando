namespace RobotCommando.GameBook;

public sealed class GameState
{
    public PlayerState Player { get; init; } = new();
    public RobotState? Robot { get; set; }
    public InventoryState Inventory { get; init; } = new();
    public WorldState World { get; init; } = new();
    public CityState City { get; init; } = new();
    public PageState Page { get; init; } = new();
    public DiceState Die { get; init; } = new();
}

public sealed class PlayerState
{
    public int Skill { get; set; }
    public int SkillMax { get; set; }
    public int Stamina { get; set; }
    public int StaminaMax { get; set; }
    public int Luck { get; set; }
    public int LuckMax { get; set; }
    public int RobotSkill { get; set; }
    public int RobotSkillMax { get; set; }
}

public sealed class RobotState
{
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RobotFrame Frame { get; set; }
    public int Armor { get; set; }
    public int ArmorMax { get; set; }
    public int CombatBonus { get; set; }
    public int CombatBonusMax { get; set; }
    public SpeedBand Speed { get; set; }
    public SpeedBand SpeedMax { get; set; }
    public List<RobotAbilityState> Abilities { get; } = [];

    public RobotState Clone()
    {
        var clone = new RobotState
        {
            Tag = Tag,
            Name = Name,
            Description = Description,
            Frame = Frame,
            Armor = Armor,
            ArmorMax = ArmorMax,
            CombatBonus = CombatBonus,
            CombatBonusMax = CombatBonusMax,
            Speed = Speed,
            SpeedMax = SpeedMax,
        };

        foreach (var ability in Abilities)
        {
            clone.Abilities.Add(new RobotAbilityState { Name = ability.Name });
        }

        return clone;
    }
}

public sealed class RobotAbilityState
{
    public string Name { get; set; } = string.Empty;
}

public sealed class InventoryState
{
    private readonly Dictionary<string, InventoryEntryState> _items = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> ItemTags => _items.Keys;

    public IReadOnlyCollection<InventoryEntryState> Entries => _items.Values;

    public void Add(string tag) => Add(tag, tag, quantity: 1);

    public void Add(string tag, string name, int quantity, string? description = null, string? icon = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be greater than zero.");
        }

        if (_items.TryGetValue(tag, out var entry))
        {
            entry.Quantity += quantity;
            entry.Name = name;
            if (!string.IsNullOrWhiteSpace(description))
            {
                entry.Description = description;
            }

            if (!string.IsNullOrWhiteSpace(icon))
            {
                entry.Icon = icon;
            }

            return;
        }

        _items[tag] = new InventoryEntryState
        {
            Tag = tag,
            Name = name,
            Description = description ?? string.Empty,
            Icon = icon ?? string.Empty,
            Quantity = quantity,
        };
    }

    public bool Contains(string tag) => _items.TryGetValue(tag, out var entry) && entry.Quantity > 0;

    public int GetQuantity(string tag) => _items.TryGetValue(tag, out var entry) ? entry.Quantity : 0;
}

public sealed class InventoryEntryState
{
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public sealed class WorldState
{
    private readonly HashSet<string> _facts = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Facts => _facts;

    public void AddFact(string fact) => _facts.Add(fact);

    public bool HasFact(string fact) => _facts.Contains(fact);
}

public sealed class CityState
{
    public WorldLocation Location { get; set; }
    public bool IsVisited { get; set; }
}

public sealed class PageState
{
    public int Number { get; set; }
    public WorldLocation Location { get; set; }
    public bool IsVisited { get; set; }
    public List<PageChoiceState> Choices { get; } = [];
}

public sealed class PageChoiceState
{
    public int To { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class DiceState
{
    public int Roll() => throw new NotSupportedException("DiceState is a placeholder for DSL evaluation and should be supplied by the game runtime.");

    public int Roll(int count) => throw new NotSupportedException("DiceState is a placeholder for DSL evaluation and should be supplied by the game runtime.");
}
