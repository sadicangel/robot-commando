namespace RobotCommando.GameBook;

public sealed partial class AdventureService : IAdventureService
{
    public const int EndAdventureBlockId = -1;

    private const string EndAdventureChoiceText = "Return to ending screen.";

    private readonly IBookRepository _bookRepository;
    private readonly IState<AdventureViewState> _viewState;

    private AdventureSession? _session;
    private AdventureViewState _snapshot = AdventureViewState.Empty;

    public AdventureService(IBookRepository bookRepository)
    {
        _bookRepository = bookRepository;
        _viewState = State.Value(this, () => AdventureViewState.Empty);
    }

    public AdventureViewState Snapshot => _snapshot;

    public IState<AdventureViewState> ViewState => _viewState;

    public async Task StartNewGame(CancellationToken cancellationToken = default)
    {
        _bookRepository.GetAllBlocks();
        _session = new AdventureSession(CreateInitialGameState());

        await EnterBlockAsync(0, cancellationToken);
    }

    public async Task SelectChoice(string actionId, CancellationToken cancellationToken = default)
    {
        var session = GetSession();
        var viewChoice = BuildViewState(session)
            .Choices
            .FirstOrDefault(candidate => string.Equals(candidate.Id, actionId, StringComparison.Ordinal));

        if (viewChoice is null || !viewChoice.IsEnabled)
        {
            throw new InvalidOperationException($"Choice '{actionId}' is not available.");
        }

        var choiceIndex = ParseActionIndex(actionId, "choice");
        var block = session.CurrentBlock ?? throw new InvalidOperationException("No current block is active.");
        if (choiceIndex < 0 || choiceIndex >= block.Choices.Count)
        {
            throw new InvalidOperationException($"Choice '{actionId}' is not available.");
        }

        var choice = block.Choices[choiceIndex];
        choice.Effect?.Execute(session.GameState);

        var targetBlockId = session.GameState.Page.Choices[choiceIndex].To;
        if (targetBlockId == EndAdventureBlockId
            && string.Equals(choice.Text, EndAdventureChoiceText, StringComparison.Ordinal))
        {
            _session = null;
            await PublishAsync(AdventureViewState.Empty, cancellationToken);
            return;
        }

        await EnterBlockAsync(targetBlockId, cancellationToken);
    }

    public async Task PickUpItem(string actionId, CancellationToken cancellationToken = default)
    {
        var session = GetSession();
        EnsureNoActiveEncounter(session);

        var itemIndex = ParseActionIndex(actionId, "item");
        var block = session.CurrentBlock ?? throw new InvalidOperationException("No current block is active.");
        var progress = session.GetProgress(block.Id);

        if (itemIndex < 0 || itemIndex >= block.Items.Count || progress.TakenItemIndices.Contains(itemIndex))
        {
            throw new InvalidOperationException($"Item '{actionId}' is not available.");
        }

        var item = block.Items[itemIndex];
        var metadata = ResolveItemMetadata(item);
        session.GameState.Inventory.Add(
            metadata.Tag,
            metadata.Name,
            quantity: 1,
            metadata.Description,
            metadata.Icon);
        if (ShouldRunTrigger(item.OnAcquire, session.GameState))
        {
            item.OnAcquire!.Effect?.Execute(session.GameState);
        }

        progress.TakenItemIndices.Add(itemIndex);

        await PublishAsync(BuildViewState(session), cancellationToken);
    }

    public async Task TakeRobot(string actionId, CancellationToken cancellationToken = default)
    {
        var session = GetSession();
        EnsureNoActiveEncounter(session);

        var block = session.CurrentBlock ?? throw new InvalidOperationException("No current block is active.");
        var progress = session.GetProgress(block.Id);

        if (TryParseDroppedRobotActionIndex(actionId, out var droppedRobotIndex))
        {
            if (droppedRobotIndex < 0 || droppedRobotIndex >= progress.DroppedRobots.Count)
            {
                throw new InvalidOperationException($"Robot '{actionId}' is not available.");
            }

            var selectedRobot = progress.DroppedRobots[droppedRobotIndex].Clone();
            progress.DroppedRobots.RemoveAt(droppedRobotIndex);
            DropCurrentRobot(progress, session.GameState.Robot);
            session.GameState.Robot = selectedRobot;
        }
        else
        {
            var robotIndex = ParseActionIndex(actionId, "robot");
            if (robotIndex < 0 || robotIndex >= block.Robots.Count || progress.TakenRobotIndices.Contains(robotIndex))
            {
                throw new InvalidOperationException($"Robot '{actionId}' is not available.");
            }

            DropCurrentRobot(progress, session.GameState.Robot);
            session.GameState.Robot = CreateRobotState(block.Robots[robotIndex]);
            progress.TakenRobotIndices.Add(robotIndex);
        }

        await PublishAsync(BuildViewState(session), cancellationToken);
    }

