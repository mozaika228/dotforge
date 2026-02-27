using Dotforge.IL;
using Dotforge.Metadata;
using Dotforge.Runtime.Gc;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotforge.Runtime;

public sealed class MiniVm
{
    private readonly GenerationalHeap _heap = new();
    private readonly Dictionary<CallSiteCacheKey, MethodDefinitionHandle> _virtualCallCache = [];

    public int ExecuteEntryPoint(ManagedAssembly assembly)
    {
        var entryPoint = assembly.GetEntryPoint();
        var entryMethod = assembly.Metadata.GetMethodDefinition(entryPoint);
        var signature = ReadMethodSignature(assembly.Metadata, entryMethod.Signature);
        var entryArgs = signature.ParameterCount switch
        {
            0 => Array.Empty<object?>(),
            1 => [Array.Empty<string>()],
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
        var localCount = ReadLocalCount(metadata, methodBody.LocalSignature);
        var frame = new ExecutionFrame(instructions: decoded.Instructions, methodBody: methodBody, args: args, localCount: localCount)
        {
            Assembly = assembly,
            Caches = caches
        };
        var ip = 0;

        while (ip < frame.Instructions.Count)
        {
            var instruction = frame.Instructions[ip];
            try
            {
                switch (instruction.OpCode)
                {
                    case IlOpCode.Nop:
                        ip++;
                        break;
                    case IlOpCode.Endfinally:
                        ip++;
                        break;

                    case IlOpCode.Dup:
                    {
                        var value = frame.Stack.Peek();
                        frame.Stack.Push(value);
                        ip++;
                        break;
                    }

                    case IlOpCode.Ldarg0:
                        frame.Stack.Push(ReadArg(frame.Args, 0));
                        ip++;
                        break;
                    case IlOpCode.Ldarg1:
                        frame.Stack.Push(ReadArg(frame.Args, 1));
                        ip++;
                        break;
                    case IlOpCode.Ldarg2:
                        frame.Stack.Push(ReadArg(frame.Args, 2));
                        ip++;
                        break;
                    case IlOpCode.Ldarg3:
                        frame.Stack.Push(ReadArg(frame.Args, 3));
                        ip++;
                        break;
                    case IlOpCode.LdargS:
                    case IlOpCode.Ldarg:
                        frame.Stack.Push(ReadArg(frame.Args, Convert.ToInt32(instruction.Operand)));
                        ip++;
                        break;

                    case IlOpCode.Ldloc0:
                        frame.Stack.Push(ReadLocal(frame, 0));
                        ip++;
                        break;
                    case IlOpCode.Ldloc1:
                        frame.Stack.Push(ReadLocal(frame, 1));
                        ip++;
                        break;
                    case IlOpCode.Ldloc2:
                        frame.Stack.Push(ReadLocal(frame, 2));
                        ip++;
                        break;
                    case IlOpCode.Ldloc3:
                        frame.Stack.Push(ReadLocal(frame, 3));
                        ip++;
                        break;
                    case IlOpCode.LdlocS:
                    case IlOpCode.Ldloc:
                        frame.Stack.Push(ReadLocal(frame, Convert.ToInt32(instruction.Operand)));
                        ip++;
                        break;

                    case IlOpCode.Stloc0:
                        WriteLocal(frame, 0, frame.Stack.Pop());
                        ip++;
                        break;
                    case IlOpCode.Stloc1:
                        WriteLocal(frame, 1, frame.Stack.Pop());
                        ip++;
                        break;
                    case IlOpCode.Stloc2:
                        WriteLocal(frame, 2, frame.Stack.Pop());
                        ip++;
                        break;
                    case IlOpCode.Stloc3:
                        WriteLocal(frame, 3, frame.Stack.Pop());
                        ip++;
                        break;
                    case IlOpCode.StlocS:
                    case IlOpCode.Stloc:
                        WriteLocal(frame, Convert.ToInt32(instruction.Operand), frame.Stack.Pop());
                        ip++;
                        break;

                    case IlOpCode.LdcI4M1:
                        frame.Stack.Push(-1);
                        ip++;
                        break;
                    case IlOpCode.LdcI4_0:
                        frame.Stack.Push(0);
                        ip++;
                        break;
                    case IlOpCode.LdcI4_1:
                        frame.Stack.Push(1);
                        ip++;
                        break;
                    case IlOpCode.LdcI4_2:
                        frame.Stack.Push(2);
                        ip++;
                        break;
                    case IlOpCode.LdcI4_3:
                        frame.Stack.Push(3);
                        ip++;
                        break;
                    case IlOpCode.LdcI4_4:
                        frame.Stack.Push(4);
                        ip++;
                        break;
                    case IlOpCode.LdcI4_5:
                        frame.Stack.Push(5);
                        ip++;
                        break;
                    case IlOpCode.LdcI4_6:
                        frame.Stack.Push(6);
                        ip++;
                        break;
                    case IlOpCode.LdcI4_7:
                        frame.Stack.Push(7);
                        ip++;
                        break;
                    case IlOpCode.LdcI4_8:
                        frame.Stack.Push(8);
                        ip++;
                        break;
                    case IlOpCode.LdcI4S:
                        frame.Stack.Push(Convert.ToInt32(instruction.Operand));
                        ip++;
                        break;
                    case IlOpCode.LdcI4:
                        frame.Stack.Push((int)instruction.Operand!);
                        ip++;
                        break;
                    case IlOpCode.Ldnull:
                        frame.Stack.Push(null);
                        ip++;
                        break;
                    case IlOpCode.Ldstr:
                        frame.Stack.Push(ReadUserString(metadata, (int)instruction.Operand!));
                        ip++;
                        break;

                    case IlOpCode.Pop:
                        frame.Stack.Pop();
                        ip++;
                        break;
                    case IlOpCode.Add:
                        frame.Stack.Push(PopInt(frame.Stack) + PopInt(frame.Stack));
                        ip++;
                        break;
                    case IlOpCode.Sub:
                    {
                        var right = PopInt(frame.Stack);
                        var left = PopInt(frame.Stack);
                        frame.Stack.Push(left - right);
                        ip++;
                        break;
                    }
                    case IlOpCode.Mul:
                        frame.Stack.Push(PopInt(frame.Stack) * PopInt(frame.Stack));
                        ip++;
                        break;
                    case IlOpCode.Div:
                    {
                        var right = PopInt(frame.Stack);
                        var left = PopInt(frame.Stack);
                        frame.Stack.Push(left / right);
                        ip++;
                        break;
                    }
                    case IlOpCode.Ceq:
                    {
                        var right = frame.Stack.Pop();
                        var left = frame.Stack.Pop();
                        frame.Stack.Push(Equals(left, right) ? 1 : 0);
                        ip++;
                        break;
                    }
                    case IlOpCode.Cgt:
                    {
                        var right = PopInt(frame.Stack);
                        var left = PopInt(frame.Stack);
                        frame.Stack.Push(left > right ? 1 : 0);
                        ip++;
                        break;
                    }
                    case IlOpCode.Clt:
                    {
                        var right = PopInt(frame.Stack);
                        var left = PopInt(frame.Stack);
                        frame.Stack.Push(left < right ? 1 : 0);
                        ip++;
                        break;
                    }

                    case IlOpCode.Ldfld:
                    {
                        var fieldKey = ResolveFieldKey(metadata, caches, (int)instruction.Operand!);
                        var instance = RequireDotObject(frame.Stack.Pop());
                        frame.Stack.Push(instance.GetField(fieldKey));
                        ip++;
                        break;
                    }
                    case IlOpCode.Stfld:
                    {
                        var fieldKey = ResolveFieldKey(metadata, caches, (int)instruction.Operand!);
                        var value = frame.Stack.Pop();
                        var instance = RequireDotObject(frame.Stack.Pop());
                        instance.SetField(fieldKey, value);
                        ip++;
                        break;
                    }
                    case IlOpCode.Newobj:
                        frame.Stack.Push(ExecuteNewobj(assembly, caches, frame.Stack, (int)instruction.Operand!));
                        ip++;
                        break;

                    case IlOpCode.Call:
                        ExecuteCall(assembly, caches, frame.Stack, (int)instruction.Operand!);
                        ip++;
                        break;
                    case IlOpCode.Callvirt:
                        ExecuteCallVirt(assembly, caches, frame.Stack, (int)instruction.Operand!);
                        ip++;
                        break;

                    case IlOpCode.Br:
                    case IlOpCode.BrS:
                        ip = Jump(frame.OffsetToIndex, (int)instruction.Operand!);
                        break;
                    case IlOpCode.Brfalse:
                    case IlOpCode.BrfalseS:
                    {
                        var condition = frame.Stack.Pop();
                        if (!IsTrue(condition))
                        {
                            ip = Jump(frame.OffsetToIndex, (int)instruction.Operand!);
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
                        var condition = frame.Stack.Pop();
                        if (IsTrue(condition))
                        {
                            ip = Jump(frame.OffsetToIndex, (int)instruction.Operand!);
                        }
                        else
                        {
                            ip++;
                        }
                        break;
                    }
                    case IlOpCode.Leave:
                    case IlOpCode.LeaveS:
                    {
                        var currentOffset = instruction.Offset;
                        var targetOffset = (int)instruction.Operand!;
                        ip = HandleLeave(frame, currentOffset, targetOffset);
                        break;
                    }
                    case IlOpCode.Throw:
                    {
                        var thrown = frame.Stack.Pop();
                        throw BuildManagedException(thrown);
                    }

                    case IlOpCode.Ret:
                    {
                        var roots = EnumerateFrameRoots(frame);
                        _heap.CollectMinor(roots);
                        return frame.Stack.Count > 0 ? frame.Stack.Pop() : null;
                    }

                    default:
                        throw new NotSupportedException($"Unsupported opcode: {instruction.OpCode}.");
                }
            }
            catch (Exception ex)
            {
                var currentOffset = instruction.Offset;
                if (TryHandleException(assembly, caches, frame, ex, currentOffset, out var nextIp))
                {
                    ip = nextIp;
                    continue;
                }

                throw;
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

            if (TryHandleIntrinsicCall(metadata, member, name, args, out var returnValue))
            {
                if (!signature.ReturnsVoid)
                {
                    stack.Push(returnValue);
                }

                return;
            }

            throw new NotSupportedException($"Unsupported member reference call: {name}.");
        }

        throw new NotSupportedException($"Unsupported call target kind: {handle.Kind}.");
    }

    private void ExecuteCallVirt(ManagedAssembly assembly, RuntimeCaches caches, Stack<object?> stack, int token)
    {
        var metadata = assembly.Metadata;
        var handle = MetadataTokens.EntityHandle(token);
        if (handle.Kind != HandleKind.MemberReference)
        {
            ExecuteCall(assembly, caches, stack, token);
            return;
        }

        var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
        var name = metadata.GetString(member.Name);
        var signature = ReadMethodSignature(metadata, member.Signature);
        var totalArgs = signature.ParameterCount + (signature.IsInstance ? 1 : 0);
        var args = PopArguments(stack, totalArgs);

        if (args.Length == 0 || args[0] is null)
        {
            throw new NullReferenceException("callvirt on null instance.");
        }

        if (TryHandleIntrinsicCall(metadata, member, name, args, out var intrinsicRet))
        {
            if (!signature.ReturnsVoid)
            {
                stack.Push(intrinsicRet);
            }

            return;
        }

        if (args[0] is not DotObject instance)
        {
            throw new NotSupportedException("callvirt currently supports DotObject instances only.");
        }

        var runtimeTypeToken = MetadataTokens.GetToken(instance.TypeHandle);
        var cacheKey = new CallSiteCacheKey(token, runtimeTypeToken);
        if (!_virtualCallCache.TryGetValue(cacheKey, out var resolvedTarget))
        {
            if (!TryResolveVirtualTarget(metadata, caches, instance.TypeName, name, signature.ParameterCount, out resolvedTarget))
            {
                throw new MissingMethodException($"Virtual target not found: {instance.TypeName}::{name}({signature.ParameterCount}).");
            }

            _virtualCallCache[cacheKey] = resolvedTarget;
        }

        var result = ExecuteMethod(assembly, caches, resolvedTarget, args);
        if (!signature.ReturnsVoid)
        {
            stack.Push(result);
        }
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

        var typeToken = MetadataTokens.GetToken(typeHandle);
        if (!caches.TypeNameByToken.TryGetValue(typeToken, out var typeName))
        {
            throw new InvalidOperationException($"Could not resolve type name for token 0x{typeToken:X8}.");
        }

        if (!caches.TypeFieldKeysByToken.TryGetValue(typeToken, out var fields))
        {
            fields = [];
        }

        var instance = _heap.AllocateObject(typeHandle, typeName, fields);
        var ctorArgs = PopArguments(stack, ctorSig.ParameterCount);
        var allArgs = new object?[ctorArgs.Length + 1];
        allArgs[0] = instance;
        Array.Copy(ctorArgs, 0, allArgs, 1, ctorArgs.Length);
        _ = ExecuteMethod(assembly, caches, ctorHandle, allArgs);
        return instance;
    }

    private static bool TryHandleIntrinsicCall(MetadataReader metadata, MemberReference member, string name, object?[] args, out object? returnValue)
    {
        returnValue = null;
        if (member.Parent.Kind != HandleKind.TypeReference)
        {
            return false;
        }

        var typeRef = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
        var ns = metadata.GetString(typeRef.Namespace);
        var typeName = metadata.GetString(typeRef.Name);

        if (string.Equals(ns, "System", StringComparison.Ordinal) && string.Equals(typeName, "Console", StringComparison.Ordinal))
        {
            if (string.Equals(name, "WriteLine", StringComparison.Ordinal))
            {
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
                    Console.WriteLine(string.Join(" ", args));
                }

                return true;
            }
        }

        if (string.Equals(ns, "System", StringComparison.Ordinal) && string.Equals(typeName, "String", StringComparison.Ordinal))
        {
            if (string.Equals(name, "Concat", StringComparison.Ordinal))
            {
                returnValue = string.Concat(args.Select(x => x?.ToString() ?? string.Empty));
                return true;
            }
        }

        return false;
    }

    private bool TryHandleException(ManagedAssembly assembly, RuntimeCaches caches, ExecutionFrame frame, Exception exception, int faultOffset, out int nextIp)
    {
        _ = assembly;
        _ = caches;

        var regions = frame.MethodBody.ExceptionRegions
            .Where(r => IsWithinRange(faultOffset, r.TryOffset, r.TryLength))
            .OrderBy(r => r.TryLength)
            .ToArray();

        foreach (var region in regions)
        {
            if (region.Kind == ExceptionRegionKind.Catch && CatchMatches(frame.Assembly.Metadata, region, exception))
            {
                frame.Stack.Clear();
                frame.Stack.Push(exception);
                nextIp = Jump(frame.OffsetToIndex, region.HandlerOffset);
                return true;
            }
        }

        nextIp = -1;
        return false;
    }

    private int HandleLeave(ExecutionFrame frame, int currentOffset, int targetOffset)
    {
        _ = currentOffset;
        frame.Stack.Clear();
        return Jump(frame.OffsetToIndex, targetOffset);
    }

    private static bool CatchMatches(MetadataReader metadata, ExceptionRegion region, Exception exception)
    {
        if (region.CatchType.IsNil)
        {
            return true;
        }

        var catchName = ReadTypeName(metadata, region.CatchType);
        var currentType = exception.GetType();
        while (currentType is not null)
        {
            if (string.Equals(currentType.FullName, catchName, StringComparison.Ordinal))
            {
                return true;
            }

            currentType = currentType.BaseType;
        }

        return string.Equals(catchName, "System.Object", StringComparison.Ordinal);
    }

    private bool TryResolveVirtualTarget(MetadataReader metadata, RuntimeCaches caches, string runtimeTypeName, string methodName, int parameterCount, out MethodDefinitionHandle methodHandle)
    {
        methodHandle = default;
        if (!caches.MethodsByTypeName.TryGetValue(runtimeTypeName, out var methods))
        {
            return false;
        }

        foreach (var candidate in methods)
        {
            var method = metadata.GetMethodDefinition(candidate);
            if (!string.Equals(metadata.GetString(method.Name), methodName, StringComparison.Ordinal))
            {
                continue;
            }

            var signature = ReadMethodSignature(metadata, method.Signature);
            if (signature.ParameterCount != parameterCount || !signature.IsInstance)
            {
                continue;
            }

            methodHandle = candidate;
            return true;
        }

        return false;
    }

    private static Exception BuildManagedException(object? thrown)
    {
        return thrown switch
        {
            Exception ex => ex,
            null => new NullReferenceException("throw null"),
            DotObject dotObject => new InvalidOperationException($"Managed object throw is not mapped yet: {dotObject.TypeName}."),
            _ => new Exception(thrown.ToString())
        };
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
            typeFieldKeysByToken[typeToken] = [];
            methodsByTypeName[typeName] = [];

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

    private static int ReadLocalCount(MetadataReader metadata, StandaloneSignatureHandle localSignatureHandle)
    {
        if (localSignatureHandle.IsNil)
        {
            return 0;
        }

        var localSig = metadata.GetStandaloneSignature(localSignatureHandle);
        var reader = metadata.GetBlobReader(localSig.Signature);
        var header = reader.ReadSignatureHeader();
        if (header.Kind != SignatureKind.LocalVariables)
        {
            throw new NotSupportedException($"Unsupported local signature kind: {header.Kind}.");
        }

        return reader.ReadCompressedInteger();
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

    private static object? ReadLocal(ExecutionFrame frame, int index)
    {
        if (index < 0 || index >= frame.Locals.Length)
        {
            throw new IndexOutOfRangeException($"Local index {index} is out of range.");
        }

        return frame.Locals[index];
    }

    private static void WriteLocal(ExecutionFrame frame, int index, object? value)
    {
        if (index < 0 || index >= frame.Locals.Length)
        {
            throw new IndexOutOfRangeException($"Local index {index} is out of range.");
        }

        frame.Locals[index] = value;
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

    private static bool IsWithinRange(int offset, int start, int length)
    {
        return offset >= start && offset < start + length;
    }

    private static IEnumerable<DotObject> EnumerateFrameRoots(ExecutionFrame frame)
    {
        foreach (var arg in frame.Args)
        {
            if (arg is DotObject dotArg)
            {
                yield return dotArg;
            }
        }

        foreach (var local in frame.Locals)
        {
            if (local is DotObject dotLocal)
            {
                yield return dotLocal;
            }
        }

        foreach (var stackItem in frame.Stack)
        {
            if (stackItem is DotObject dotStack)
            {
                yield return dotStack;
            }
        }
    }

    private sealed record RuntimeCaches(
        Dictionary<int, TypeDefinitionHandle> MethodOwnerByToken,
        Dictionary<int, string> FieldKeyByToken,
        Dictionary<int, string> TypeNameByToken,
        Dictionary<int, List<string>> TypeFieldKeysByToken,
        Dictionary<string, List<MethodDefinitionHandle>> MethodsByTypeName);

    private readonly record struct MethodSignatureInfo(int ParameterCount, bool IsInstance, bool ReturnsVoid);
    private readonly record struct CallSiteCacheKey(int MethodToken, int RuntimeTypeToken);

    private sealed class ExecutionFrame
    {
        public ExecutionFrame(IReadOnlyList<IlInstruction> instructions, MethodBodyBlock methodBody, object?[] args, int localCount)
        {
            Instructions = instructions;
            MethodBody = methodBody;
            Args = args;
            Locals = new object?[localCount];
            Stack = new Stack<object?>();
            OffsetToIndex = BuildOffsetMap(instructions);
            Assembly = null!;
            Caches = null!;
        }

        public IReadOnlyList<IlInstruction> Instructions { get; }
        public MethodBodyBlock MethodBody { get; }
        public object?[] Args { get; }
        public object?[] Locals { get; }
        public Stack<object?> Stack { get; }
        public Dictionary<int, int> OffsetToIndex { get; }
        public ManagedAssembly Assembly { get; set; }
        public RuntimeCaches Caches { get; set; }
    }
}
