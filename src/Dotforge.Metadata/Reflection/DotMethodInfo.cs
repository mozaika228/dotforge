namespace Dotforge.Metadata.Reflection;

public sealed record DotMethodInfo(
    int Token,
    string Name,
    string DeclaringType,
    int ParameterCount,
    int GenericArity,
    IReadOnlyList<string> GenericParameters,
    bool IsStatic,
    string ReturnTypeCode);