    public async Task Fight(string actionId, CancellationToken cancellationToken = default)
    {
        var session = GetSession();
        var encounter = GetActiveEncounter(session);

        if (encounter is null || !string.Equals(encounter.Id, actionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Encounter '{actionId}' is not available.");
        }

        var progress = session.GetProgress(session.CurrentBlockId);
        MarkEncounterResolved(encounter, progress);

        if (encounter.BattleOutcome?.WinSpecified == true && encounter.BattleOutcome.Win > 0)
        {
            await EnterBlockAsync(encounter.BattleOutcome.Win, cancellationToken);
            return;
        }

        await PublishAsync(BuildViewState(session), cancellationToken);
    }

    public async Task Escape(string actionId, CancellationToken cancellationToken = default)
    {
        var session = GetSession();
        var encounter = GetActiveEncounter(session);

        if (encounter is null || !string.Equals(encounter.Id, actionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Encounter '{actionId}' is not available.");
        }

        if (encounter.BattleOutcome?.EscapeSpecified != true || encounter.BattleOutcome.Escape <= 0)
        {
            throw new InvalidOperationException($"Encounter '{actionId}' does not allow escape.");
        }

        await EnterBlockAsync(encounter.BattleOutcome.Escape, cancellationToken);
    }

    private async Task EnterBlockAsync(int blockId, CancellationToken cancellationToken)
    {
        var session = GetSession();
        var block = _bookRepository.GetBlock(blockId);
        var wasVisited = session.VisitedBlockIds.Contains(blockId);
        var nextLocation = ResolveEffectiveLocation(session, block.Location);
        var previousCity = TryGetCityLocation(session.GameState.City.Location, out var currentCity)
            ? currentCity
            : (WorldLocation?)null;
        var nextCity = TryGetCityLocation(nextLocation, out var targetCity)
            ? targetCity
            : (WorldLocation?)null;

        if (previousCity is not null && previousCity != nextCity)
        {
            session.VisitedCityLocations.Add(previousCity.Value);
        }

        var isCityVisited = nextCity is not null && session.VisitedCityLocations.Contains(nextCity.Value);

        session.CurrentBlockId = blockId;
        session.CurrentBlock = block;
        session.GameState.Page.Number = block.Id;
        session.GameState.Page.Location = nextLocation;
        session.GameState.Page.IsVisited = wasVisited;
        SyncPageChoices(session.GameState.Page, block);
        session.GameState.City.Location = nextCity ?? WorldLocation.Unknown;
        session.GameState.City.IsVisited = isCityVisited;

        foreach (var effect in block.Effects)
        {
            effect.Execute(session.GameState);
        }

        session.VisitedBlockIds.Add(blockId);

        await PublishAsync(BuildViewState(session), cancellationToken);
    }

    private async Task PublishAsync(AdventureViewState viewState, CancellationToken cancellationToken)
    {
        _snapshot = viewState;
        await _viewState.UpdateAsync(_ => viewState, cancellationToken);
    }

    private static WorldLocation ResolveEffectiveLocation(AdventureSession session, WorldLocation location)
        => location == WorldLocation.Inherit
            ? session.GameState.Page.Location
            : location;

    private static string GetPageText(AdventureSession session, BookBlock block)
    {
        if (string.IsNullOrWhiteSpace(block.RevisitText))
        {
            return block.Text;
        }

        var isCityRevisitBlock = UsesCityVisitedState(block);
        if (isCityRevisitBlock && session.GameState.City.IsVisited)
        {
            return block.RevisitText!;
        }

        if (!isCityRevisitBlock && session.GameState.Page.IsVisited)
        {
            return block.RevisitText!;
        }

        return block.Text;
    }

    private static bool UsesCityVisitedState(BookBlock block)
        => block.Choices.Any(choice =>
            choice.Condition?.Text.Contains("context.City.IsVisited", StringComparison.Ordinal) == true);

    private AdventureViewState BuildViewState(AdventureSession session)
    {
        var block = session.CurrentBlock;
        if (block is null)
        {
            return AdventureViewState.Empty;
        }

        var activeEncounter = GetActiveEncounter(session);
        var interactions = activeEncounter is not null
            ? [ProjectEncounter(activeEncounter)]
            : ProjectPassiveInteractions(session, block);

        var choices = activeEncounter is not null
            ? ImmutableArray<ChoiceViewData>.Empty
            : ProjectChoices(session, block);

        var inventory = session.GameState.Inventory.Entries
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ProjectInventoryEntry)
            .ToImmutableArray();

        return new AdventureViewState(
            HasSession: true,
            BlockId: block.Id,
            BlockNumberText: $"Block {block.Id}",
            LocationDisplay: GetLocationDisplayName(session.GameState.Page.Location),
            PageText: GetPageText(session, block),
            Player: ProjectPlayer(session.GameState.Player),
            Robot: ProjectRobot(session.GameState.Robot),
            Inventory: inventory,
            InventorySummaryText: inventory.Length == 0 ? "No items collected yet." : string.Empty,
            Interactions: interactions,
            InteractionsSummaryText: interactions.Length == 0
                ? "Nothing here can be interacted with right now."
                : string.Empty,
            Choices: choices,
            ChoicesSummaryText: choices.Length == 0
                ? activeEncounter is not null
                    ? "Resolve the current encounter to continue."
                    : "No story choices are available here."
                : string.Empty,
            HasActiveEncounter: activeEncounter is not null);
    }

    private static ImmutableArray<InteractionViewData> ProjectPassiveInteractions(AdventureSession session, BookBlock block)
    {
        var progress = session.GetProgress(block.Id);
        var interactions = ImmutableArray.CreateBuilder<InteractionViewData>();

        for (var index = 0; index < block.Items.Count; index++)
        {
            if (progress.TakenItemIndices.Contains(index))
            {
                continue;
            }

            var item = block.Items[index];
            var metadata = ResolveItemMetadata(item);
            interactions.Add(new InteractionViewData(
                Id: $"item:{index}",
                KindLabel: "Item",
                Title: metadata.Name,
                Description: string.IsNullOrWhiteSpace(metadata.Description) ? "Add this item to your inventory." : metadata.Description,
                Icon: metadata.Icon,
                IconGlyph: metadata.IconGlyph,
                ToolTipText: string.IsNullOrWhiteSpace(metadata.Description) ? metadata.Name : metadata.Description,
                PrimaryActionLabel: "Pick up",
                CanEscape: false));
        }

        for (var index = 0; index < block.Robots.Count; index++)
        {
            if (progress.TakenRobotIndices.Contains(index))
            {
                continue;
            }

            var robot = block.Robots[index];
            interactions.Add(new InteractionViewData(
                Id: $"robot:{index}",
                KindLabel: "Robot",
                Title: robot.Name,
                Description: string.IsNullOrWhiteSpace(robot.Description) ? "Equip this robot." : robot.Description,
                Icon: string.Empty,
                IconGlyph: "\U0001F916",
                ToolTipText: string.IsNullOrWhiteSpace(robot.Description) ? robot.Name : robot.Description,
                PrimaryActionLabel: GetRobotActionLabel(session.GameState.Robot),
                CanEscape: false));
        }

        for (var index = 0; index < progress.DroppedRobots.Count; index++)
        {
            var robot = progress.DroppedRobots[index];
            interactions.Add(new InteractionViewData(
                Id: $"robot:dropped:{index}",
                KindLabel: "Robot",
                Title: robot.Name,
                Description: string.IsNullOrWhiteSpace(robot.Description) ? "A robot waiting where you left it." : robot.Description,
                Icon: string.Empty,
                IconGlyph: "\U0001F916",
                ToolTipText: string.IsNullOrWhiteSpace(robot.Description) ? robot.Name : robot.Description,
                PrimaryActionLabel: GetRobotActionLabel(session.GameState.Robot),
                CanEscape: false));
        }

        return interactions.ToImmutable();
    }

    private static ImmutableArray<ChoiceViewData> ProjectChoices(AdventureSession session, BookBlock block)
    {
        if (session.GameState.Page.Choices.Count != block.Choices.Count)
        {
            SyncPageChoices(session.GameState.Page, block);
        }

        var choices = ImmutableArray.CreateBuilder<ChoiceViewData>();

        for (var index = 0; index < block.Choices.Count; index++)
        {
            var choice = block.Choices[index];
            var availability = EvaluateChoiceAvailability(choice, session.GameState);

            if (!availability.IsVisible)
            {
                continue;
            }

            choices.Add(new ChoiceViewData(
                Id: $"choice:{index}",
                Text: choice.Text,
                TargetBlockId: session.GameState.Page.Choices[index].To,
                IsEnabled: availability.IsEnabled));
        }

        return choices.ToImmutable();
    }

    private static InteractionViewData ProjectEncounter(EncounterState encounter)
        => new(
            Id: encounter.Id,
            KindLabel: encounter.KindLabel,
            Title: encounter.Name,
            Description: string.IsNullOrWhiteSpace(encounter.Description)
                ? "You must deal with this threat before moving on."
                : encounter.Description,
            Icon: string.Empty,
            IconGlyph: "\u26A0",
            ToolTipText: string.IsNullOrWhiteSpace(encounter.Description) ? encounter.Name : encounter.Description,
            PrimaryActionLabel: "Fight",
            CanEscape: encounter.BattleOutcome?.EscapeSpecified == true && encounter.BattleOutcome.Escape > 0);

    private static ChoiceAvailability EvaluateChoiceAvailability(BookChoice choice, GameState gameState)
    {
        if (choice.Condition is null || string.IsNullOrWhiteSpace(choice.Condition.Text))
        {
            return ChoiceAvailability.VisibleEnabled;
        }

        return choice.Condition.Evaluate(gameState)
            ? ChoiceAvailability.VisibleEnabled
            : choice.ShowWhenDisabled
                ? ChoiceAvailability.VisibleDisabled
                : ChoiceAvailability.Hidden;
    }

    private static bool ShouldRunTrigger(ItemTrigger? trigger, GameState gameState)
        => trigger is not null
            && (trigger.Condition is null || string.IsNullOrWhiteSpace(trigger.Condition.Text) || trigger.Condition.Evaluate(gameState));

    private static void SyncPageChoices(PageState page, BookBlock block)
    {
        page.Choices.Clear();
        foreach (var choice in block.Choices)
        {
            page.Choices.Add(new PageChoiceState
            {
                To = choice.To,
                Text = choice.Text,
            });
        }
    }

    private static bool TryGetCityLocation(WorldLocation location, out WorldLocation cityLocation)
    {
        cityLocation = location;
        return location is WorldLocation.CapitalCity
            or WorldLocation.CityOfIndustry
            or WorldLocation.CityOfKnowledge
            or WorldLocation.CityOfPleasure
            or WorldLocation.CityOfStorms
            or WorldLocation.CityOfTheGuardians
            or WorldLocation.CityOfTheJungle
            or WorldLocation.CityOfWorship;
    }

    private EncounterState? GetActiveEncounter(AdventureSession session)
    {
        var block = session.CurrentBlock;
        if (block is null)
        {
            return null;
        }

        var progress = session.GetProgress(block.Id);

        for (var index = 0; index < block.Enemies.Count; index++)
        {
            if (progress.DefeatedEnemyIndices.Contains(index))
            {
                continue;
            }

            var enemy = block.Enemies[index];
            return new EncounterState(
                $"enemy:{index}",
                "Enemy",
                enemy.Name,
                enemy.Description ?? string.Empty,
                enemy.BattleOutcome,
                false,
                index);
        }

        for (var index = 0; index < block.Monsters.Count; index++)
        {
            if (progress.DefeatedMonsterIndices.Contains(index))
            {
                continue;
            }

            var monster = block.Monsters[index];
            return new EncounterState(
                $"monster:{index}",
                "Monster",
                monster.Name,
                monster.Description ?? string.Empty,
                monster.BattleOutcome,
                true,
                index);
        }

        return null;
    }

    private static void MarkEncounterResolved(EncounterState encounter, BlockProgress progress)
    {
        if (encounter.IsMonster)
        {
            progress.DefeatedMonsterIndices.Add(encounter.Index);
        }
        else
        {
            progress.DefeatedEnemyIndices.Add(encounter.Index);
        }
    }

    private static void EnsureNoActiveEncounter(AdventureSession session)
    {
        if (session.CurrentBlock is null)
        {
            throw new InvalidOperationException("No current block is active.");
        }

        if (session.HasActiveEncounter())
        {
            throw new InvalidOperationException("Resolve the current encounter before interacting with the scene.");
        }
    }

    private static int ParseActionIndex(string actionId, string expectedPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);

        var parts = actionId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !string.Equals(parts[0], expectedPrefix, StringComparison.Ordinal) || !int.TryParse(parts[1], out var index))
        {
            throw new InvalidOperationException($"Action '{actionId}' is not a valid {expectedPrefix} identifier.");
        }

