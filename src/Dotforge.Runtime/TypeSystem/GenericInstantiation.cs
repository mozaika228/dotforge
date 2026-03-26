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

        return $"{GenericType.FullName}<{string.Join(", ", TypeArguments.Select(x => x.FullName))}>";
    }
}
