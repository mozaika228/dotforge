namespace Dotforge.Runtime.Jit.Passes;

public sealed class DeadCodeEliminationPass : IIrPass
{
    public string Name => "dce";

    public IrFunction Run(IrFunction input)
    {
        var usedTemps = new HashSet<int>();
        for (var i = input.Instructions.Count - 1; i >= 0; i--)
        {
            var inst = input.Instructions[i];
            if (inst.Left is int l)
            {
                usedTemps.Add(l);
            }

            if (inst.Right is int r)
            {
                usedTemps.Add(r);
            }
        }

        var output = new List<IrInstruction>(input.Instructions.Count);
        foreach (var inst in input.Instructions)
        {
            if (inst.Dest is int d &&
                !usedTemps.Contains(d) &&
                IsPureDefinition(inst.OpCode))
            {
                continue;
            }

            output.Add(inst);
        }

        return new IrFunction(input.MethodToken, output, input.TempCount);
    }

    private static bool IsPureDefinition(IrOpCode opCode)
    {
        return opCode is IrOpCode.ConstI4 or IrOpCode.LoadArg or IrOpCode.LoadLocal or
            IrOpCode.Add or IrOpCode.Sub or IrOpCode.Mul or IrOpCode.Div or IrOpCode.Ceq or IrOpCode.Cgt or IrOpCode.Clt;
    }
}