        return index;
    }

    private static bool TryParseDroppedRobotActionIndex(string actionId, out int index)
    {
        index = -1;

        var parts = actionId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 3
            && string.Equals(parts[0], "robot", StringComparison.Ordinal)
            && string.Equals(parts[1], "dropped", StringComparison.Ordinal)
            && int.TryParse(parts[2], out index);
    }

    private AdventureSession GetSession()
        => _session ?? throw new InvalidOperationException("No adventure session is active.");

    private static GameState CreateInitialGameState()
    {
        var gameState = new GameState();
        gameState.Player.Skill = 9;
        gameState.Player.SkillMax = 9;
        gameState.Player.Stamina = 19;
        gameState.Player.StaminaMax = 19;
        gameState.Player.Luck = 9;
        gameState.Player.LuckMax = 9;
        gameState.Player.RobotSkill = 0;
        gameState.Player.RobotSkillMax = 0;

        var medikit = ItemMetadataCatalog.GetRequired("Medikit");
        gameState.Inventory.Add(medikit.Tag, medikit.Name, quantity: 5, medikit.Description, medikit.Icon);

        var sword = ItemMetadataCatalog.GetRequired("Sword");
        gameState.Inventory.Add(sword.Tag, sword.Name, quantity: 1, sword.Description, sword.Icon);

        return gameState;
    }

    private static InventoryEntryViewData ProjectInventoryEntry(InventoryEntryState entry)
    {
        var metadata = ItemMetadataCatalog.Resolve(entry.Tag, entry.Name, entry.Description, entry.Icon);
        return new InventoryEntryViewData(
            metadata.Tag,
            metadata.Name,
            metadata.Description,
            metadata.Icon,
            metadata.IconGlyph,
            entry.Quantity,
            $"x{entry.Quantity}");
    }

    private static ItemMetadata ResolveItemMetadata(BookItem item)
        => ItemMetadataCatalog.Resolve(item.Tag, item.Name, item.Description, item.Icon);

    private static PlayerStatsViewData ProjectPlayer(PlayerState player)
        => new(
            SkillText: FormatTrack("Skill", player.Skill, player.SkillMax),
            StaminaText: FormatTrack("Stamina", player.Stamina, player.StaminaMax),
            LuckText: FormatTrack("Luck", player.Luck, player.LuckMax),
            RobotSkillText: FormatTrack("Robot Skill", player.RobotSkill, player.RobotSkillMax));

    private static RobotViewData ProjectRobot(RobotState? robot)
    {
        if (robot is null)
        {
            return RobotViewData.None;
        }

        var abilities = robot.Abilities.Count == 0
            ? "Abilities None"
            : $"Abilities {string.Join(", ", robot.Abilities.Select(ability => ability.Name))}";

        return new RobotViewData(
            Name: robot.Name,
            Description: string.IsNullOrWhiteSpace(robot.Description) ? "Ready for action." : robot.Description,
            ArmorText: FormatTrack("Armor", robot.Armor, robot.ArmorMax),
            SpeedText: $"Speed {GetSpeedDisplayName(robot.Speed)}",
            CombatBonusText: FormatTrack("Combat Bonus", robot.CombatBonus, robot.CombatBonusMax),
            AbilitiesText: abilities);
    }

    private static RobotState CreateRobotState(BookRobot robot)
    {
        var state = new RobotState
        {
            Tag = robot.Tag,
            Name = robot.Name,
            Description = robot.Description ?? string.Empty,
            Frame = robot.Frame,
            Armor = robot.Armor,
            ArmorMax = robot.ArmorMax,
            CombatBonus = robot.CombatBonus,
            CombatBonusMax = robot.CombatBonusMax,
            Speed = robot.Speed,
            SpeedMax = robot.SpeedMax,
        };

        foreach (var ability in GetRobotAbilities(robot))
        {
            state.Abilities.Add(ability);
        }

        return state;
    }

    private static IEnumerable<RobotAbilityState> GetRobotAbilities(BookRobot robot)
    {
        if (string.Equals(robot.Name, "Dragonfly", StringComparison.OrdinalIgnoreCase))
        {
            yield return new RobotAbilityState { Name = "Flying" };
        }
    }

    private static void DropCurrentRobot(BlockProgress progress, RobotState? robot)
    {
        if (robot is null)
        {
            return;
        }

        progress.DroppedRobots.Add(robot.Clone());
    }

    private static string GetRobotActionLabel(RobotState? equippedRobot)
        => equippedRobot is null ? "Take robot" : "Swap robot";

    private static string FormatTrack(string label, int current, int maximum)
        => maximum > 0
            ? $"{label} {current}/{maximum}"
            : $"{label} {current}";

    private static string GetLocationDisplayName(WorldLocation location)
        => location switch
        {
            WorldLocation.Unknown => "Unknown",
            WorldLocation.Inherit => "Inherited",
            WorldLocation.Farm => "Farm",
            WorldLocation.CapitalCity => "Capital City",
            WorldLocation.CityOfIndustry => "City of Industry",
            WorldLocation.CityOfKnowledge => "City of Knowledge",
            WorldLocation.CityOfPleasure => "City of Pleasure",
            WorldLocation.CityOfStorms => "City of Storms",
            WorldLocation.CityOfTheGuardians => "City of the Guardians",
            WorldLocation.CityOfTheJungle => "City of the Jungle",
            WorldLocation.CityOfWorship => "City of Worship",
            _ => location.ToString(),
        };

    private static string GetSpeedDisplayName(SpeedBand speed)
        => speed switch
        {
            SpeedBand.Static => "Static",
            SpeedBand.Slow => "Slow",
            SpeedBand.Average => "Average",
            SpeedBand.Fast => "Fast",
            SpeedBand.VeryFast => "Very Fast",
            SpeedBand.UltraFast => "Ultra Fast",
            _ => speed.ToString(),
        };

    private readonly record struct ChoiceAvailability(bool IsVisible, bool IsEnabled)
    {
        public static ChoiceAvailability VisibleEnabled { get; } = new(IsVisible: true, IsEnabled: true);
        public static ChoiceAvailability VisibleDisabled { get; } = new(IsVisible: true, IsEnabled: false);
        public static ChoiceAvailability Hidden { get; } = new(IsVisible: false, IsEnabled: false);
    }

    private sealed class AdventureSession
    {
        private readonly Dictionary<int, BlockProgress> _progressByBlockId = [];

        public AdventureSession(GameState gameState)
        {
            GameState = gameState;
        }

        public GameState GameState { get; }

        public HashSet<int> VisitedBlockIds { get; } = [];

        public HashSet<WorldLocation> VisitedCityLocations { get; } = [];

        public int CurrentBlockId { get; set; }

        public BookBlock? CurrentBlock { get; set; }

        public BlockProgress GetProgress(int blockId)
        {
            if (_progressByBlockId.TryGetValue(blockId, out var progress))
            {
                return progress;
            }

            progress = new BlockProgress();
            _progressByBlockId[blockId] = progress;
            return progress;
        }

        public bool HasActiveEncounter()
        {
            if (CurrentBlock is null)
            {
                return false;
            }

            var progress = GetProgress(CurrentBlock.Id);
            return CurrentBlock.Enemies.Where((_, index) => !progress.DefeatedEnemyIndices.Contains(index)).Any()
                || CurrentBlock.Monsters.Where((_, index) => !progress.DefeatedMonsterIndices.Contains(index)).Any();
        }
    }

    private sealed class BlockProgress
    {
        public HashSet<int> TakenItemIndices { get; } = [];
        public HashSet<int> TakenRobotIndices { get; } = [];
        public List<RobotState> DroppedRobots { get; } = [];
        public HashSet<int> DefeatedEnemyIndices { get; } = [];
        public HashSet<int> DefeatedMonsterIndices { get; } = [];
    }

    private sealed class EncounterState
    {
        public EncounterState(
            string id,
            string kindLabel,
            string name,
            string description,
            BattleOutcome? battleOutcome,
            bool isMonster,
            int index)
        {
            Id = id;
            KindLabel = kindLabel;
            Name = name;
            Description = description;
            BattleOutcome = battleOutcome;
            IsMonster = isMonster;
            Index = index;
        }

        public string Id { get; }

        public string KindLabel { get; }

        public string Name { get; }

        public string Description { get; }

        public BattleOutcome? BattleOutcome { get; }

        public bool IsMonster { get; }

        public int Index { get; }
    }
}
