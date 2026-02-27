using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Dotforge.IL;

public static class IlDecoder
{
    public static IlMethodBody Decode(MethodBodyBlock methodBody)
    {
        var il = methodBody.GetILReader();
        var instructions = new List<IlInstruction>();

        while (il.RemainingBytes > 0)
        {
            var offset = il.Offset;
            var opByte = il.ReadByte();

            if (opByte == 0xFE)
            {
                var ext = il.ReadByte();
                instructions.Add(ext switch
                {
                    0x01 => new IlInstruction(offset, IlOpCode.Ceq),
                    0x02 => new IlInstruction(offset, IlOpCode.Cgt),
                    0x04 => new IlInstruction(offset, IlOpCode.Clt),
                    0x09 => new IlInstruction(offset, IlOpCode.Ldarg, il.ReadUInt16()),
                    0x0C => new IlInstruction(offset, IlOpCode.Ldloc, il.ReadUInt16()),
                    0x0E => new IlInstruction(offset, IlOpCode.Stloc, il.ReadUInt16()),
                    0x11 => new IlInstruction(offset, IlOpCode.Endfinally),
                    _ => throw new NotSupportedException($"Unsupported IL opcode: 0xFE{ext:X2} at offset {offset}.")
                });
                continue;
            }

            instructions.Add(opByte switch
            {
                0x00 => new IlInstruction(offset, IlOpCode.Nop),
                0x25 => new IlInstruction(offset, IlOpCode.Dup),
                0x02 => new IlInstruction(offset, IlOpCode.Ldarg0),
                0x03 => new IlInstruction(offset, IlOpCode.Ldarg1),
                0x04 => new IlInstruction(offset, IlOpCode.Ldarg2),
                0x05 => new IlInstruction(offset, IlOpCode.Ldarg3),
                0x06 => new IlInstruction(offset, IlOpCode.Ldloc0),
                0x07 => new IlInstruction(offset, IlOpCode.Ldloc1),
                0x08 => new IlInstruction(offset, IlOpCode.Ldloc2),
                0x09 => new IlInstruction(offset, IlOpCode.Ldloc3),
                0x0A => new IlInstruction(offset, IlOpCode.Stloc0),
                0x0B => new IlInstruction(offset, IlOpCode.Stloc1),
                0x0C => new IlInstruction(offset, IlOpCode.Stloc2),
                0x0D => new IlInstruction(offset, IlOpCode.Stloc3),
                0x0E => new IlInstruction(offset, IlOpCode.LdargS, il.ReadByte()),
                0x11 => new IlInstruction(offset, IlOpCode.LdlocS, il.ReadByte()),
                0x13 => new IlInstruction(offset, IlOpCode.StlocS, il.ReadByte()),
                0x15 => new IlInstruction(offset, IlOpCode.LdcI4M1),
                0x16 => new IlInstruction(offset, IlOpCode.LdcI4_0),
                0x17 => new IlInstruction(offset, IlOpCode.LdcI4_1),
                0x18 => new IlInstruction(offset, IlOpCode.LdcI4_2),
                0x19 => new IlInstruction(offset, IlOpCode.LdcI4_3),
                0x1A => new IlInstruction(offset, IlOpCode.LdcI4_4),
                0x1B => new IlInstruction(offset, IlOpCode.LdcI4_5),
                0x1C => new IlInstruction(offset, IlOpCode.LdcI4_6),
                0x1D => new IlInstruction(offset, IlOpCode.LdcI4_7),
                0x1E => new IlInstruction(offset, IlOpCode.LdcI4_8),
                0x1F => new IlInstruction(offset, IlOpCode.LdcI4S, (sbyte)il.ReadSByte()),
                0x20 => new IlInstruction(offset, IlOpCode.LdcI4, il.ReadInt32()),
                0x14 => new IlInstruction(offset, IlOpCode.Ldnull),
                0x26 => new IlInstruction(offset, IlOpCode.Pop),
                0x28 => new IlInstruction(offset, IlOpCode.Call, il.ReadInt32()),
                0x6F => new IlInstruction(offset, IlOpCode.Callvirt, il.ReadInt32()),
                0x2A => new IlInstruction(offset, IlOpCode.Ret),
                0x2B => Branch(offset, IlOpCode.BrS, il.ReadSByte(), il.Offset),
                0x2C => Branch(offset, IlOpCode.BrfalseS, il.ReadSByte(), il.Offset),
                0x2D => Branch(offset, IlOpCode.BrtrueS, il.ReadSByte(), il.Offset),
                0x38 => Branch(offset, IlOpCode.Br, il.ReadInt32(), il.Offset),
                0x39 => Branch(offset, IlOpCode.Brfalse, il.ReadInt32(), il.Offset),
                0x3A => Branch(offset, IlOpCode.Brtrue, il.ReadInt32(), il.Offset),
                0xDD => Branch(offset, IlOpCode.Leave, il.ReadInt32(), il.Offset),
                0xDE => Branch(offset, IlOpCode.LeaveS, il.ReadSByte(), il.Offset),
                0x58 => new IlInstruction(offset, IlOpCode.Add),
                0x59 => new IlInstruction(offset, IlOpCode.Sub),
                0x5A => new IlInstruction(offset, IlOpCode.Mul),
                0x5B => new IlInstruction(offset, IlOpCode.Div),
                0x7A => new IlInstruction(offset, IlOpCode.Throw),
                0x7B => new IlInstruction(offset, IlOpCode.Ldfld, il.ReadInt32()),
                0x7D => new IlInstruction(offset, IlOpCode.Stfld, il.ReadInt32()),
                0x72 => new IlInstruction(offset, IlOpCode.Ldstr, il.ReadInt32()),
                0x73 => new IlInstruction(offset, IlOpCode.Newobj, il.ReadInt32()),
                _ => throw new NotSupportedException($"Unsupported IL opcode: 0x{opByte:X2} at offset {offset}.")
            });
        }

        return new IlMethodBody(instructions);
    }

    private static IlInstruction Branch(int offset, IlOpCode opCode, int delta, int nextInstructionOffset)
    {
        var target = nextInstructionOffset + delta;
        return new IlInstruction(offset, opCode, target);
    }
}
