using Dotforge.IL;

namespace Dotforge.Runtime.Jit;

public static class IlToIrLowerer
{
    public static IrFunction Lower(int methodToken, IlMethodBody body)
    {
        var instructions = new List<IrInstruction>();
        var eval = new Stack<int>();
        var temp = 0;

        var branchTargets = body.Instructions
            .Where(i => IsBranch(i.OpCode))
            .Select(i => Convert.ToInt32(i.Operand))
            .ToHashSet();

        var labels = branchTargets.ToDictionary(x => x, x => $"L_{x:X4}");

        foreach (var il in body.Instructions)
        {
            if (labels.TryGetValue(il.Offset, out var labelName))
            {
                instructions.Add(new IrInstruction(IrOpCode.Label, Label: labelName));
            }

            switch (il.OpCode)
            {
                case IlOpCode.LdcI4M1:
                    PushConst(-1, instructions, eval, ref temp);
                    break;
                case IlOpCode.LdcI4_0:
                    PushConst(0, instructions, eval, ref temp);
                    break;
                case IlOpCode.LdcI4_1:
                    PushConst(1, instructions, eval, ref temp);
                    break;
                case IlOpCode.LdcI4_2:
                    PushConst(2, instructions, eval, ref temp);
                    break;
                case IlOpCode.LdcI4_3:
                    PushConst(3, instructions, eval, ref temp);
                    break;
                case IlOpCode.LdcI4_4:
                    PushConst(4, instructions, eval, ref temp);
                    break;
                case IlOpCode.LdcI4_5:
                    PushConst(5, instructions, eval, ref temp);
                    break;
                case IlOpCode.LdcI4_6:
                    PushConst(6, instructions, eval, ref temp);
                    break;
                case IlOpCode.LdcI4_7:
                    PushConst(7, instructions, eval, ref temp);
                    break;
                case IlOpCode.LdcI4_8:
                    PushConst(8, instructions, eval, ref temp);
                    break;
                case IlOpCode.LdcI4S:
                case IlOpCode.LdcI4:
                    PushConst(Convert.ToInt32(il.Operand), instructions, eval, ref temp);
                    break;
                case IlOpCode.Ldnull:
                case IlOpCode.Ldstr:
                    PushUnknown(instructions, eval, ref temp);
                    break;
                case IlOpCode.Dup:
                {
                    var top = PopOrUnknown(instructions, eval, ref temp);
                    eval.Push(top);
                    eval.Push(top);
                    break;
                }
                case IlOpCode.Pop:
                    _ = PopOrUnknown(instructions, eval, ref temp);
                    break;

                case IlOpCode.Ldarg0:
                    PushArg(0, instructions, eval, ref temp);
                    break;
                case IlOpCode.Ldarg1:
                    PushArg(1, instructions, eval, ref temp);
                    break;
                case IlOpCode.Ldarg2:
                    PushArg(2, instructions, eval, ref temp);
                    break;
                case IlOpCode.Ldarg3:
                    PushArg(3, instructions, eval, ref temp);
                    break;
                case IlOpCode.LdargS:
                case IlOpCode.Ldarg:
                    PushArg(Convert.ToInt32(il.Operand), instructions, eval, ref temp);
                    break;

                case IlOpCode.Ldloc0:
                    PushLocal(0, instructions, eval, ref temp);
                    break;
                case IlOpCode.Ldloc1:
                    PushLocal(1, instructions, eval, ref temp);
                    break;
                case IlOpCode.Ldloc2:
                    PushLocal(2, instructions, eval, ref temp);
                    break;
                case IlOpCode.Ldloc3:
                    PushLocal(3, instructions, eval, ref temp);
                    break;
                case IlOpCode.LdlocS:
                case IlOpCode.Ldloc:
                    PushLocal(Convert.ToInt32(il.Operand), instructions, eval, ref temp);
                    break;

                case IlOpCode.Stloc0:
                    instructions.Add(new IrInstruction(IrOpCode.StoreLocal, Left: PopOrUnknown(instructions, eval, ref temp), LocalIndex: 0));
                    break;
                case IlOpCode.Stloc1:
                    instructions.Add(new IrInstruction(IrOpCode.StoreLocal, Left: PopOrUnknown(instructions, eval, ref temp), LocalIndex: 1));
                    break;
                case IlOpCode.Stloc2:
                    instructions.Add(new IrInstruction(IrOpCode.StoreLocal, Left: PopOrUnknown(instructions, eval, ref temp), LocalIndex: 2));
                    break;
                case IlOpCode.Stloc3:
                    instructions.Add(new IrInstruction(IrOpCode.StoreLocal, Left: PopOrUnknown(instructions, eval, ref temp), LocalIndex: 3));
                    break;
                case IlOpCode.StlocS:
                case IlOpCode.Stloc:
                    instructions.Add(new IrInstruction(IrOpCode.StoreLocal, Left: PopOrUnknown(instructions, eval, ref temp), LocalIndex: Convert.ToInt32(il.Operand)));
                    break;

                case IlOpCode.Add:
                    EmitBinary(IrOpCode.Add, instructions, eval, ref temp);
                    break;
                case IlOpCode.Sub:
                    EmitBinary(IrOpCode.Sub, instructions, eval, ref temp);
                    break;
                case IlOpCode.Mul:
                    EmitBinary(IrOpCode.Mul, instructions, eval, ref temp);
                    break;
                case IlOpCode.Div:
                    EmitBinary(IrOpCode.Div, instructions, eval, ref temp);
                    break;
                case IlOpCode.Ceq:
                    EmitBinary(IrOpCode.Ceq, instructions, eval, ref temp);
                    break;
                case IlOpCode.Cgt:
                    EmitBinary(IrOpCode.Cgt, instructions, eval, ref temp);
                    break;
                case IlOpCode.Clt:
                    EmitBinary(IrOpCode.Clt, instructions, eval, ref temp);
                    break;

                case IlOpCode.Br:
                case IlOpCode.BrS:
                    instructions.Add(new IrInstruction(IrOpCode.Br, Label: labels[Convert.ToInt32(il.Operand)]));
                    break;
                case IlOpCode.Brtrue:
                case IlOpCode.BrtrueS:
                    instructions.Add(new IrInstruction(IrOpCode.BrTrue, Left: PopOrUnknown(instructions, eval, ref temp), Label: labels[Convert.ToInt32(il.Operand)]));
                    break;
                case IlOpCode.Brfalse:
                case IlOpCode.BrfalseS:
                    instructions.Add(new IrInstruction(IrOpCode.BrFalse, Left: PopOrUnknown(instructions, eval, ref temp), Label: labels[Convert.ToInt32(il.Operand)]));
                    break;

                case IlOpCode.Call:
                case IlOpCode.Callvirt:
                case IlOpCode.Calli:
                case IlOpCode.Newobj:
                case IlOpCode.Newarr:
                case IlOpCode.LdelemI4:
                case IlOpCode.LdelemRef:
                case IlOpCode.Ldfld:
                case IlOpCode.Box:
                case IlOpCode.Unbox:
                case IlOpCode.UnboxAny:
                    // In ryujit-lite planning mode, represent complex operations as unknown value producers.
                    PushUnknown(instructions, eval, ref temp);
                    break;
                case IlOpCode.StelemI4:
                case IlOpCode.StelemRef:
                case IlOpCode.Stfld:
                case IlOpCode.Throw:
                    // Complex sink operations consume stack values but do not produce one.
                    _ = PopOrUnknown(instructions, eval, ref temp);
                    break;

                case IlOpCode.Ret:
                    instructions.Add(new IrInstruction(IrOpCode.Ret, Left: eval.Count > 0 ? PopOrUnknown(instructions, eval, ref temp) : null));
                    break;

                default:
                    // Unsupported opcodes remain interpreted; JIT plan for this method is still useful for diagnostics.
                    break;
            }
        }

        return new IrFunction(methodToken, instructions, temp);
    }

