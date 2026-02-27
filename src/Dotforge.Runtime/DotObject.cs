using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Dotforge.Runtime.Gc;

namespace Dotforge.Runtime;

internal sealed class DotObject
{
    private readonly GenerationalHeap _heap;
    private readonly Dictionary<string, object?> _fields;

    public DotObject(TypeDefinitionHandle typeHandle, string typeName, IEnumerable<string> fields, GenerationalHeap heap)
    {
        TypeHandle = typeHandle;
        TypeName = typeName;
        _heap = heap;
        _fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var key in fields)
        {
            _fields[key] = null;
        }
    }

    public TypeDefinitionHandle TypeHandle { get; }
    public string TypeName { get; }
    public int Generation { get; private set; }
    public bool Marked { get; private set; }

    public void PromoteToOld()
    {
        Generation = 1;
    }

    public void Mark()
    {
        Marked = true;
    }

    public void ClearMark()
    {
        Marked = false;
    }

    public object? GetField(string fieldKey)
    {
        if (!_fields.TryGetValue(fieldKey, out var value))
        {
            throw new MissingFieldException($"Field '{fieldKey}' is not present on '{TypeName}'.");
        }

        return value;
    }

    public void SetField(string fieldKey, object? value)
    {
        if (!_fields.ContainsKey(fieldKey))
        {
            throw new MissingFieldException($"Field '{fieldKey}' is not present on '{TypeName}'.");
        }

        _fields[fieldKey] = value;
        _heap.WriteBarrier(this, value);
    }

    public IEnumerable<DotObject> EnumerateReferences()
    {
        foreach (var fieldValue in _fields.Values)
        {
            if (fieldValue is DotObject dotObject)
            {
                yield return dotObject;
            }
        }
    }
}
