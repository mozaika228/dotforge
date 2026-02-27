namespace Dotforge.Metadata.Reflection;

public sealed record DotMethodInfo(
    int Token,
    string Name,
    string DeclaringType,
    int ParameterCount,
    bool IsStatic,
    string ReturnTypeCode);
