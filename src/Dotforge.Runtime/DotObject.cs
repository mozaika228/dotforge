using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotforge.Runtime;

internal sealed class DotObject
{
    private readonly Dictionary<string, object?> _fields;

    public DotObject(TypeDefinitionHandle typeHandle, string typeName, IEnumerable<string> fields)
    {
        TypeHandle = typeHandle;
        TypeName = typeName;
        _fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var key in fields)
        {
            _fields[key] = null;
        }
    }

    public TypeDefinitionHandle TypeHandle { get; }
    public string TypeName { get; }

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
    }
}
