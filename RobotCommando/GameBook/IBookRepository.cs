namespace RobotCommando.GameBook;

public interface IBookRepository
{
    IReadOnlyList<BookBlock> GetAllBlocks();

    BookBlock GetBlock(int blockId);
}
