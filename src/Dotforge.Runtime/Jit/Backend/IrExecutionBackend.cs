namespace Dotforge.Runtime.Jit.Backend;

public static class IrExecutionBackend
{
    public static object? Execute(IrFunction function, object?[] args, object?[] locals)
    {
        var instructions = function.Instructions;
        var labelToIndex = BuildLabelToIndex(instructions);
        var temps = new object?[function.TempCount];
        var ip = 0;

        while (ip < instructions.Count)
        {
            var instruction = instructions[ip];
            switch (instruction.OpCode)
            {
                case IrOpCode.Label:
                    ip++;
                    break;

                case IrOpCode.ConstI4:
                    temps[Require(instruction.Dest, nameof(instruction.Dest))] = Require(instruction.Immediate, nameof(instruction.Immediate));
                    ip++;
                    break;

                case IrOpCode.LoadArg:
                    temps[Require(instruction.Dest, nameof(instruction.Dest))] = args[Require(instruction.ArgIndex, nameof(instruction.ArgIndex))];
                    ip++;
                    break;

                case IrOpCode.LoadLocal:
                    temps[Require(instruction.Dest, nameof(instruction.Dest))] = locals[Require(instruction.LocalIndex, nameof(instruction.LocalIndex))];
                    ip++;
                    break;

                case IrOpCode.StoreLocal:
                    locals[Require(instruction.LocalIndex, nameof(instruction.LocalIndex))] = temps[Require(instruction.Left, nameof(instruction.Left))];
                    ip++;
                    break;

                case IrOpCode.Add:
                {
                    var left = ToInt32(temps[Require(instruction.Left, nameof(instruction.Left))]);
                    var right = ToInt32(temps[Require(instruction.Right, nameof(instruction.Right))]);
                    temps[Require(instruction.Dest, nameof(instruction.Dest))] = left + right;
                    ip++;
                    break;
                }
                case IrOpCode.Sub:
                {
                    var left = ToInt32(temps[Require(instruction.Left, nameof(instruction.Left))]);
                    var right = ToInt32(temps[Require(instruction.Right, nameof(instruction.Right))]);
                    temps[Require(instruction.Dest, nameof(instruction.Dest))] = left - right;
                    ip++;
                    break;
                }
                case IrOpCode.Mul:
                {
                    var left = ToInt32(temps[Require(instruction.Left, nameof(instruction.Left))]);
                    var right = ToInt32(temps[Require(instruction.Right, nameof(instruction.Right))]);
                    temps[Require(instruction.Dest, nameof(instruction.Dest))] = left * right;
                    ip++;
                    break;
                }
                case IrOpCode.Div:
                {
                    var left = ToInt32(temps[Require(instruction.Left, nameof(instruction.Left))]);
                    var right = ToInt32(temps[Require(instruction.Right, nameof(instruction.Right))]);
                    temps[Require(instruction.Dest, nameof(instruction.Dest))] = left / right;
                    ip++;
                    break;
                }
                case IrOpCode.Ceq:
                {
                    var left = temps[Require(instruction.Left, nameof(instruction.Left))];
                    var right = temps[Require(instruction.Right, nameof(instruction.Right))];
                    temps[Require(instruction.Dest, nameof(instruction.Dest))] = Equals(left, right) ? 1 : 0;
                    ip++;
                    break;
                }
                case IrOpCode.Cgt:
                {
                    var left = ToInt32(temps[Require(instruction.Left, nameof(instruction.Left))]);
                    var right = ToInt32(temps[Require(instruction.Right, nameof(instruction.Right))]);
                    temps[Require(instruction.Dest, nameof(instruction.Dest))] = left > right ? 1 : 0;
                    ip++;
                    break;
                }
                case IrOpCode.Clt:
                {
                    var left = ToInt32(temps[Require(instruction.Left, nameof(instruction.Left))]);
                    var right = ToInt32(temps[Require(instruction.Right, nameof(instruction.Right))]);
                    temps[Require(instruction.Dest, nameof(instruction.Dest))] = left < right ? 1 : 0;
                    ip++;
                    break;
                }

                case IrOpCode.Br:
                    ip = labelToIndex[Require(instruction.Label, nameof(instruction.Label))];
                    break;
                case IrOpCode.BrTrue:
                {
                    var condition = temps[Require(instruction.Left, nameof(instruction.Left))];
                    ip = IsTrue(condition) ? labelToIndex[Require(instruction.Label, nameof(instruction.Label))] : ip + 1;
                    break;
                }
                case IrOpCode.BrFalse:
                {
                    var condition = temps[Require(instruction.Left, nameof(instruction.Left))];
                    ip = !IsTrue(condition) ? labelToIndex[Require(instruction.Label, nameof(instruction.Label))] : ip + 1;
                    break;
                }

                case IrOpCode.Ret:
                    return instruction.Left is int valueTemp ? temps[valueTemp] : null;

                default:
                    throw new NotSupportedException($"Unsupported IR opcode for execution backend: {instruction.OpCode}.");
            }
        }

        return null;
    }

    private static Dictionary<string, int> BuildLabelToIndex(IReadOnlyList<IrInstruction> instructions)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < instructions.Count; i++)
        {
            var instruction = instructions[i];
            if (instruction.OpCode == IrOpCode.Label && instruction.Label is not null)
            {
                map[instruction.Label] = i;
            }
        }

        return map;
    }

    private static int Require(int? value, string field)
    {
        if (value is not int unwrapped)
        {
            throw new InvalidOperationException($"IR instruction missing required field: {field}.");
        }

        return unwrapped;
    }

    private static string Require(string? value, string field)
    {
        if (value is null)
        {
            throw new InvalidOperationException($"IR instruction missing required field: {field}.");
        }

        return value;
    }

    private static int ToInt32(object? value)
    {
        return Convert.ToInt32(value);
    }

    private static bool IsTrue(object? value)
    {
        if (value is null)
        {
            return false;
        }

        return value switch
        {
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            _ => true
        };
    }
}
