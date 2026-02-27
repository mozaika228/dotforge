namespace Dotforge.IL;

public sealed record IlInstruction(int Offset, IlOpCode OpCode, object? Operand = null);
