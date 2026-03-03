namespace Dotforge.Runtime;

internal sealed class DotArray
{
    private readonly object?[] _items;

    public DotArray(string elementTypeName, int length)
    {
        if (length < 0)
        {
            throw new OverflowException("Array length cannot be negative.");
        }

        ElementTypeName = elementTypeName;
        _items = new object?[length];
    }

    public string ElementTypeName { get; }
    public int Length => _items.Length;

    public object? Get(int index)
    {
        EnsureIndex(index);
        return _items[index];
    }

    public void Set(int index, object? value)
    {
        EnsureIndex(index);
        _items[index] = value;
    }

    private void EnsureIndex(int index)
    {
        if (index < 0 || index >= _items.Length)
        {
            throw new IndexOutOfRangeException($"Array index {index} is out of range.");
        }
    }
}
