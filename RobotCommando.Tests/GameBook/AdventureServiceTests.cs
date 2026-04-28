using System.Reflection;

namespace RobotCommando.Tests.GameBook;

public sealed class AdventureServiceTests
{
    [Test]
    public async Task StartNewGame_BeginsAtBlockZeroWithStarterInventoryMetadata()
    {
        var service = CreateBundledAdventureService();

        await service.StartNewGame();

        service.Snapshot.BlockId.Should().Be(0);
        service.Snapshot.LocationDisplay.Should().Be("Farm");
        service.Snapshot.Inventory.Should().HaveCount(2);
        service.Snapshot.Inventory.Should().ContainSingle(entry =>
            entry.Name == "Medikit"
            && entry.Quantity == 5
            && entry.Description == "Use: +1 stamina."
            && entry.Icon == "medikit.svg"
            && entry.IconGlyph == "\u271A");
        service.Snapshot.Inventory.Should().ContainSingle(entry =>
            entry.Name == "Sword"
            && entry.Quantity == 1
            && entry.Description == "Your trustworthy weapon."
            && entry.Icon == "sword.svg"
            && entry.IconGlyph == "\u2694");
        service.Snapshot.Choices.Should().ContainSingle(choice => choice.TargetBlockId == 1 && choice.IsEnabled);
    }

    [Test]
    public async Task OpeningFlow_AllowsRobotSelectionAndUnlocksConditionalChoice()
    {
        var service = CreateBundledAdventureService();

        await service.StartNewGame();
        await service.SelectChoice("choice:0");
        await service.SelectChoice("choice:0");

        service.Snapshot.BlockId.Should().Be(24);
        service.Snapshot.Interactions.Should().ContainSingle(interaction => interaction.KindLabel == "Robot" && interaction.Title == "Cowboy");
        service.Snapshot.Choices.Should().Contain(choice => choice.TargetBlockId == 70 && !choice.IsEnabled);

        await service.TakeRobot("robot:0");

        service.Snapshot.Robot.Name.Should().Be("Cowboy");
        service.Snapshot.Choices.Should().Contain(choice => choice.TargetBlockId == 70 && choice.IsEnabled);
        service.Snapshot.Interactions.Should().BeEmpty();
    }

    [Test]
    public async Task PickingUpItem_AddsInventoryAndRemovesInteraction()
    {
        var repository = new TestBookRepository(
            new BookBlock
            {
                Id = 0,
                Location = WorldLocation.Farm,
                Text = "A storeroom."
            },
            new BookBlock
            {
                Id = 1,
                Location = WorldLocation.Farm,
                Text = "Unused."
            });

        repository.GetBlock(0).Items.Add(new BookItem
        {
            Tag = "Interface Transponder",
            Name = "Interface Transponder",
            Description = "+1 skill with robots."
        });

        var service = new AdventureService(repository);

        await service.StartNewGame();
        service.Snapshot.Interactions.Should().ContainSingle(interaction =>
            interaction.Id == "item:0"
            && interaction.Title == "Interface Transponder"
            && interaction.Description == "+1 skill with robots."
            && interaction.ToolTipText == "+1 skill with robots."
            && interaction.IconGlyph == "\u25C8");

        await service.PickUpItem("item:0");

        service.Snapshot.Inventory.Should().Contain(entry =>
            entry.Name == "Interface Transponder"
            && entry.Quantity == 1
            && entry.Description == "+1 skill with robots."
            && entry.IconGlyph == "\u25C8");
        service.Snapshot.Interactions.Should().BeEmpty();
    }

    [Test]
    public async Task PickingUpItem_RunsAcquireEffect()
    {
        var block = new BookBlock
        {
            Id = 0,
            Location = WorldLocation.Farm,
            Text = "A storeroom."
        };
        block.Items.Add(new BookItem
        {
            Tag = "Interface Transponder",
            Name = "Interface Transponder",
            OnAcquire = new ItemTrigger
            {
                Effect = "context.Player.RobotSkill++"
            }
        });

        var service = new AdventureService(new TestBookRepository(block));

        await service.StartNewGame();
        await service.PickUpItem("item:0");

        GetGameState(service).Player.RobotSkill.Should().Be(1);
    }

