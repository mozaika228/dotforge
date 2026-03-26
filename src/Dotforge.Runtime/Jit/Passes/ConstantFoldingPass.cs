namespace Dotforge.Runtime.Jit.Passes;

public sealed class ConstantFoldingPass : IIrPass
{
    public string Name => "const-fold";

    public IrFunction Run(IrFunction input)
    {
        var constValues = new Dictionary<int, int>();
        var output = new List<IrInstruction>(input.Instructions.Count);

        foreach (var instruction in input.Instructions)
        {
            switch (instruction.OpCode)
            {
                case IrOpCode.ConstI4:
                    if (instruction.Dest is int dest && instruction.Immediate is int imm)
                    {
                        constValues[dest] = imm;
                    }
                    output.Add(instruction);
                    break;

                case IrOpCode.Add:
                case IrOpCode.Sub:
                case IrOpCode.Mul:
                case IrOpCode.Div:
                case IrOpCode.Ceq:
                case IrOpCode.Cgt:
                case IrOpCode.Clt:
                    if (instruction.Dest is int d &&
                        instruction.Left is int l &&
                        instruction.Right is int r &&
                        constValues.TryGetValue(l, out var lv) &&
                        constValues.TryGetValue(r, out var rv))
                    {
                        var folded = Fold(instruction.OpCode, lv, rv);
                        constValues[d] = folded;
                        output.Add(new IrInstruction(IrOpCode.ConstI4, Dest: d, Immediate: folded));
                    }
                    else
                    {
                        output.Add(instruction);
                    }
                    break;

                default:
                    output.Add(instruction);
                    break;
            }
        }

        return new IrFunction(input.MethodToken, output, input.TempCount);
    }

    private static int Fold(IrOpCode opCode, int left, int right)
    {
        return opCode switch
        {
            IrOpCode.Add => left + right,
            IrOpCode.Sub => left - right,
            IrOpCode.Mul => left * right,
            IrOpCode.Div => left / right,
            IrOpCode.Ceq => left == right ? 1 : 0,
            IrOpCode.Cgt => left > right ? 1 : 0,
            IrOpCode.Clt => left < right ? 1 : 0,
            _ => throw new NotSupportedException($"Fold not supported for {opCode}.")
        };
    }
}
