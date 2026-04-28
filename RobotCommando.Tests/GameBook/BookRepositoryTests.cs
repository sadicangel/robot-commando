namespace RobotCommando.Tests.GameBook;

public sealed class BookRepositoryTests
{
    [Test]
    public void GetAllBlocks_LoadsCanonicalBookAndResolvesInheritedLocations()
    {
        var repository = new BookRepository(GetBookPath());

        var blocks = repository.GetAllBlocks();

        blocks.Should().HaveCount(401);
        blocks.Select(block => block.Id).Should().BeInAscendingOrder();

        repository.GetBlock(7).Location.Should().Be(WorldLocation.CapitalCity);
        repository.GetBlock(361).Location.Should().Be(WorldLocation.CityOfKnowledge);
        repository.GetBlock(24).Robots.Should().ContainSingle(robot => robot.Name == "Cowboy");
        repository.GetBlock(39).Monsters.Should().ContainSingle(monster => monster.Name == "Tyrannosaurus");
        repository.GetBlock(39).Text.Should().Be("After several hours of travel, you enter rocky terrain. The path narrows, and you are forced to use both of your robot's hands just to climb. You wish you were in a vehicle that could just fly over all this! Suddenly, you hear a roar. Looking behind you, you see a huge Tyrannosaurus sprinting through the rocks at you! Jaws agape, it lunges towards you, and robot and dinosaur fall to the ground, grappling fiercely. This huge meat-eater is the 'king of the dinosaurs', and attacks anything it sees to feed its savage appetite. You must fight it to the finish.");
        repository.GetBlock(39).Monsters.Single().BattleOutcome!.Lose.Should().Be(258);
        repository.GetBlock(207).Monsters.Should().ContainSingle(monster => monster.Name == "Robot Tyrannosaurus");
    }

    [Test]
    public void LoadBook_LoadsCanonicalBookWithOrderedNormalizedBlocks()
    {
        var document = BookBlockXmlStore.LoadBook(GetBookPath());

        document.Blocks.Should().HaveCount(401);
        document.Blocks.Select(block => block.Id).Should().BeInAscendingOrder();

        var block26 = document.Blocks.Single(block => block.Id == 26);
        block26.Monsters.Should().ContainSingle(monster => monster.Name == "Air Fighter");
        block26.Text.Should().NotContain("AIR-FIGHTER ARMOUR7");
        block26.Choices.Should().BeEmpty();
        block26.Monsters.Single().BattleOutcome!.Lose.Should().Be(136);

        var block39 = document.Blocks.Single(block => block.Id == 39);
        block39.Monsters.Should().ContainSingle(monster => monster.Name == "Tyrannosaurus");
        block39.Text.Should().EndWith("You must fight it to the finish.");
        block39.Choices.Should().BeEmpty();
        block39.Monsters.Single().BattleOutcome!.Lose.Should().Be(258);

        var block207 = document.Blocks.Single(block => block.Id == 207);
        block207.Monsters.Should().ContainSingle(monster => monster.Name == "Robot Tyrannosaurus");
        block207.Text.Should().NotContain("ROBOT TYRANNOSAURUS ARMOUR");
    }

    [Test]
    public void GetAllBlocks_LoadsProvidedCanonicalBook()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var bookPath = Path.Combine(tempDirectory, "book.xml");

            BookBlockXmlStore.SaveBook(bookPath,
            [
                new BookBlock
                {
                    Id = 0,
                    Location = WorldLocation.Farm,
                    Text = "Loaded from canonical book."
                }
            ]);

            var repository = new BookRepository(bookPath);

            repository.GetAllBlocks().Should().ContainSingle();
            repository.GetBlock(0).Text.Should().Be("Loaded from canonical book.");
            repository.GetBlock(0).Location.Should().Be(WorldLocation.Farm);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void GetAllBlocks_ThrowsWhenCanonicalBookIsMissing()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var bookPath = Path.Combine(tempDirectory, "book.xml");
            var repository = new BookRepository(bookPath);

            var act = () => repository.GetAllBlocks();

            act.Should().Throw<FileNotFoundException>()
                .WithMessage($"*{bookPath}*");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string GetBookPath()
        => Path.Combine(GetRepositoryRoot(), "RobotCommando", "BookData", "book.xml");

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "RobotCommando", "BookData", "book.xml");
            if (File.Exists(candidate))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not locate RobotCommando/BookData/book.xml from the test output directory.");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "robot-commando-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