    [Test]
    public async Task SelectingChoice_RunsEffectBeforeResolvingTarget()
    {
        var start = new BookBlock
        {
            Id = 0,
            Location = WorldLocation.Farm,
            Text = "Choose."
        };
        start.Choices.Add(new BookChoice
        {
            To = 1,
            Text = "Roll onward.",
            Effect = "context.Page.Choices[0].To = 2"
        });

        var originalTarget = new BookBlock
        {
            Id = 1,
            Location = WorldLocation.Farm,
            Text = "Original."
        };
        var redirectedTarget = new BookBlock
        {
            Id = 2,
            Location = WorldLocation.Farm,
            Text = "Redirected."
        };

        var service = new AdventureService(new TestBookRepository(start, originalTarget, redirectedTarget));

        await service.StartNewGame();
        await service.SelectChoice("choice:0");

        service.Snapshot.BlockId.Should().Be(2);
        service.Snapshot.PageText.Should().Be("Redirected.");
    }

    [Test]
    public async Task RevisitingCity_AfterLeaving_UsesRevisitTextAndVisitedChoiceFiltering()
    {
        var cityHub = new BookBlock
        {
            Id = 0,
            Location = WorldLocation.CityOfKnowledge,
            Text = "You arrive for the first time.",
            RevisitText = "You are back again."
        };
        cityHub.Choices.Add(new BookChoice
        {
            To = 1,
            Text = "Explore the square.",
            Condition = "!context.City.IsVisited"
        });
        cityHub.Choices.Add(new BookChoice
        {
            To = 3,
            Text = "Visit the archive.",
            Condition = "context.City.IsVisited"
        });

        var sameCityStreet = new BookBlock
        {
            Id = 1,
            Location = WorldLocation.CityOfKnowledge,
            Text = "An empty street."
        };
        sameCityStreet.Choices.Add(new BookChoice
        {
            To = 0,
            Text = "Return to the square."
        });
        sameCityStreet.Choices.Add(new BookChoice
        {
            To = 2,
            Text = "Leave the city."
        });

        var outsideCity = new BookBlock
        {
            Id = 2,
            Location = WorldLocation.Unknown,
            Text = "Outside the city."
        };
        outsideCity.Choices.Add(new BookChoice
        {
            To = 0,
            Text = "Return to the city."
        });

        var revisitDestination = new BookBlock
        {
            Id = 3,
            Location = WorldLocation.CityOfKnowledge,
            Text = "Archive."
        };

        var service = new AdventureService(new TestBookRepository(cityHub, sameCityStreet, outsideCity, revisitDestination));

        await service.StartNewGame();
        service.Snapshot.PageText.Should().Be("You arrive for the first time.");
        service.Snapshot.Choices.Select(choice => choice.Text).Should().Equal("Explore the square.");

        await service.SelectChoice("choice:0");
        await service.SelectChoice("choice:0");

        service.Snapshot.BlockId.Should().Be(0);
        service.Snapshot.PageText.Should().Be("You arrive for the first time.");
        service.Snapshot.Choices.Select(choice => choice.Text).Should().Equal("Explore the square.");

        await service.SelectChoice("choice:0");
        await service.SelectChoice("choice:1");
        await service.SelectChoice("choice:0");

        service.Snapshot.BlockId.Should().Be(0);
        service.Snapshot.PageText.Should().Be("You are back again.");
        service.Snapshot.Choices.Select(choice => choice.Text).Should().Equal("Visit the archive.");
    }

