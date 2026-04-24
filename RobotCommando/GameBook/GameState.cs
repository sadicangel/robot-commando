#nullable enable
using System;
using System.Collections.Generic;

namespace RobotCommando.GameBook;

public sealed class GameState
{
    public PlayerState Player { get; init; } = new();
    public RobotState? Robot { get; set; }
    public InventoryState Inventory { get; init; } = new();
    public WorldState World { get; init; } = new();
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
    public int Armor { get; set; }
    public int ArmorMax { get; set; }
    public int CombatBonus { get; set; }
    public SpeedBand Speed { get; set; }
    public List<RobotAbilityState> Abilities { get; } = [];
}

public sealed class RobotAbilityState
{
    public string Name { get; set; } = string.Empty;
}

public sealed class InventoryState
{
    private readonly HashSet<string> _itemTags = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> ItemTags => _itemTags;

    public void Add(string tag) => _itemTags.Add(tag);

    public bool Contains(string tag) => _itemTags.Contains(tag);
}

public sealed class WorldState
{
    private readonly HashSet<string> _facts = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Facts => _facts;

    public void AddFact(string fact) => _facts.Add(fact);

    public bool HasFact(string fact) => _facts.Contains(fact);
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
