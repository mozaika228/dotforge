using Dotforge.IL;
using Dotforge.Metadata;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotforge.Runtime;

public sealed class MiniVm
{
    public int ExecuteEntryPoint(ManagedAssembly assembly)
    {
        var entryPoint = assembly.GetEntryPoint();
        var entryMethod = assembly.Metadata.GetMethodDefinition(entryPoint);
        var parameterCount = entryMethod.GetParameters().Count;
        var entryArgs = parameterCount switch
        {
            0 => Array.Empty<object?>(),
            1 => new object?[] { Array.Empty<string>() },
            _ => throw new NotSupportedException("Entry point with more than one parameter is not supported.")
        };

        var result = ExecuteMethod(assembly, entryPoint, entryArgs);
        return result is int code ? code : 0;
    }

    private object? ExecuteMethod(ManagedAssembly assembly, MethodDefinitionHandle methodHandle, object?[] args)
    {
        var metadata = assembly.Metadata;
        var method = metadata.GetMethodDefinition(methodHandle);
        var parameterCount = method.GetParameters().Count;
        if (parameterCount != args.Length)
        {
            throw new InvalidOperationException($"Method expects {parameterCount} args, received {args.Length}.");
        }

        var methodBody = assembly.GetMethodBody(methodHandle);
        var decoded = IlDecoder.Decode(methodBody);
        var instructions = decoded.Instructions;
        var offsetToIndex = BuildOffsetMap(instructions);
        var stack = new Stack<object?>();
        var ip = 0;

        while (ip < instructions.Count)
        {
            var instruction = instructions[ip];
            switch (instruction.OpCode)
            {
                case IlOpCode.Nop:
                    ip++;
                    break;

                case IlOpCode.Ldarg0:
                    stack.Push(ReadArg(args, 0));
                    ip++;
                    break;
                case IlOpCode.Ldarg1:
                    stack.Push(ReadArg(args, 1));
                    ip++;
                    break;
                case IlOpCode.Ldarg2:
                    stack.Push(ReadArg(args, 2));
                    ip++;
                    break;
                case IlOpCode.Ldarg3:
                    stack.Push(ReadArg(args, 3));
                    ip++;
                    break;
                case IlOpCode.LdargS:
                    stack.Push(ReadArg(args, Convert.ToInt32(instruction.Operand)));
                    ip++;
                    break;
                case IlOpCode.Ldarg:
                    stack.Push(ReadArg(args, Convert.ToInt32(instruction.Operand)));
                    ip++;
                    break;

                case IlOpCode.LdcI4M1:
                    stack.Push(-1);
                    ip++;
                    break;
                case IlOpCode.LdcI4_0:
                    stack.Push(0);
                    ip++;
                    break;
                case IlOpCode.LdcI4_1:
                    stack.Push(1);
                    ip++;
                    break;
                case IlOpCode.LdcI4_2:
                    stack.Push(2);
                    ip++;
                    break;
                case IlOpCode.LdcI4_3:
                    stack.Push(3);
                    ip++;
                    break;
                case IlOpCode.LdcI4_4:
                    stack.Push(4);
                    ip++;
                    break;
                case IlOpCode.LdcI4_5:
                    stack.Push(5);
                    ip++;
                    break;
                case IlOpCode.LdcI4_6:
                    stack.Push(6);
                    ip++;
                    break;
                case IlOpCode.LdcI4_7:
                    stack.Push(7);
                    ip++;
                    break;
                case IlOpCode.LdcI4_8:
                    stack.Push(8);
                    ip++;
                    break;
                case IlOpCode.LdcI4S:
                    stack.Push(Convert.ToInt32(instruction.Operand));
                    ip++;
                    break;
                case IlOpCode.LdcI4:
                    stack.Push((int)instruction.Operand!);
                    ip++;
                    break;
                case IlOpCode.Ldstr:
                    stack.Push(ReadUserString(metadata, (int)instruction.Operand!));
                    ip++;
                    break;

                case IlOpCode.Pop:
                    stack.Pop();
                    ip++;
                    break;

                case IlOpCode.Add:
                    stack.Push(PopInt(stack) + PopInt(stack));
                    ip++;
                    break;
                case IlOpCode.Sub:
                {
                    var right = PopInt(stack);
                    var left = PopInt(stack);
                    stack.Push(left - right);
                    ip++;
                    break;
                }
                case IlOpCode.Mul:
                    stack.Push(PopInt(stack) * PopInt(stack));
                    ip++;
                    break;
                case IlOpCode.Div:
                {
                    var right = PopInt(stack);
                    var left = PopInt(stack);
                    stack.Push(left / right);
                    ip++;
                    break;
                }

                case IlOpCode.Call:
                    ExecuteCall(assembly, stack, (int)instruction.Operand!);
                    ip++;
                    break;

                case IlOpCode.Br:
                case IlOpCode.BrS:
                    ip = Jump(offsetToIndex, (int)instruction.Operand!);
                    break;
                case IlOpCode.Brfalse:
                case IlOpCode.BrfalseS:
                {
                    var condition = stack.Pop();
                    if (!IsTrue(condition))
                    {
                        ip = Jump(offsetToIndex, (int)instruction.Operand!);
                    }
                    else
                    {
                        ip++;
                    }
                    break;
                }
                case IlOpCode.Brtrue:
                case IlOpCode.BrtrueS:
                {
                    var condition = stack.Pop();
                    if (IsTrue(condition))
                    {
                        ip = Jump(offsetToIndex, (int)instruction.Operand!);
                    }
                    else
                    {
                        ip++;
                    }
                    break;
                }

                case IlOpCode.Ret:
                    return stack.Count > 0 ? stack.Pop() : null;

                default:
                    throw new NotSupportedException($"Unsupported opcode: {instruction.OpCode}.");
            }
        }