    [Test]
    public async Task RevisitingSameBlock_BeforeLeavingCity_DoesNotCountAsCityRevisit()
    {
        var cityHub = new BookBlock
        {
            Id = 0,
            Location = WorldLocation.CityOfKnowledge,
            Text = "First city text.",
            RevisitText = "City revisit text."
        };
        cityHub.Choices.Add(new BookChoice
        {
            To = 1,
            Text = "Walk around.",
            Condition = "!context.City.IsVisited"
        });
        cityHub.Choices.Add(new BookChoice
        {
            To = 2,
            Text = "Use revisit route.",
            Condition = "context.City.IsVisited"
        });

        var loop = new BookBlock
        {
            Id = 1,
            Location = WorldLocation.Inherit,
            Text = "Still in the same city."
        };
        loop.Choices.Add(new BookChoice
        {
            To = 0,
            Text = "Loop back."
        });

        var revisitRoute = new BookBlock
        {
            Id = 2,
            Location = WorldLocation.CityOfKnowledge,
            Text = "Revisit route."
        };

        var service = new AdventureService(new TestBookRepository(cityHub, loop, revisitRoute));

        await service.StartNewGame();
        await service.SelectChoice("choice:0");
        await service.SelectChoice("choice:0");

        service.Snapshot.BlockId.Should().Be(0);
        service.Snapshot.PageText.Should().Be("First city text.");
        service.Snapshot.Choices.Select(choice => choice.Text).Should().Equal("Walk around.");
    }

    [Test]
    public async Task Fight_HandlesSequentialAutoWinsAndEscapeRoutes()
    {
        var encounterBlock = new BookBlock
        {
            Id = 0,
            Location = WorldLocation.CityOfKnowledge,
            Text = "Three beasts block your path."
        };
        encounterBlock.Enemies.Add(new BookEnemy
        {
            Tag = "Enemy",
            Name = "Lizard I",
            BattleOutcome = new BattleOutcome
            {
                Win = -1,
                WinSpecified = true
            }
        });
        encounterBlock.Enemies.Add(new BookEnemy
        {
            Tag = "Enemy",
            Name = "Lizard II",
            BattleOutcome = new BattleOutcome
            {
                Win = -1,
                WinSpecified = true
            }
        });
        encounterBlock.Enemies.Add(new BookEnemy
        {
            Tag = "Enemy",
            Name = "Lizard III",
            BattleOutcome = new BattleOutcome
            {
                Win = 8,
                WinSpecified = true,
                Escape = 9,
                EscapeSpecified = true
            }
        });

        var winBlock = new BookBlock
        {
            Id = 8,
            Location = WorldLocation.CityOfKnowledge,
            Text = "Victory."
        };

        var escapeBlock = new BookBlock
        {
            Id = 9,
            Location = WorldLocation.CityOfKnowledge,
            Text = "Escape."
        };

        var service = new AdventureService(new TestBookRepository(encounterBlock, winBlock, escapeBlock));

        await service.StartNewGame();
        service.Snapshot.Choices.Should().BeEmpty();
        service.Snapshot.Interactions.Should().ContainSingle(interaction => interaction.Title == "Lizard I" && !interaction.CanEscape);

        await service.Fight("enemy:0");
        service.Snapshot.BlockId.Should().Be(0);
        service.Snapshot.Interactions.Should().ContainSingle(interaction => interaction.Title == "Lizard II" && !interaction.CanEscape);

        await service.Fight("enemy:1");
        service.Snapshot.Interactions.Should().ContainSingle(interaction => interaction.Title == "Lizard III" && interaction.CanEscape);

        await service.Escape("enemy:2");
        service.Snapshot.BlockId.Should().Be(9);
        service.Snapshot.PageText.Should().Be("Escape.");
    }

    [Test]
    public async Task AdventureModel_RefreshesStateAfterCommands()
    {
        var start = new BookBlock
        {
            Id = 0,
            Location = WorldLocation.Farm,
            Text = "Start."
        };
        start.Choices.Add(new BookChoice
        {
            To = 1,
            Text = "Continue."
        });

        var next = new BookBlock
        {
            Id = 1,
            Location = WorldLocation.Farm,
            Text = "Next."
        };

        var service = new AdventureService(new TestBookRepository(start, next));
        await service.StartNewGame();

        var model = new AdventureModel(service);
        model.State.BlockId.Should().Be(0);

        await model.SelectChoice("choice:0");

        model.State.BlockId.Should().Be(1);
        model.State.PageText.Should().Be("Next.");
    }

