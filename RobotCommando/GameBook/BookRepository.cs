namespace RobotCommando.GameBook;

public sealed class BookRepository : IBookRepository
{
    private readonly Lazy<ImmutableArray<BookBlock>> _blocks;
    private readonly Lazy<ImmutableDictionary<int, BookBlock>> _blocksById;
    private readonly string _bookPath;
    private readonly string? _monsterCatalogPath;

    public BookRepository()
        : this(
            Path.Combine(AppContext.BaseDirectory, "BookData", "book.xml"),
            Path.Combine(AppContext.BaseDirectory, "BookData", "Legacy", "monsters.json"))
    {
    }

    public BookRepository(string bookPath)
        : this(bookPath, InferMonsterCatalogPath(bookPath))
    {
    }

    public BookRepository(string bookPath, string? monsterCatalogPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookPath);

        _bookPath = bookPath;
        _monsterCatalogPath = monsterCatalogPath;
        _blocks = new Lazy<ImmutableArray<BookBlock>>(LoadBlocks, LazyThreadSafetyMode.ExecutionAndPublication);
        _blocksById = new Lazy<ImmutableDictionary<int, BookBlock>>(
            () => _blocks.Value.ToImmutableDictionary(block => block.Id),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyList<BookBlock> GetAllBlocks() => _blocks.Value;

    public BookBlock GetBlock(int blockId)
    {
        if (_blocksById.Value.TryGetValue(blockId, out var block))
        {
            return block;
        }

        throw new KeyNotFoundException($"Could not find block {blockId}.");
    }

    private ImmutableArray<BookBlock> LoadBlocks()
    {
        if (!File.Exists(_bookPath))
        {
            throw new FileNotFoundException($"Could not locate canonical book file '{_bookPath}'.", _bookPath);
        }

        var loadedBlocks = BookBlockXmlStore.LoadBook(_bookPath).Blocks
            .OrderBy(block => block.Id)
            .ToArray();
        var blocks = new BookEncounterNormalizer(_monsterCatalogPath).Normalize(loadedBlocks);
        var resolvedBlocks = ImmutableArray.CreateBuilder<BookBlock>(blocks.Length);
        var lastResolvedLocation = WorldLocation.Unknown;

        foreach (var block in blocks)
        {
            if (block.Location == WorldLocation.Inherit)
            {
                block.Location = lastResolvedLocation;
            }
            else
            {
                lastResolvedLocation = block.Location;
            }

            resolvedBlocks.Add(block);
        }

        return resolvedBlocks.MoveToImmutable();
    }

    private static string? InferMonsterCatalogPath(string bookPath)
    {
        if (string.IsNullOrWhiteSpace(bookPath))
        {
            return null;
        }

        var bookDataDirectory = Path.GetDirectoryName(bookPath);
        var candidate = string.IsNullOrWhiteSpace(bookDataDirectory)
            ? null
            : Path.Combine(bookDataDirectory, "Legacy", "monsters.json");

        return !string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate)
            ? candidate
            : null;
    }
}
