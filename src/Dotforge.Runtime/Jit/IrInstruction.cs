namespace Dotforge.Runtime.Jit;

public sealed record IrInstruction(string Op, string? Dest = null, string? Left = null, string? Right = null);
