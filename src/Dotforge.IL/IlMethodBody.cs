namespace Dotforge.IL;

public sealed class IlMethodBody
{
    public IlMethodBody(IReadOnlyList<IlInstruction> instructions)
    {
        Instructions = instructions;
    }

    public IReadOnlyList<IlInstruction> Instructions { get; }
}
