namespace Dotforge.Metadata.Reflection;

public sealed record DotFieldInfo(
    int Token,
    string Name,
    string DeclaringType,
    bool IsStatic,
    string FieldTypeCode);
