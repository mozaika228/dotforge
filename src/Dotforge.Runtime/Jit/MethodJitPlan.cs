using Dotforge.IL;
using Dotforge.Runtime.Jit.Backend;
using Dotforge.Runtime.Jit.Passes;

namespace Dotforge.Runtime.Jit;

public static class MethodJitPlan
{
    public static JitCompilationPlan Create(int methodToken, IlMethodBody body)
    {
        var initial = IlToIrLowerer.Lower(methodToken, body);
        var manager = new IrPassManager(
            new ConstantFoldingPass(),
            new DeadCodeEliminationPass());
        var optimized = manager.Run(initial);
        var asm = PseudoX64Lowering.Lower(optimized);
        return new JitCompilationPlan(initial, optimized, asm);
    }
}
