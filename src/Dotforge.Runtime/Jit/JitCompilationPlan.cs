namespace Dotforge.Runtime.Jit;

public sealed class JitCompilationPlan
{
    public JitCompilationPlan(IrFunction initialIr, IrFunction optimizedIr, IReadOnlyList<string> loweredPseudoAsm)
    {
        InitialIr = initialIr;
        OptimizedIr = optimizedIr;
        LoweredPseudoAsm = loweredPseudoAsm;
    }

    public IrFunction InitialIr { get; }
    public IrFunction OptimizedIr { get; }
    public IReadOnlyList<string> LoweredPseudoAsm { get; }
}
