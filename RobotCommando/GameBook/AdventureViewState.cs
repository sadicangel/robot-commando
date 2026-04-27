namespace RobotCommando.GameBook;

public sealed record AdventureViewState(
    bool HasSession,
    int BlockId,
    string BlockNumberText,
    string LocationDisplay,
    string PageText,
    PlayerStatsViewData Player,
    RobotViewData Robot,
    ImmutableArray<InventoryEntryViewData> Inventory,
    string InventorySummaryText,
    ImmutableArray<InteractionViewData> Interactions,
    string InteractionsSummaryText,
    ImmutableArray<ChoiceViewData> Choices,
    string ChoicesSummaryText,
    bool HasActiveEncounter)
{
    public static AdventureViewState Empty { get; } = new(
        HasSession: false,
        BlockId: 0,
        BlockNumberText: "Block -",
        LocationDisplay: "No location",
        PageText: "Start a new adventure from the main menu.",
        Player: PlayerStatsViewData.Empty,
        Robot: RobotViewData.None,
        Inventory: [],
        InventorySummaryText: "No inventory yet.",
        Interactions: [],
        InteractionsSummaryText: "Nothing to interact with.",
        Choices: [],
        ChoicesSummaryText: "No story choices available.",
        HasActiveEncounter: false);
}

public sealed record PlayerStatsViewData(
    string SkillText,
    string StaminaText,
    string LuckText,
    string RobotSkillText)
{
    public static PlayerStatsViewData Empty { get; } = new(
        SkillText: "Skill -",
        StaminaText: "Stamina -",
        LuckText: "Luck -",
        RobotSkillText: "Robot Skill -");
}

public sealed record RobotViewData(
    string Name,
    string Description,
    string ArmorText,
    string SpeedText,
    string CombatBonusText,
    string AbilitiesText)
{
    public static RobotViewData None { get; } = new(
        Name: "No robot equipped",
        Description: "You are travelling on foot.",
        ArmorText: "Armor -",
        SpeedText: "Speed -",
        CombatBonusText: "Combat Bonus -",
        AbilitiesText: "Abilities None");
}

public sealed record InventoryEntryViewData(
    string Tag,
    string Name,
    string Description,
    string Icon,
    string IconGlyph,
    int Quantity,
    string QuantityText);

public sealed partial record ChoiceViewData(
    string Id,
    string Text,
    int TargetBlockId,
    bool IsEnabled);

public sealed partial record InteractionViewData(
    string Id,
    string KindLabel,
    string Title,
    string Description,
    string Icon,
    string IconGlyph,
    string ToolTipText,
    string PrimaryActionLabel,
    bool CanEscape);
