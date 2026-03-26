namespace Dotforge.Metadata.Reflection;

public sealed record DotTypeInfo(
    int Token,
    string FullName,
    int GenericArity,
    IReadOnlyList<string> GenericParameters,
    IReadOnlyList<DotFieldInfo> Fields,
    IReadOnlyList<DotMethodInfo> Methods);
