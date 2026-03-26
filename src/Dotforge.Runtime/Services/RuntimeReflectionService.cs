using Dotforge.Metadata.Reflection;

namespace Dotforge.Runtime.Services;

public sealed class RuntimeReflectionService
{
    private readonly IReadOnlyList<DotTypeInfo> _types;
    private readonly Dictionary<int, DotTypeInfo> _typesByToken;
    private readonly Dictionary<string, DotTypeInfo> _typesByName;

    public RuntimeReflectionService(IReadOnlyList<DotTypeInfo> types)
    {
        _types = types;
        _typesByToken = types.ToDictionary(static t => t.Token);
        _typesByName = types.ToDictionary(static t => t.FullName, StringComparer.Ordinal);
    }

    public IReadOnlyList<DotTypeInfo> GetTypes() => _types;

    public DotTypeInfo? GetType(int token)
    {
        return _typesByToken.TryGetValue(token, out var type) ? type : null;
    }

    public DotTypeInfo? GetType(string fullName)
    {
        return _typesByName.TryGetValue(fullName, out var type) ? type : null;
    }

    public IReadOnlyList<DotMethodInfo> GetMethods(string fullTypeName)
    {
        return GetType(fullTypeName)?.Methods ?? [];
    }

    public IReadOnlyList<DotFieldInfo> GetFields(string fullTypeName)
    {
        return GetType(fullTypeName)?.Fields ?? [];
    }
}
