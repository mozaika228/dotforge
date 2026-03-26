using Dotforge.IL;
using Dotforge.Runtime.Jit;

namespace Dotforge.Runtime.Tests;

public sealed class JitPlanTests
{
    [Xunit.Fact]
    public void FoldsSimpleIntegerExpression()
    {
        var body = new IlMethodBody(
        [
            new IlInstruction(0, IlOpCode.LdcI4_2),
            new IlInstruction(1, IlOpCode.LdcI4_3),
            new IlInstruction(2, IlOpCode.Add),
            new IlInstruction(3, IlOpCode.Ret)
        ]);

        var plan = MethodJitPlan.Create(0x06000001, body);
        Xunit.Assert.Contains(plan.OptimizedIr.Instructions, i => i.OpCode == IrOpCode.ConstI4 && i.Immediate == 5);
        Xunit.Assert.DoesNotContain(plan.OptimizedIr.Instructions, i => i.OpCode == IrOpCode.Add);
    }

    [Xunit.Fact]
    public void EmitsLabelsForBranches()
    {
        var body = new IlMethodBody(
        [
            new IlInstruction(0, IlOpCode.LdcI4_1),
            new IlInstruction(1, IlOpCode.BrtrueS, 4),
            new IlInstruction(2, IlOpCode.LdcI4_0),
            new IlInstruction(3, IlOpCode.Ret),
            new IlInstruction(4, IlOpCode.LdcI4_1),
            new IlInstruction(5, IlOpCode.Ret)
        ]);

        var plan = MethodJitPlan.Create(0x06000002, body);
        Xunit.Assert.Contains(plan.InitialIr.Instructions, i => i.OpCode == IrOpCode.Label && i.Label == "L_0004");
        Xunit.Assert.Contains(plan.InitialIr.Instructions, i => i.OpCode == IrOpCode.BrTrue && i.Label == "L_0004");
    }

    [Xunit.Fact]
    public void ProducesPseudoX64Lowering()
    {
        var body = new IlMethodBody(
        [
            new IlInstruction(0, IlOpCode.LdcI4, 42),
            new IlInstruction(5, IlOpCode.Ret)
        ]);

        var plan = MethodJitPlan.Create(0x06000003, body);
        Xunit.Assert.Contains(plan.LoweredPseudoAsm, line => line.Contains("mov t"));
        Xunit.Assert.Contains(plan.LoweredPseudoAsm, line => line.Contains("ret"));
    }
}
