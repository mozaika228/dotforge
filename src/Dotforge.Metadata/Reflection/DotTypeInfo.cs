namespace Dotforge.Metadata.Reflection;

public sealed record DotTypeInfo(
    int Token,
    string FullName,
    IReadOnlyList<DotFieldInfo> Fields,
    IReadOnlyList<DotMethodInfo> Methods);
