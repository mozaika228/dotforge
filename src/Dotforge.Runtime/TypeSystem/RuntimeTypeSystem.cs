using Dotforge.Metadata;
using Dotforge.Metadata.Reflection;

namespace Dotforge.Runtime.TypeSystem;

public sealed class RuntimeTypeSystem
{
    private readonly Dictionary<int, RuntimeTypeDescriptor> _typesByToken;
    private readonly Dictionary<string, RuntimeTypeDescriptor> _typesByName;

    private RuntimeTypeSystem(
        Dictionary<int, RuntimeTypeDescriptor> typesByToken,
        Dictionary<string, RuntimeTypeDescriptor> typesByName)
    {
        _typesByToken = typesByToken;
        _typesByName = typesByName;
    }

    public static RuntimeTypeSystem Build(ManagedAssembly assembly)
    {
        var catalog = new MetadataCatalog(assembly);
        var types = catalog.GetTypes();

        var byToken = new Dictionary<int, RuntimeTypeDescriptor>(types.Count);
        var byName = new Dictionary<string, RuntimeTypeDescriptor>(types.Count, StringComparer.Ordinal);

        foreach (var type in types)
        {
            var typeHandle = new RuntimeTypeHandle(type.Token, type.FullName);
            var methods = type.Methods
                .Select(m => new RuntimeMethodDescriptor(
                    Handle: new RuntimeMethodHandle(m.Token, m.DeclaringType, m.Name),
                    ParameterCount: m.ParameterCount,
                    IsStatic: m.IsStatic,
                    GenericArity: m.GenericArity,
                    GenericParameters: m.GenericParameters,
                    ReturnTypeCode: m.ReturnTypeCode))
                .ToArray();

            var descriptor = new RuntimeTypeDescriptor(
                Handle: typeHandle,
                GenericArity: type.GenericArity,
                GenericParameters: type.GenericParameters,
                Methods: methods);

            byToken[type.Token] = descriptor;
            byName[type.FullName] = descriptor;
        }

        return new RuntimeTypeSystem(byToken, byName);
    }

    public RuntimeTypeDescriptor? GetType(int token)
    {
        return _typesByToken.TryGetValue(token, out var type) ? type : null;
    }

    public RuntimeTypeDescriptor? GetType(string fullName)
    {
        return _typesByName.TryGetValue(fullName, out var type) ? type : null;
    }

    public GenericInstantiation InstantiateType(RuntimeTypeHandle genericType, IReadOnlyList<RuntimeTypeHandle> typeArguments)
    {
        if (!_typesByToken.TryGetValue(genericType.Token, out var descriptor))
        {
            throw new KeyNotFoundException($"Type token 0x{genericType.Token:X8} not found.");
        }

        if (descriptor.GenericArity != typeArguments.Count)
        {
            throw new InvalidOperationException($"Type '{descriptor.Handle.FullName}' expects {descriptor.GenericArity} type args, got {typeArguments.Count}.");
        }

        return new GenericInstantiation(genericType, typeArguments);
    }
}
