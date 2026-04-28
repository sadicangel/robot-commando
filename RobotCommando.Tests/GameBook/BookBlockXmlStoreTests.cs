using RobotCommando.GameBook;

namespace RobotCommando.Tests.GameBook;

public sealed class BookBlockXmlStoreTests
{
    [Test]
    public void LoadBook_LoadsBundledBook()
    {
        var blocks = BookBlockXmlStore.LoadBook(GetBookPath()).Blocks;

        blocks.Should().HaveCount(401);
        blocks.Select(block => block.Id).Should().BeInAscendingOrder();

        var cityOfKnowledge = blocks.Single(block => block.Id == 361);
        cityOfKnowledge.RevisitText.Should().Be("You are back in the City of Knowledge.");
        cityOfKnowledge.Choices.Should().HaveCount(6);

        var interfaceTransponder = blocks.Single(block => block.Id == 392).Items.Single();
        interfaceTransponder.OnAcquire!.Effect!.Text.Should().Be("context.Player.RobotSkill++");

        blocks.Single(block => block.Id == 11).Items.Single(item => item.Tag == "Medikit").Quantity.Should().Be(2);
        blocks.SelectMany(block => block.Items)
            .Select(item => item.Tag)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Should()
            .BeEquivalentTo(
            [
                "Armor Plate",
                "Blue Potion",
                "City of Guardians Location",
                "Cloak Model Reference",
                "Cloak of Invisibility",
                "Interface Transponder",
                "Karossean Book Reference",
                "Karossean Countersign",
                "Karossean Password",
                "Karossean Uniform",
                "Lavender Potion",
                "Luck Amulet",
                "Medikit",
                "Seeker Missile",
                "Sword of State",
                "Tangler Field",
            ]);

        var giantLizards = blocks.Single(block => block.Id == 328).Enemies;
        giantLizards.Should().HaveCount(3);
        giantLizards.Last().BattleOutcome!.Escape.Should().Be(254);
    }

    [Test]
    public void Save_RoundTripsABlock()
    {
        var original = BookBlockXmlStore.LoadBook(GetBookPath()).Blocks.Single(block => block.Id == 24);
        var tempPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid():N}.xml");

        try
        {
            BookBlockXmlStore.Save(tempPath, original);
            var reloaded = BookBlockXmlStore.Load(tempPath);

            reloaded.Id.Should().Be(24);
            reloaded.Location.Should().Be(WorldLocation.Farm);
            reloaded.Robots.Should().ContainSingle();
            reloaded.Choices.Should().HaveCount(2);
            reloaded.Choices.Last().Condition!.Text.Should().Be("context.Robot is not null");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Test]
    public void Save_RoundTripsInheritedLocation()
    {
        var block = new BookBlock
        {
            Id = 7,
            Location = WorldLocation.Inherit,
            Text = "Countersign check."
        };
        var tempPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid():N}.xml");

        try
        {
            BookBlockXmlStore.Save(tempPath, block);
            var reloaded = BookBlockXmlStore.Load(tempPath);

            reloaded.Location.Should().Be(WorldLocation.Inherit);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
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
}
