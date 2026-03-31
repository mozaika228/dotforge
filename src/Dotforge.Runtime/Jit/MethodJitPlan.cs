using Dotforge.IL;
using Dotforge.Runtime.Jit.Backend;
using Dotforge.Runtime.Jit.Passes;

namespace Dotforge.Runtime.Jit;

public static class MethodJitPlan
{
    public static JitCompilationPlan Create(int methodToken, IlMethodBody body, bool hasExceptionRegions = false)
    {
        var initial = IlToIrLowerer.Lower(methodToken, body);
        var manager = new IrPassManager(
            new ConstantFoldingPass(),
            new DeadCodeEliminationPass());
        var optimized = manager.Run(initial);
        var asm = PseudoX64Lowering.Lower(optimized);
        var isExecutable = !hasExceptionRegions && body.Instructions.All(IsSupportedExecutableOpcode);
        return new JitCompilationPlan(initial, optimized, asm, isExecutable);
    }

    private static bool IsSupportedExecutableOpcode(IlInstruction instruction)
    {
        return instruction.OpCode switch
        {
            IlOpCode.Nop => true,
            IlOpCode.Dup => true,
            IlOpCode.Pop => true,

            IlOpCode.Ldarg0 => true,
            IlOpCode.Ldarg1 => true,
            IlOpCode.Ldarg2 => true,
            IlOpCode.Ldarg3 => true,
            IlOpCode.LdargS => true,
            IlOpCode.Ldarg => true,

            IlOpCode.Ldloc0 => true,
            IlOpCode.Ldloc1 => true,
            IlOpCode.Ldloc2 => true,
            IlOpCode.Ldloc3 => true,
            IlOpCode.LdlocS => true,
            IlOpCode.Ldloc => true,
            IlOpCode.Stloc0 => true,
            IlOpCode.Stloc1 => true,
            IlOpCode.Stloc2 => true,
            IlOpCode.Stloc3 => true,
            IlOpCode.StlocS => true,
            IlOpCode.Stloc => true,

            IlOpCode.LdcI4M1 => true,
            IlOpCode.LdcI4_0 => true,
            IlOpCode.LdcI4_1 => true,
            IlOpCode.LdcI4_2 => true,
            IlOpCode.LdcI4_3 => true,
            IlOpCode.LdcI4_4 => true,
            IlOpCode.LdcI4_5 => true,
            IlOpCode.LdcI4_6 => true,
            IlOpCode.LdcI4_7 => true,
            IlOpCode.LdcI4_8 => true,
            IlOpCode.LdcI4S => true,
            IlOpCode.LdcI4 => true,

            IlOpCode.Add => true,
            IlOpCode.Sub => true,
            IlOpCode.Mul => true,
            IlOpCode.Div => true,
            IlOpCode.Ceq => true,
            IlOpCode.Cgt => true,
            IlOpCode.Clt => true,

            IlOpCode.Br => true,
            IlOpCode.BrS => true,
            IlOpCode.Brtrue => true,
            IlOpCode.BrtrueS => true,
            IlOpCode.Brfalse => true,
            IlOpCode.BrfalseS => true,

            IlOpCode.Ret => true,
            _ => false
        };
    }
}
