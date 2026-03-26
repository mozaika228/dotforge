using Dotforge.Runtime.Gc;

namespace Dotforge.Runtime;

internal sealed class DotArray
    : IHeapObject
{
    private readonly GenerationalHeap _heap;
    private readonly object?[] _items;
    private bool _marked;
    private int _generation;

    public DotArray(string elementTypeName, int length, GenerationalHeap heap)
    {
        if (length < 0)
        {
            throw new OverflowException("Array length cannot be negative.");
        }

        _heap = heap;
        ElementTypeName = elementTypeName;
        _items = new object?[length];
    }

    public string ElementTypeName { get; }
    public int Length => _items.Length;
    public int Generation => _generation;
    public bool Marked => _marked;
    public int EstimatedSizeBytes => 32 + (_items.Length * 8);

    public object? Get(int index)
    {
        EnsureIndex(index);
        return _items[index];
    }

    public void Set(int index, object? value)
    {
        EnsureIndex(index);
        _items[index] = value;
        _heap.WriteBarrier(this, value);
    }

    public void Mark()
    {
        _marked = true;
    }

    public void ClearMark()
    {
        _marked = false;
    }

    public void SetGeneration(int generation)
    {
        _generation = generation;
    }

    public IEnumerable<IHeapObject> EnumerateReferences()
    {
        foreach (var item in _items)
        {
            if (item is IHeapObject heapObject)
            {
                yield return heapObject;
            }
        }
    }

    private void EnsureIndex(int index)
    {
        if (index < 0 || index >= _items.Length)
        {
            throw new IndexOutOfRangeException($"Array index {index} is out of range.");
        }
    }
}
