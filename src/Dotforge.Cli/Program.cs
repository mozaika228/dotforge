using Dotforge.IL;
using Dotforge.Metadata.Loader;
using Dotforge.Metadata;
using Dotforge.Metadata.Reflection;
using Dotforge.Metadata.Verification;
using Dotforge.Runtime;
using Dotforge.Runtime.Services;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

if (args.Length < 2)
{
    PrintUsage();
    return 1;
}

var command = args[0].ToLowerInvariant();
return command switch
{
    "run" => Run(args),
    "inspect" => Inspect(args),
    "disasm" => Disasm(args),
    "verify" => Verify(args),
    "runtime" => RuntimeInfo(args),
    _ => UnknownCommand(command)
};

static int Run(string[] args)
{
    if (args.Length is < 2 or > 4)
    {
        Console.Error.WriteLine("Usage: dotforge run <path-to-managed-assembly> [--verify] [--strict-warnings]");
        return 1;
    }

    var verify = args.Skip(2).Any(static x => string.Equals(x, "--verify", StringComparison.OrdinalIgnoreCase));
    var strictWarnings = args.Skip(2).Any(static x => string.Equals(x, "--strict-warnings", StringComparison.OrdinalIgnoreCase));
    using var host = RuntimeHost.Load(args[1]);
    if (string.Equals(Environment.GetEnvironmentVariable("DOTFORGE_GC_LOG"), "1", StringComparison.Ordinal))
    {
        host.Vm.GcLogger = Console.Error.WriteLine;
    }

    if (!verify)
    {
        return host.RunEntryPoint();
    }

    var preflight = host.VerifyPreflight();
    foreach (var unresolved in preflight.UnresolvedReferences)
    {
        Console.Error.WriteLine($"[warn] unresolved-reference: {unresolved}");
    }

    foreach (var message in preflight.MetadataReport.Messages.Concat(preflight.IlReport.Messages))
    {
        var level = message.IsError ? "error" : "warn";
        Console.Error.WriteLine($"[{level}] {message.Code}: {message.Message}");
    }

    return host.RunEntryPointVerified(
        failOnWarnings: strictWarnings,
        requireResolvedReferences: true);
}

static int RuntimeInfo(string[] args)
{
    if (args.Length is not (2 or 3))
    {
        Console.Error.WriteLine("Usage: dotforge runtime <path-to-managed-assembly> [parallel-runs]");
        return 1;
    }

    using var host = RuntimeHost.Load(args[1]);
    int exitCode;
    if (args.Length == 3)
    {
        if (!int.TryParse(args[2], out var runs) || runs <= 0)
        {
            Console.Error.WriteLine("parallel-runs must be a positive integer.");
            return 1;
        }

        var results = host.RunEntryPointParallelAsync(runs).GetAwaiter().GetResult();
        exitCode = results.Last();
    }
    else
    {
        exitCode = host.RunEntryPoint();
    }

    var snapshot = host.CaptureSnapshot();
    Console.WriteLine($"runtime: exit={exitCode}");
    Console.WriteLine($"  types={snapshot.TypeCount} methods={snapshot.MethodCount}");
    Console.WriteLine($"  jit-plans={snapshot.JitPlanCount}");
    Console.WriteLine($"  runs: total={snapshot.ExecutionCount} success={snapshot.SuccessfulRuns} failed={snapshot.FailedRuns}");
    Console.WriteLine($"  gc: minor={snapshot.GcStats.MinorCollections} major={snapshot.GcStats.MajorCollections} gen0={snapshot.GcStats.Gen0Count} gen1={snapshot.GcStats.Gen1Count} loh={snapshot.GcStats.LohCount}");
    return exitCode;
}

static int Inspect(string[] args)
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine("Usage: dotforge inspect <path-to-managed-assembly>");
        return 1;
    }

    using var assembly = ManagedAssembly.Load(args[1]);
    var catalog = new MetadataCatalog(assembly);
    foreach (var type in catalog.GetTypes())
    {
        var typeGeneric = type.GenericArity > 0 ? $"<{string.Join(", ", type.GenericParameters)}>" : string.Empty;
        Console.WriteLine($"type 0x{type.Token:X8} {type.FullName}{typeGeneric}");
        foreach (var field in type.Fields)
        {
            var mode = field.IsStatic ? "static" : "instance";
            Console.WriteLine($"  field 0x{field.Token:X8} {mode} {field.Name} : {field.FieldTypeCode}");
        }

        foreach (var method in type.Methods)
        {
            var mode = method.IsStatic ? "static" : "instance";
            var methodGeneric = method.GenericArity > 0 ? $"<{string.Join(", ", method.GenericParameters)}>" : string.Empty;
            Console.WriteLine($"  method 0x{method.Token:X8} {mode} {method.Name}{methodGeneric} ({method.ParameterCount}) -> {method.ReturnTypeCode}");
        }
    }

    return 0;
}

