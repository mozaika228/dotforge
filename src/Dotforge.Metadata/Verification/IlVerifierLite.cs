using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotforge.Metadata.Verification;

public static class IlVerifierLite
{
    public static VerificationReport Verify(ManagedAssembly assembly)
    {
        var report = new VerificationReport();
        var metadata = assembly.Metadata;

        foreach (var methodHandle in metadata.MethodDefinitions)
        {
            var method = metadata.GetMethodDefinition(methodHandle);
            if (method.RelativeVirtualAddress == 0)
            {
                continue;
            }

            var methodName = metadata.GetString(method.Name);
            var token = MetadataTokens.GetToken(methodHandle);
            MethodBodyBlock body;
            try
            {
                body = assembly.GetMethodBody(methodHandle);
            }
            catch (Exception ex)
            {
                report.AddError("IL0001", $"Method 0x{token:X8} {methodName}: failed to read body: {ex.Message}");
                continue;
            }

            VerifyMethodBody(body, token, methodName, report);
        }

        return report;
    }

    private static void VerifyMethodBody(MethodBodyBlock body, int token, string methodName, VerificationReport report)
    {
        var il = body.GetILBytes();
        var offset = 0;
        var instructionOffsets = new HashSet<int>();
        var branchTargets = new List<int>();
        var stack = 0;

        while (offset < il.Length)
        {
            var start = offset;
            instructionOffsets.Add(start);
            var op = il[offset++];
            if (op == 0xFE)
            {
                if (offset >= il.Length)
                {
                    report.AddError("IL0002", $"Method 0x{token:X8} {methodName}: malformed extended opcode at end.");
                    return;
                }

                var ext = il[offset++];
                switch (ext)
                {
                    case 0x01: // ceq
                    case 0x02: // cgt
                    case 0x04: // clt
                        stack -= 1;
                        break;
                    case 0x09: // ldarg
                    case 0x0C: // ldloc
                        EnsureBytes(il, offset, 2, token, methodName, report);
                        offset += 2;
                        stack += 1;
                        break;
                    case 0x0E: // stloc
                        EnsureBytes(il, offset, 2, token, methodName, report);
                        offset += 2;
                        stack -= 1;
                        break;
                    case 0x11: // endfilter
                        break;
                    default:
                        report.AddWarning("IL000W", $"Method 0x{token:X8} {methodName}: unsupported extended opcode 0xFE{ext:X2}.");
                        break;
                }
            }
            else
            {
                switch (op)
                {
                    case 0x00: // nop
                    case 0x2A: // ret
                    case 0xDC: // endfinally/endfault
                        break;

                    case 0x06: // ldloc.0
                    case 0x07: // ldloc.1
                    case 0x08: // ldloc.2
                    case 0x09: // ldloc.3
                    case 0x02: // ldarg.0
                    case 0x03: // ldarg.1
                    case 0x04: // ldarg.2
                    case 0x05: // ldarg.3
                    case 0x15:
                    case 0x16:
                    case 0x17:
                    case 0x18:
                    case 0x19:
                    case 0x1A:
                    case 0x1B:
                    case 0x1C:
                    case 0x1D:
                    case 0x1E:
                    case 0x14: // ldc + ldnull
                        stack += 1;
                        break;

                    case 0x0A:
                    case 0x0B:
                    case 0x0C:
                    case 0x0D: // stloc.*
                    case 0x26: // pop
                        stack -= 1;
                        break;

                    case 0x0E:
                    case 0x11:
                    case 0x13: // ldarg.s ldloc.s stloc.s
                        if (!EnsureBytes(il, offset, 1, token, methodName, report))
                        {
                            return;
                        }
                        offset += 1;
                        if (op == 0x13)
                        {
                            stack -= 1;
                        }
                        else
                        {
                            stack += 1;
                        }
                        break;

                    case 0x1F: // ldc.i4.s
                        if (!EnsureBytes(il, offset, 1, token, methodName, report))
                        {
                            return;
                        }
                        offset += 1;
                        stack += 1;
                        break;

                    case 0x20: // ldc.i4
                        if (!EnsureBytes(il, offset, 4, token, methodName, report))
                        {
                            return;
                        }
                        offset += 4;
                        stack += 1;
                        break;

                    case 0x72: // ldstr
                    case 0x73: // newobj
                    case 0x7B: // ldfld
                    case 0x7D: // stfld
                    case 0x8C: // box
                    case 0x8D: // newarr
                    case 0xA5: // unbox.any
                    case 0x28: // call
                    case 0x29: // calli
                    case 0x6F: // callvirt
                        if (!EnsureBytes(il, offset, 4, token, methodName, report))
                        {
                            return;
                        }
                        offset += 4;
                        break;

                    case 0x58:
                    case 0x59:
                    case 0x5A:
                    case 0x5B: // add/sub/mul/div
                    case 0x94:
                    case 0x9A: // ldelem
                    case 0x9E:
                    case 0xA2: // stelem
                        stack -= 1;
                        break;

                    case 0x8E: // ldlen
                        break;

                    case 0x2B:
                    case 0x2C:
                    case 0x2D:
                    case 0xDE: // short branches incl leave.s
                    {
                        if (!EnsureBytes(il, offset, 1, token, methodName, report))
                        {
                            return;
                        }
                        var delta = unchecked((sbyte)il[offset]);
                        offset += 1;
                        var target = offset + delta;
                        branchTargets.Add(target);
                        if (op == 0x2C || op == 0x2D)
                        {
                            stack -= 1;
                        }
                        break;
                    }

                    case 0x38:
                    case 0x39:
                    case 0x3A:
                    case 0xDD: // long branches incl leave
                    {
                        if (!EnsureBytes(il, offset, 4, token, methodName, report))
                        {
                            return;
                        }
                        var delta = BitConverter.ToInt32(il, offset);
                        offset += 4;
                        var target = offset + delta;
                        branchTargets.Add(target);
                        if (op == 0x39 || op == 0x3A)
                        {
                            stack -= 1;
                        }
                        break;
                    }

                    case 0x7A: // throw
                        stack -= 1;
                        break;

                    default:
                        report.AddWarning("IL000W", $"Method 0x{token:X8} {methodName}: unsupported opcode 0x{op:X2} at IL_{start:X4}.");
                        break;
                }
            }

            if (stack < 0)
            {
                report.AddError("IL0003", $"Method 0x{token:X8} {methodName}: simulated stack underflow at IL_{start:X4}.");
                stack = 0;
            }
        }

        foreach (var target in branchTargets)
        {
            if (!instructionOffsets.Contains(target))
            {
                report.AddError("IL0004", $"Method 0x{token:X8} {methodName}: invalid branch target IL_{target:X4}.");
            }
        }
    }

    private static bool EnsureBytes(byte[] il, int offset, int count, int token, string methodName, VerificationReport report)
    {
        if (offset + count > il.Length)
        {
            report.AddError("IL0002", $"Method 0x{token:X8} {methodName}: malformed IL stream.");
            return false;
        }

        return true;
    }
}