        return null;
    }

    private void ExecuteCall(ManagedAssembly assembly, Stack<object?> stack, int token)
    {
        var metadata = assembly.Metadata;
        var handle = MetadataTokens.EntityHandle(token);

        if (handle.Kind == HandleKind.MethodDefinition)
        {
            var methodHandle = (MethodDefinitionHandle)handle;
            var methodDef = metadata.GetMethodDefinition(methodHandle);
            var parameterCount = methodDef.GetParameters().Count;
            var args = PopArguments(stack, parameterCount);
            var result = ExecuteMethod(assembly, methodHandle, args);
            if (!ReturnsVoid(metadata, methodDef.Signature))
            {
                stack.Push(result);
            }
            return;
        }

        if (handle.Kind == HandleKind.MemberReference)
        {
            var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
            var name = metadata.GetString(member.Name);
            var paramCount = ReadParameterCount(metadata, member.Signature);
            var args = PopArguments(stack, paramCount);

            if (TryHandleConsoleWriteLine(metadata, member, name, args))
            {
                return;
            }

            throw new NotSupportedException($"Unsupported member reference call: {name}.");
        }

        throw new NotSupportedException($"Unsupported call target kind: {handle.Kind}.");
    }

    private static bool TryHandleConsoleWriteLine(MetadataReader metadata, MemberReference member, string name, object?[] args)
    {
        if (!string.Equals(name, "WriteLine", StringComparison.Ordinal))
        {
            return false;
        }

        if (member.Parent.Kind != HandleKind.TypeReference)
        {
            return false;
        }

        var typeRef = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
        var ns = metadata.GetString(typeRef.Namespace);
        var typeName = metadata.GetString(typeRef.Name);
        if (!string.Equals(ns, "System", StringComparison.Ordinal) ||
            !string.Equals(typeName, "Console", StringComparison.Ordinal))
        {
            return false;
        }

        if (args.Length == 0)
        {
            Console.WriteLine();
        }
        else if (args.Length == 1)
        {
            Console.WriteLine(args[0]);
        }
        else
        {
            throw new NotSupportedException("Only Console.WriteLine with up to one argument is supported.");
        }

        return true;
    }

    private static int ReadParameterCount(MetadataReader metadata, BlobHandle signatureHandle)
    {
        var reader = metadata.GetBlobReader(signatureHandle);
        var header = reader.ReadSignatureHeader();
        if (header.Kind != SignatureKind.Method)
        {
            throw new NotSupportedException($"Unsupported signature kind: {header.Kind}.");
        }

        if (header.IsGeneric)
        {
            _ = reader.ReadCompressedInteger();
        }

        return reader.ReadCompressedInteger();
    }

    private static bool ReturnsVoid(MetadataReader metadata, BlobHandle signatureHandle)
    {
        var reader = metadata.GetBlobReader(signatureHandle);
        var header = reader.ReadSignatureHeader();
        if (header.Kind != SignatureKind.Method)
        {
            throw new NotSupportedException($"Unsupported signature kind: {header.Kind}.");
        }

        if (header.IsGeneric)
        {
            _ = reader.ReadCompressedInteger();
        }

        _ = reader.ReadCompressedInteger(); // parameter count
        var returnType = reader.ReadSignatureTypeCode();
        return returnType == SignatureTypeCode.Void;
    }

    private static object?[] PopArguments(Stack<object?> stack, int parameterCount)
    {
        var args = new object?[parameterCount];
        for (var i = parameterCount - 1; i >= 0; i--)
        {
            args[i] = stack.Pop();
        }
        return args;
    }

    private static string ReadUserString(MetadataReader metadata, int token)
    {
        var handle = MetadataTokens.UserStringHandle(token);
        return metadata.GetUserString(handle);
    }

    private static int PopInt(Stack<object?> stack)
    {
        var value = stack.Pop();
        return Convert.ToInt32(value);
    }

    private static object? ReadArg(object?[] args, int index)
    {
        if (index < 0 || index >= args.Length)
        {
            throw new IndexOutOfRangeException($"Arg index {index} is out of range.");
        }

        return args[index];
    }

    private static int Jump(IReadOnlyDictionary<int, int> offsetToIndex, int targetOffset)
    {
        if (!offsetToIndex.TryGetValue(targetOffset, out var index))
        {
            throw new InvalidOperationException($"Invalid branch target offset: {targetOffset}.");
        }

        return index;
    }

    private static Dictionary<int, int> BuildOffsetMap(IReadOnlyList<IlInstruction> instructions)
    {
        var map = new Dictionary<int, int>(instructions.Count);
        for (var i = 0; i < instructions.Count; i++)
        {
            map[instructions[i].Offset] = i;
        }

        return map;
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
