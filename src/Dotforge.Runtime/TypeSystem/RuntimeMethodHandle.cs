namespace Dotforge.Runtime.TypeSystem;

public readonly record struct RuntimeMethodHandle(int Token, string DeclaringType, string Name);
