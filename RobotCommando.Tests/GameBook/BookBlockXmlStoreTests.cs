using RobotCommando.GameBook;

namespace RobotCommando.Tests.GameBook;

public sealed class BookBlockXmlStoreTests
{
    [Test]
    public void LoadDirectory_LoadsBundledBlocks()
    {
        var blocks = BookBlockXmlStore.LoadDirectory(GetBlockDirectory());

        blocks.Should().HaveCount(401);
        blocks.Select(block => block.Id).Should().BeInAscendingOrder();

        var cityOfKnowledge = blocks.Single(block => block.Id == 361);
        cityOfKnowledge.RevisitText.Should().Be("You are in the City of Knowledge.");
        cityOfKnowledge.Choices.Should().HaveCount(6);

        var interfaceTransponder = blocks.Single(block => block.Id == 392).Items.Single();
        interfaceTransponder.OnAcquire!.Effect!.Text.Should().Be("context.Player.RobotSkill++");

        var giantLizards = blocks.Single(block => block.Id == 328).Enemies;
        giantLizards.Should().HaveCount(3);
        giantLizards.Last().BattleOutcome!.Escape.Should().Be(254);
    }

    [Test]
    public void Save_RoundTripsABlock()
    {
        var directory = GetBlockDirectory();
        var original = BookBlockXmlStore.Load(Path.Combine(directory, "0024.xml"));
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

    private static string GetBlockDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "RobotCommando", "BookData", "Blocks");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate RobotCommando/BookData/Blocks from the test output directory.");
    }
}