static int Disasm(string[] args)
{
    if (args.Length != 3)
    {
        Console.Error.WriteLine("Usage: dotforge disasm <path-to-managed-assembly> <method-token-or-Type::Method>");
        return 1;
    }

    using var assembly = ManagedAssembly.Load(args[1]);
    var metadata = assembly.Metadata;
    var methodHandle = ResolveMethodHandle(metadata, args[2]);
    var methodDef = metadata.GetMethodDefinition(methodHandle);
    var declaringType = ResolveDeclaringType(metadata, methodHandle);
    var methodName = metadata.GetString(methodDef.Name);
    var typeName = ReadTypeName(metadata, declaringType);
    var body = assembly.GetMethodBody(methodHandle);
    var decoded = IlDecoder.Decode(body);

    Console.WriteLine($".method {typeName}::{methodName} 0x{MetadataTokens.GetToken(methodHandle):X8}");
    foreach (var instruction in decoded.Instructions)
    {
        var operand = instruction.Operand is null ? string.Empty : $" {instruction.Operand}";
        Console.WriteLine($"  IL_{instruction.Offset:X4}: {instruction.OpCode}{operand}");
    }

    return 0;
}

static int Verify(string[] args)
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine("Usage: dotforge verify <path-to-managed-assembly>");
        return 1;
    }

    using var context = new AssemblyLoadContextLite(Path.GetDirectoryName(Path.GetFullPath(args[1])) ?? ".");
    using var assembly = context.Load(args[1]);
    var unresolved = context.ResolveAllReferences(assembly);
    var mdReport = MetadataValidator.Validate(assembly);
    var ilReport = IlVerifierLite.Verify(assembly);

    Console.WriteLine($"verify: {Path.GetFileName(assembly.Path)}");
    foreach (var miss in unresolved)
    {
        Console.WriteLine($"  [warn] unresolved-reference: {miss}");
    }

    foreach (var m in mdReport.Messages.Concat(ilReport.Messages))
    {
        var level = m.IsError ? "error" : "warn";
        Console.WriteLine($"  [{level}] {m.Code}: {m.Message}");
    }

    var hasErrors = mdReport.HasErrors || ilReport.HasErrors;
    Console.WriteLine(hasErrors ? "verification: failed" : "verification: passed");
    return hasErrors ? 2 : 0;
}

static MethodDefinitionHandle ResolveMethodHandle(MetadataReader metadata, string methodArg)
{
    if (TryParseToken(methodArg, out var token))
    {
        var entity = MetadataTokens.EntityHandle(token);
        if (entity.Kind != HandleKind.MethodDefinition)
        {
            throw new ArgumentException($"Token 0x{token:X8} is not a MethodDefinition.");
        }

        return (MethodDefinitionHandle)entity;
    }

    var parts = methodArg.Split("::", StringSplitOptions.None);
    if (parts.Length != 2)
    {
        throw new ArgumentException("Method must be token (e.g. 0x06000001) or Type::Method.");
    }

    var typeName = parts[0];
    var methodName = parts[1];

    foreach (var typeHandle in metadata.TypeDefinitions)
    {
        if (!string.Equals(ReadTypeName(metadata, typeHandle), typeName, StringComparison.Ordinal))
        {
            continue;
        }

        var typeDef = metadata.GetTypeDefinition(typeHandle);
        foreach (var candidate in typeDef.GetMethods())
        {
            var methodDef = metadata.GetMethodDefinition(candidate);
            if (string.Equals(metadata.GetString(methodDef.Name), methodName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }
    }

    throw new MissingMethodException($"Method '{methodArg}' was not found.");
}

static bool TryParseToken(string value, out int token)
{
    token = 0;
    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
    {
        return int.TryParse(value.AsSpan(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out token);
    }

    return int.TryParse(value, out token);
}

static TypeDefinitionHandle ResolveDeclaringType(MetadataReader metadata, MethodDefinitionHandle methodHandle)
{
    foreach (var typeHandle in metadata.TypeDefinitions)
    {
        var typeDef = metadata.GetTypeDefinition(typeHandle);
        foreach (var candidate in typeDef.GetMethods())
        {
            if (candidate.Equals(methodHandle))
            {
                return typeHandle;
            }
        }
    }

    throw new InvalidOperationException("Declaring type not found.");
}

static string ReadTypeName(MetadataReader metadata, TypeDefinitionHandle typeHandle)
{
    var typeDef = metadata.GetTypeDefinition(typeHandle);
    var ns = metadata.GetString(typeDef.Namespace);
    var name = metadata.GetString(typeDef.Name);
    return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  dotforge run <path-to-managed-assembly> [--verify] [--strict-warnings]");
    Console.Error.WriteLine("  dotforge inspect <path-to-managed-assembly>");
    Console.Error.WriteLine("  dotforge disasm <path-to-managed-assembly> <method-token-or-Type::Method>");
    Console.Error.WriteLine("  dotforge verify <path-to-managed-assembly>");
    Console.Error.WriteLine("  dotforge runtime <path-to-managed-assembly> [parallel-runs]");
}
