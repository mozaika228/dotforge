namespace Dotforge.Runtime.TypeSystem;

public sealed record RuntimeMethodDescriptor(
    RuntimeMethodHandle Handle,
    int ParameterCount,
    bool IsStatic,
    int GenericArity,
    IReadOnlyList<string> GenericParameters,
    string ReturnTypeCode);