    private static bool IsBranch(IlOpCode opCode)
    {
        return opCode is IlOpCode.Br or IlOpCode.BrS or IlOpCode.Brtrue or IlOpCode.BrtrueS or IlOpCode.Brfalse or IlOpCode.BrfalseS;
    }

    private static void PushConst(int value, List<IrInstruction> instructions, Stack<int> eval, ref int temp)
    {
        var dest = temp++;
        instructions.Add(new IrInstruction(IrOpCode.ConstI4, Dest: dest, Immediate: value));
        eval.Push(dest);
    }

    private static void PushArg(int index, List<IrInstruction> instructions, Stack<int> eval, ref int temp)
    {
        var dest = temp++;
        instructions.Add(new IrInstruction(IrOpCode.LoadArg, Dest: dest, ArgIndex: index));
        eval.Push(dest);
    }

    private static void PushLocal(int index, List<IrInstruction> instructions, Stack<int> eval, ref int temp)
    {
        var dest = temp++;
        instructions.Add(new IrInstruction(IrOpCode.LoadLocal, Dest: dest, LocalIndex: index));
        eval.Push(dest);
    }

    private static void EmitBinary(IrOpCode opCode, List<IrInstruction> instructions, Stack<int> eval, ref int temp)
    {
        var right = PopOrUnknown(instructions, eval, ref temp);
        var left = PopOrUnknown(instructions, eval, ref temp);
        var dest = temp++;
        instructions.Add(new IrInstruction(opCode, Dest: dest, Left: left, Right: right));
        eval.Push(dest);
    }

    private static int PopOrUnknown(List<IrInstruction> instructions, Stack<int> eval, ref int temp)
    {
        if (eval.Count > 0)
        {
            return eval.Pop();
        }

        var unknown = temp++;
        instructions.Add(new IrInstruction(IrOpCode.ConstI4, Dest: unknown, Immediate: 0));
        return unknown;
    }

    private static void PushUnknown(List<IrInstruction> instructions, Stack<int> eval, ref int temp)
    {
        var unknown = temp++;
        instructions.Add(new IrInstruction(IrOpCode.ConstI4, Dest: unknown, Immediate: 0));
        eval.Push(unknown);
    }
}
