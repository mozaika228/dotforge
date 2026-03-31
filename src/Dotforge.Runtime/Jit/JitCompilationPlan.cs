namespace Dotforge.Runtime.Jit;

public sealed class JitCompilationPlan
{
    public JitCompilationPlan(
        IrFunction initialIr,
        IrFunction optimizedIr,
        IReadOnlyList<string> loweredPseudoAsm,
        bool isExecutable)
    {
        InitialIr = initialIr;
        OptimizedIr = optimizedIr;
        LoweredPseudoAsm = loweredPseudoAsm;
        IsExecutable = isExecutable;
    }

    public IrFunction InitialIr { get; }
    public IrFunction OptimizedIr { get; }
    public IReadOnlyList<string> LoweredPseudoAsm { get; }
    public bool IsExecutable { get; }
}
