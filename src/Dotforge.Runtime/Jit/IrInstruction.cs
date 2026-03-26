namespace Dotforge.Runtime.Jit;

public sealed record IrInstruction(
    IrOpCode OpCode,
    int? Dest = null,
    int? Left = null,
    int? Right = null,
    int? Immediate = null,
    int? LocalIndex = null,
    int? ArgIndex = null,
    string? Label = null);