    [Test]
    public async Task TakingRobotWhileAnotherIsEquipped_DropsItOnThePageAndPreservesItsStats()
    {
        var workshop = new BookBlock
        {
            Id = 0,
            Location = WorldLocation.Farm,
            Text = "A crowded workshop."
        };
        workshop.Robots.Add(new BookRobot
        {
            Tag = "Robot",
            Name = "Cowboy",
            Description = "A sturdy utility robot.",
            Frame = RobotFrame.Humanoid,
            Armor = 8,
            ArmorMax = 8,
            Speed = SpeedBand.Fast,
            SpeedMax = SpeedBand.Fast,
            CombatBonus = 2,
            CombatBonusMax = 2,
        });
        workshop.Robots.Add(new BookRobot
        {
            Tag = "Robot",
            Name = "Dragonfly",
            Description = "A light flying robot.",
            Frame = RobotFrame.Humanoid,
            Armor = 5,
            ArmorMax = 5,
            Speed = SpeedBand.UltraFast,
            SpeedMax = SpeedBand.UltraFast,
            CombatBonus = 1,
            CombatBonusMax = 1,
        });
        workshop.Choices.Add(new BookChoice
        {
            To = 1,
            Text = "Leave the workshop."
        });

        var road = new BookBlock
        {
            Id = 1,
            Location = WorldLocation.Farm,
            Text = "The road outside."
        };
        road.Choices.Add(new BookChoice
        {
            To = 0,
            Text = "Return to the workshop."
        });

        var service = new AdventureService(new TestBookRepository(workshop, road));

        await service.StartNewGame();
        await service.TakeRobot("robot:0");

        var gameState = GetGameState(service);
        gameState.Robot.Should().NotBeNull();
        gameState.Robot!.Armor = 3;
        gameState.Robot.Speed = SpeedBand.Slow;

        await service.TakeRobot("robot:1");

        service.Snapshot.Robot.Name.Should().Be("Dragonfly");
        service.Snapshot.Interactions.Should().ContainSingle(interaction =>
            interaction.Id == "robot:dropped:0"
            && interaction.Title == "Cowboy"
            && interaction.PrimaryActionLabel == "Swap robot");

        await service.SelectChoice("choice:0");
        await service.SelectChoice("choice:0");

        service.Snapshot.BlockId.Should().Be(0);
        service.Snapshot.Interactions.Should().ContainSingle(interaction =>
            interaction.Id == "robot:dropped:0"
            && interaction.Title == "Cowboy");

        await service.TakeRobot("robot:dropped:0");

        service.Snapshot.Robot.Name.Should().Be("Cowboy");
        service.Snapshot.Robot.ArmorText.Should().Be("Armor 3/8");
        service.Snapshot.Robot.SpeedText.Should().Be("Speed Slow");
        service.Snapshot.Interactions.Should().ContainSingle(interaction =>
            interaction.Id == "robot:dropped:0"
            && interaction.Title == "Dragonfly");
    }

    private static AdventureService CreateBundledAdventureService()
        => new(new BookRepository(GetBookPath()));

    private static GameState GetGameState(AdventureService service)
    {
        var sessionField = typeof(AdventureService).GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic);
        sessionField.Should().NotBeNull();

        var session = sessionField!.GetValue(service);
        session.Should().NotBeNull();

        var gameStateProperty = session!.GetType().GetProperty("GameState", BindingFlags.Instance | BindingFlags.Public);
        gameStateProperty.Should().NotBeNull();

        return (GameState)(gameStateProperty!.GetValue(session) ?? throw new InvalidOperationException("Adventure session game state was null."));
    }

    private static string GetBookPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "RobotCommando", "BookData", "book.xml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not locate RobotCommando/BookData/book.xml from the test output directory.");
    }

    private sealed class TestBookRepository : IBookRepository
    {
        private readonly ImmutableDictionary<int, BookBlock> _blocksById;
        private readonly ImmutableArray<BookBlock> _orderedBlocks;

        public TestBookRepository(params BookBlock[] blocks)
        {
            _orderedBlocks = blocks.OrderBy(block => block.Id).ToImmutableArray();
            _blocksById = _orderedBlocks.ToImmutableDictionary(block => block.Id);
        }

        public IReadOnlyList<BookBlock> GetAllBlocks() => _orderedBlocks;

        public BookBlock GetBlock(int blockId) => _blocksById[blockId];
    }
}
