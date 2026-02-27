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
        var signature = ReadMethodSignature(assembly.Metadata, entryMethod.Signature);
        var entryArgs = signature.ParameterCount switch
        {
            0 => Array.Empty<object?>(),
            1 => new object?[] { Array.Empty<string>() },
            _ => throw new NotSupportedException("Entry point with more than one parameter is not supported.")
        };

        var caches = BuildCaches(assembly.Metadata);
        var result = ExecuteMethod(assembly, caches, entryPoint, entryArgs);
        return result is int code ? code : 0;
    }

    private object? ExecuteMethod(ManagedAssembly assembly, RuntimeCaches caches, MethodDefinitionHandle methodHandle, object?[] args)
    {
        var metadata = assembly.Metadata;
        var method = metadata.GetMethodDefinition(methodHandle);
        var signature = ReadMethodSignature(metadata, method.Signature);
        var expectedArgCount = signature.ParameterCount + (signature.IsInstance ? 1 : 0);
        if (expectedArgCount != args.Length)
        {
            throw new InvalidOperationException($"Method expects {expectedArgCount} args, received {args.Length}.");
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
                case IlOpCode.Ldnull:
                    stack.Push(null);
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

                case IlOpCode.Ldfld:
                {
                    var fieldKey = ResolveFieldKey(metadata, caches, (int)instruction.Operand!);
                    var instance = RequireDotObject(stack.Pop());
                    stack.Push(instance.GetField(fieldKey));
                    ip++;
                    break;
                }
                case IlOpCode.Stfld:
                {
                    var fieldKey = ResolveFieldKey(metadata, caches, (int)instruction.Operand!);
                    var value = stack.Pop();
                    var instance = RequireDotObject(stack.Pop());
                    instance.SetField(fieldKey, value);
                    ip++;
                    break;
                }
                case IlOpCode.Newobj:
                    stack.Push(ExecuteNewobj(assembly, caches, stack, (int)instruction.Operand!));
                    ip++;
                    break;
                case IlOpCode.Call:
                    ExecuteCall(assembly, caches, stack, (int)instruction.Operand!);
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

    private void ExecuteCall(ManagedAssembly assembly, RuntimeCaches caches, Stack<object?> stack, int token)
    {
        var metadata = assembly.Metadata;
        var handle = MetadataTokens.EntityHandle(token);

        if (handle.Kind == HandleKind.MethodDefinition)
        {
            var methodHandle = (MethodDefinitionHandle)handle;
            var methodDef = metadata.GetMethodDefinition(methodHandle);
            var signature = ReadMethodSignature(metadata, methodDef.Signature);
            var totalArgs = signature.ParameterCount + (signature.IsInstance ? 1 : 0);
            var args = PopArguments(stack, totalArgs);
            var result = ExecuteMethod(assembly, caches, methodHandle, args);
            if (!signature.ReturnsVoid)
            {
                stack.Push(result);
            }
            return;
        }

        if (handle.Kind == HandleKind.MemberReference)
        {
            var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
            var name = metadata.GetString(member.Name);
            var signature = ReadMethodSignature(metadata, member.Signature);
            var totalArgs = signature.ParameterCount + (signature.IsInstance ? 1 : 0);
            var args = PopArguments(stack, totalArgs);

            if (TryHandleConsoleWriteLine(metadata, member, name, args))
            {
                return;
            }

            throw new NotSupportedException($"Unsupported member reference call: {name}.");
        }

        throw new NotSupportedException($"Unsupported call target kind: {handle.Kind}.");
    }

    private object ExecuteNewobj(ManagedAssembly assembly, RuntimeCaches caches, Stack<object?> stack, int token)
    {
        var metadata = assembly.Metadata;
        var handle = MetadataTokens.EntityHandle(token);
        MethodSignatureInfo ctorSig;
        MethodDefinitionHandle ctorHandle;
        TypeDefinitionHandle typeHandle;

        if (handle.Kind == HandleKind.MethodDefinition)
        {
            ctorHandle = (MethodDefinitionHandle)handle;
            if (!caches.MethodOwnerByToken.TryGetValue(token, out typeHandle))
            {
                throw new InvalidOperationException($"Could not resolve declaring type for ctor token 0x{token:X8}.");
            }

            var ctorMethod = metadata.GetMethodDefinition(ctorHandle);
            ctorSig = ReadMethodSignature(metadata, ctorMethod.Signature);
        }
        else if (handle.Kind == HandleKind.MemberReference)
        {
            var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
            var memberName = metadata.GetString(member.Name);
            if (!string.Equals(memberName, ".ctor", StringComparison.Ordinal))
            {
                throw new NotSupportedException("newobj target is not a constructor.");
            }

            ctorSig = ReadMethodSignature(metadata, member.Signature);
            typeHandle = ResolveTypeFromCtorMemberRef(metadata, member);
            if (!TryResolveMethodDefinition(metadata, caches, typeHandle, memberName, ctorSig.ParameterCount, out ctorHandle))
            {
                throw new NotSupportedException("Only constructors defined in the current assembly are supported.");
            }
        }
        else
        {
            throw new NotSupportedException($"Unsupported newobj target kind: {handle.Kind}.");
        }

        if (!ctorSig.IsInstance)
        {
            throw new InvalidOperationException("Constructor must be an instance method.");
        }

        var typeToken = MetadataTokens.GetToken(typeHandle);
        if (!caches.TypeNameByToken.TryGetValue(typeToken, out var typeName))
        {
            throw new InvalidOperationException($"Could not resolve type name for token 0x{typeToken:X8}.");
        }

        caches.TypeFieldKeysByToken.TryGetValue(typeToken, out var fields);
        var instance = new DotObject(typeHandle, typeName, fields ?? Array.Empty<string>());
        var ctorArgs = PopArguments(stack, ctorSig.ParameterCount);
        var allArgs = new object?[ctorArgs.Length + 1];
        allArgs[0] = instance;
        Array.Copy(ctorArgs, 0, allArgs, 1, ctorArgs.Length);
        _ = ExecuteMethod(assembly, caches, ctorHandle, allArgs);
        return instance;
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

    private static RuntimeCaches BuildCaches(MetadataReader metadata)
    {
        var methodOwnerByToken = new Dictionary<int, TypeDefinitionHandle>();
        var fieldKeyByToken = new Dictionary<int, string>();
        var typeNameByToken = new Dictionary<int, string>();
        var typeFieldKeysByToken = new Dictionary<int, List<string>>();
        var methodsByTypeName = new Dictionary<string, List<MethodDefinitionHandle>>(StringComparer.Ordinal);

        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var typeDef = metadata.GetTypeDefinition(typeHandle);
            var typeName = ReadTypeName(metadata, typeHandle);
            var typeToken = MetadataTokens.GetToken(typeHandle);
            typeNameByToken[typeToken] = typeName;
            typeFieldKeysByToken[typeToken] = new List<string>();
            methodsByTypeName[typeName] = new List<MethodDefinitionHandle>();

            foreach (var methodHandle in typeDef.GetMethods())
            {
                methodOwnerByToken[MetadataTokens.GetToken(methodHandle)] = typeHandle;
                methodsByTypeName[typeName].Add(methodHandle);
            }

            foreach (var fieldHandle in typeDef.GetFields())
            {
                var fieldDef = metadata.GetFieldDefinition(fieldHandle);
                var fieldName = metadata.GetString(fieldDef.Name);
                var fieldKey = $"{typeName}::{fieldName}";
                var fieldToken = MetadataTokens.GetToken(fieldHandle);
                fieldKeyByToken[fieldToken] = fieldKey;
                typeFieldKeysByToken[typeToken].Add(fieldKey);
            }
        }

        return new RuntimeCaches(methodOwnerByToken, fieldKeyByToken, typeNameByToken, typeFieldKeysByToken, methodsByTypeName);
    }

    private static string ResolveFieldKey(MetadataReader metadata, RuntimeCaches caches, int token)
    {
        if (caches.FieldKeyByToken.TryGetValue(token, out var knownKey))
        {
            return knownKey;
        }

        var handle = MetadataTokens.EntityHandle(token);
        if (handle.Kind != HandleKind.MemberReference)
        {
            throw new NotSupportedException($"Unsupported field token kind: {handle.Kind}.");
        }

        var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
        var fieldName = metadata.GetString(member.Name);
        var parentTypeName = ReadTypeName(metadata, member.Parent);
        return $"{parentTypeName}::{fieldName}";
    }

    private static TypeDefinitionHandle ResolveTypeFromCtorMemberRef(MetadataReader metadata, MemberReference member)
    {
        return member.Parent.Kind switch
        {
            HandleKind.TypeDefinition => (TypeDefinitionHandle)member.Parent,
            _ => throw new NotSupportedException("Only constructors on types defined in current assembly are supported.")
        };
    }

    private static bool TryResolveMethodDefinition(MetadataReader metadata, RuntimeCaches caches, TypeDefinitionHandle typeHandle, string methodName, int parameterCount, out MethodDefinitionHandle methodHandle)
    {
        var typeName = ReadTypeName(metadata, typeHandle);
        methodHandle = default;
        if (!caches.MethodsByTypeName.TryGetValue(typeName, out var methods))
        {
            return false;
        }

        foreach (var candidate in methods)
        {
            var methodDef = metadata.GetMethodDefinition(candidate);
            if (!string.Equals(metadata.GetString(methodDef.Name), methodName, StringComparison.Ordinal))
            {
                continue;
            }

            var signature = ReadMethodSignature(metadata, methodDef.Signature);
            if (signature.ParameterCount != parameterCount)
            {
                continue;
            }

            methodHandle = candidate;
            return true;
        }

        return false;
    }

    private static MethodSignatureInfo ReadMethodSignature(MetadataReader metadata, BlobHandle signatureHandle)
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

        var parameterCount = reader.ReadCompressedInteger();
        var returnType = reader.ReadSignatureTypeCode();
        return new MethodSignatureInfo(parameterCount, header.IsInstance, returnType == SignatureTypeCode.Void);
    }

    private static object?[] PopArguments(Stack<object?> stack, int count)
    {
        var args = new object?[count];
        for (var i = count - 1; i >= 0; i--)
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

    private static DotObject RequireDotObject(object? value)
    {
        if (value is not DotObject dotObject)
        {
            throw new NullReferenceException("Object reference is null or not a managed DotObject.");
        }

        return dotObject;
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

    private static string ReadTypeName(MetadataReader metadata, TypeDefinitionHandle typeHandle)
    {
        var typeDef = metadata.GetTypeDefinition(typeHandle);
        var ns = metadata.GetString(typeDef.Namespace);
        var name = metadata.GetString(typeDef.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static string ReadTypeName(MetadataReader metadata, EntityHandle handle)
    {
        return handle.Kind switch
        {
            HandleKind.TypeDefinition => ReadTypeName(metadata, (TypeDefinitionHandle)handle),
            HandleKind.TypeReference => ReadTypeName(metadata, (TypeReferenceHandle)handle),
            _ => throw new NotSupportedException($"Unsupported type handle kind: {handle.Kind}.")
        };
    }

    private static string ReadTypeName(MetadataReader metadata, TypeReferenceHandle typeHandle)
    {
        var typeRef = metadata.GetTypeReference(typeHandle);
        var ns = metadata.GetString(typeRef.Namespace);
        var name = metadata.GetString(typeRef.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
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
            DotObject => true,
            _ => true
        };
    }

    private sealed record RuntimeCaches(
        Dictionary<int, TypeDefinitionHandle> MethodOwnerByToken,
        Dictionary<int, string> FieldKeyByToken,
        Dictionary<int, string> TypeNameByToken,
        Dictionary<int, List<string>> TypeFieldKeysByToken,
        Dictionary<string, List<MethodDefinitionHandle>> MethodsByTypeName);

    private readonly record struct MethodSignatureInfo(int ParameterCount, bool IsInstance, bool ReturnsVoid);
}
