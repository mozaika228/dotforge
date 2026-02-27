using Dotforge.IL;

namespace Dotforge.Runtime.Jit;

public static class MethodJitPlan
{
    public static IReadOnlyList<IrInstruction> LowerToIr(IlMethodBody body)
    {
        var ir = new List<IrInstruction>(body.Instructions.Count);
        foreach (var instruction in body.Instructions)
        {
            ir.Add(new IrInstruction(
                Op: instruction.OpCode.ToString(),
                Dest: $"il_{instruction.Offset:X4}",
                Left: instruction.Operand?.ToString()));
        }

        return ir;
    }
}
