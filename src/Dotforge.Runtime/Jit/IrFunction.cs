namespace Dotforge.Runtime.Jit;

public sealed class IrFunction
{
    public IrFunction(int methodToken, IReadOnlyList<IrInstruction> instructions, int tempCount)
    {
        MethodToken = methodToken;
        Instructions = instructions;
        TempCount = tempCount;
    }

    public int MethodToken { get; }
    public IReadOnlyList<IrInstruction> Instructions { get; }
    public int TempCount { get; }
}
