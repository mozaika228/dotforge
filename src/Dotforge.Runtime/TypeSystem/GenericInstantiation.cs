namespace Dotforge.Runtime.TypeSystem;

public sealed record GenericInstantiation(
    RuntimeTypeHandle GenericType,
    IReadOnlyList<RuntimeTypeHandle> TypeArguments)
{
    public override string ToString()
    {
        if (TypeArguments.Count == 0)
        {
            return GenericType.FullName;
        }

        var printable = RuntimeTypeSystem.NormalizeMetadataTypeName(GenericType.FullName);
        return $"{printable}<{string.Join(", ", TypeArguments.Select(x => x.FullName))}>";
    }
}
