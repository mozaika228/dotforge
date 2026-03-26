namespace Dotforge.Runtime.TypeSystem;

public sealed record RuntimeTypeDescriptor(
    RuntimeTypeHandle Handle,
    int GenericArity,
    IReadOnlyList<string> GenericParameters,
    IReadOnlyList<RuntimeMethodDescriptor> Methods);
