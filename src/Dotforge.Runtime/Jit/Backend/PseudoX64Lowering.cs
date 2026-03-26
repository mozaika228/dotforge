namespace Dotforge.Runtime.Jit.Backend;

public static class PseudoX64Lowering
{
    public static IReadOnlyList<string> Lower(IrFunction function)
    {
        var asm = new List<string>(function.Instructions.Count + 4)
        {
            $"; method 0x{function.MethodToken:X8}",
            "push rbp",
            "mov rbp, rsp"
        };

        foreach (var inst in function.Instructions)
        {
            asm.Add(inst.OpCode switch
            {
                IrOpCode.Label => $"{inst.Label}:",
                IrOpCode.ConstI4 => $"mov t{inst.Dest}, {inst.Immediate}",
                IrOpCode.LoadArg => $"mov t{inst.Dest}, arg{inst.ArgIndex}",
                IrOpCode.LoadLocal => $"mov t{inst.Dest}, loc{inst.LocalIndex}",
                IrOpCode.StoreLocal => $"mov loc{inst.LocalIndex}, t{inst.Left}",
                IrOpCode.Add => $"add t{inst.Dest}, t{inst.Left}, t{inst.Right}",
                IrOpCode.Sub => $"sub t{inst.Dest}, t{inst.Left}, t{inst.Right}",
                IrOpCode.Mul => $"imul t{inst.Dest}, t{inst.Left}, t{inst.Right}",
                IrOpCode.Div => $"idiv t{inst.Dest}, t{inst.Left}, t{inst.Right}",
                IrOpCode.Ceq => $"cmp_eq t{inst.Dest}, t{inst.Left}, t{inst.Right}",
                IrOpCode.Cgt => $"cmp_gt t{inst.Dest}, t{inst.Left}, t{inst.Right}",
                IrOpCode.Clt => $"cmp_lt t{inst.Dest}, t{inst.Left}, t{inst.Right}",
                IrOpCode.Br => $"jmp {inst.Label}",
                IrOpCode.BrTrue => $"brtrue t{inst.Left}, {inst.Label}",
                IrOpCode.BrFalse => $"brfalse t{inst.Left}, {inst.Label}",
                IrOpCode.Ret => inst.Left is int t ? $"mov eax, t{t}; ret" : "ret",
                _ => $"; unsupported {inst.OpCode}"
            });
        }

        asm.Add("pop rbp");
        asm.Add("ret");
        return asm;
    }
}
