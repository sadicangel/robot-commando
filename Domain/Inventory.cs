using System.Collections;

namespace RobotCommando;

public sealed record class Inventory(
    Guid Id,
    string Tag,
    string Name,
    string Description,
    string Icon,
    int Size)
: IInventory
{
    private readonly List<Item>[] _slots = new Func<int, List<Item>[]>(static (int size) =>
    {
        var slots = new List<Item>[size];
        for (int i = 0; i < slots.Length; ++i)
            slots[i] = [];
        return slots;
    }).Invoke(Size);

    public IReadOnlyList<Item> this[int index] { get => _slots[index]; }

    public int Count { get => _slots.Length; }

    public event Action<Inventory, Item>? ItemAdded;
    public event Action<Inventory, Item>? ItemRemoved;
    public event Action<Inventory, SwapIndices>? ItemMoved;

    private int FindFreeSlotIndex()
    {
        var index = -1;
        for (int i = 0; i < _slots.Length; ++i)
        {
            if (_slots[i].Count == 0)
            {
                index = i;
                break;
            }
        }
        return index;
    }

    private int FindItemSlotIndex(Item item)
    {
        var index = -1;
        for (int i = 0; i < _slots.Length; ++i)
        {
            if (_slots[i] is [var top, ..] && top.Tag == item.Tag)
            {
                index = i;
                break;
            }
        }
        return index;
    }

    public void Add(Item item)
    {
        var index = FindItemSlotIndex(item);
        if (index < 0)
            index = FindFreeSlotIndex();
        if (index < 0)
            throw new InvalidOperationException("Inventory size exceeded");
        _slots[index].Add(item);
        ItemAdded?.Invoke(this, item);
    }

    public void Remove(Item item)
    {
        var index = FindItemSlotIndex(item);
        if (index < 0)
            throw new InvalidOperationException("Cannot remove item");

        _slots[index].Remove(item);
        ItemRemoved?.Invoke(this, item);
    }

    public bool Contains(Item item) => FindItemSlotIndex(item) >= 0;

    public void Swap(SwapIndices indices)
    {
        var (i1, i2) = indices;
        (_slots[i1], _slots[i2]) = (_slots[i2], _slots[i1]);
        ItemMoved?.Invoke(this, indices);
    }

    public IEnumerator<IReadOnlyList<Item>> GetEnumerator()
    {
        foreach (var list in _slots)
            yield return list;
    }

    IEnumerator IEnumerable.GetEnumerator() => _slots.GetEnumerator();
}
