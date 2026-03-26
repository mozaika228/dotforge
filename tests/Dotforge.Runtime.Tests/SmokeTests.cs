using Dotforge.IL;
using Dotforge.Metadata;
using Dotforge.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dotforge.Runtime.Tests;

public sealed class SmokeTests
{
    [Xunit.Fact]
    public void ExecutesArithmeticWithLocals()
    {
        var source = """
            public static class Program
            {
                public static int Main()
                {
                    int a = 20;
                    int b = 22;
                    int c = a + b;
                    return c;
                }
            }
            """;

        var assemblyPath = Compile(source);
        using var assembly = ManagedAssembly.Load(assemblyPath);
        var vm = new MiniVm();
        var result = vm.ExecuteEntryPoint(assembly);
        Xunit.Assert.Equal(42, result);
    }

    [Xunit.Fact]
    public void ExecutesLdstrAndBranchOpcodes()
    {
        var source = """
            using System;

            public static class Program
            {
                public static int Main()
                {
                    string s = "hello";
                    if (s is null)
                    {
                        return 0;
                    }

                    if (s.Length > 0)
                    {
                        return 11;
                    }

                    return 0;
                }
            }
            """;

        var assemblyPath = Compile(source);
        using var assembly = ManagedAssembly.Load(assemblyPath);
        AssertMethodContainsOpcodes(assembly, "Program::Main", IlOpCode.Ldstr);
        AssertMethodContainsAnyOpcode(assembly, "Program::Main", IlOpCode.Brtrue, IlOpCode.BrtrueS, IlOpCode.Brfalse, IlOpCode.BrfalseS);
        var vm = new MiniVm();
        var result = vm.ExecuteEntryPoint(assembly);
        Xunit.Assert.Equal(11, result);
    }

    [Xunit.Fact]
    public void ExecutesNewarrAndElementOps()
    {
        var source = """
            public static class Program
            {
                public static int Main()
                {
                    int[] values = new int[2];
                    values[0] = 40;
                    values[1] = 2;
                    return values[0] + values[1];
                }
            }
            """;

        var assemblyPath = Compile(source);
        using var assembly = ManagedAssembly.Load(assemblyPath);
        AssertMethodContainsOpcodes(assembly, "Program::Main", IlOpCode.Newarr, IlOpCode.StelemI4, IlOpCode.LdelemI4);
        var vm = new MiniVm();
        var result = vm.ExecuteEntryPoint(assembly);
        Xunit.Assert.Equal(42, result);
    }

    [Xunit.Fact]
    public void ExecutesStringArrayReferenceElementOps()
    {
        var source = """
            public static class Program
            {
                public static int Main()
                {
                    string[] values = new string[1];
                    values[0] = "x";
                    return values[0] != null ? 1 : 0;
                }
            }
            """;

        var assemblyPath = Compile(source);
        using var assembly = ManagedAssembly.Load(assemblyPath);
        AssertMethodContainsOpcodes(assembly, "Program::Main", IlOpCode.Newarr);
        AssertMethodContainsAnyOpcode(assembly, "Program::Main", IlOpCode.StelemRef, IlOpCode.StelemI4);
        AssertMethodContainsAnyOpcode(assembly, "Program::Main", IlOpCode.LdelemRef, IlOpCode.LdelemI4);
        var vm = new MiniVm();
        var result = vm.ExecuteEntryPoint(assembly);
        Xunit.Assert.Equal(1, result);
    }

    [Xunit.Fact]
    public void ExecutesBoxUnboxAny()
    {
        var source = """
            public static class Program
            {
                public static int Main()
                {
                    object value = 42;
                    return (int)value;
                }
            }
            """;

        var assemblyPath = Compile(source);
        using var assembly = ManagedAssembly.Load(assemblyPath);
        AssertMethodContainsOpcodes(assembly, "Program::Main", IlOpCode.Box, IlOpCode.UnboxAny);
        var vm = new MiniVm();
        var result = vm.ExecuteEntryPoint(assembly);
        Xunit.Assert.Equal(42, result);
    }

    [Xunit.Fact]
    public void HandlesCatchForNullCallvirt()
    {
        var source = """
            using System;

            public static class Program
            {
                public static int Main()
                {
                    try
                    {
                        string? s = null;
                        _ = s!.ToString();
                        return 1;
                    }
                    catch (NullReferenceException)
                    {
                        return 7;
                    }
                }
            }
            """;

        var assemblyPath = Compile(source);
        using var assembly = ManagedAssembly.Load(assemblyPath);
        var vm = new MiniVm();
        var result = vm.ExecuteEntryPoint(assembly);
        Xunit.Assert.Equal(7, result);
    }

    [Xunit.Fact]
    public void ExecutesCalliViaFunctionPointer()
    {
        var source = """
            using System;

            public static unsafe class Program
            {
                private static int Inc(int x) => x + 1;

                public static int Main()
                {
                    delegate* managed<int, int> fn = &Inc;
                    return fn(41);
                }
            }
            """;

        var assemblyPath = Compile(source, allowUnsafe: true);
        using var assembly = ManagedAssembly.Load(assemblyPath);
        AssertMethodContainsOpcodes(assembly, "Program::Main", IlOpCode.Calli, IlOpCode.Ldftn);
        var vm = new MiniVm();
        var result = vm.ExecuteEntryPoint(assembly);
        Xunit.Assert.Equal(42, result);
    }

    [Xunit.Fact]
    public void ExecutesFinallyOnNormalLeave()
    {
        var source = """
            public static class Program
            {
                public static int Main()
                {
                    int x = 1;
                    try
                    {
                        x = 2;
                    }
                    finally
                    {
                        x = x + 3;
                    }

                    return x;
                }
            }
            """;

        var assemblyPath = Compile(source);
        using var assembly = ManagedAssembly.Load(assemblyPath);
        var vm = new MiniVm();
        var result = vm.ExecuteEntryPoint(assembly);
        Xunit.Assert.Equal(5, result);
    }

    [Xunit.Fact]
    public void ExecutesFinallyDuringExceptionUnwind()
    {
        var source = """
            using System;

            public static class Program
            {
                public static int Main()
                {
                    int x = 0;
                    try
                    {
                        try
                        {
                            throw new Exception("boom");
                        }
                        finally
                        {
                            x = 9;
                        }
                    }
                    catch (Exception)
                    {
                        return x;
                    }
                }
            }
            """;

        var assemblyPath = Compile(source);
        using var assembly = ManagedAssembly.Load(assemblyPath);
        var vm = new MiniVm();
        var result = vm.ExecuteEntryPoint(assembly);
        Xunit.Assert.Equal(9, result);
    }

    [Xunit.Fact]
    public void ExecutesPInvokeInteropCalls()
    {
        var source = """
            using System.Runtime.InteropServices;

            public static class Native
            {
                [DllImport("dotforge_native", EntryPoint = "abs")]
                public static extern int Abs(int value);

                [DllImport("dotforge_native", EntryPoint = "strlen")]
                public static extern int StrLen(string value);
            }

            public static class Program
            {
                public static int Main()
                {
                    return Native.Abs(-40) + Native.StrLen("ab");
                }
            }
            """;

        var assemblyPath = Compile(source);
        using var assembly = ManagedAssembly.Load(assemblyPath);
        var vm = new MiniVm();
        var result = vm.ExecuteEntryPoint(assembly);
        Xunit.Assert.Equal(42, result);
    }

    private static void AssertMethodContainsOpcodes(ManagedAssembly assembly, string methodSpec, params IlOpCode[] expected)
    {
        var handle = ResolveMethodHandle(assembly, methodSpec);
        var body = assembly.GetMethodBody(handle);
        var decoded = IlDecoder.Decode(body);
        var opcodes = decoded.Instructions.Select(i => i.OpCode).ToHashSet();

        foreach (var opcode in expected)
        {
            Xunit.Assert.Contains(opcode, opcodes);
        }
    }

    private static void AssertMethodContainsAnyOpcode(ManagedAssembly assembly, string methodSpec, params IlOpCode[] candidates)
    {
        var handle = ResolveMethodHandle(assembly, methodSpec);
        var body = assembly.GetMethodBody(handle);
        var decoded = IlDecoder.Decode(body);
        var opcodes = decoded.Instructions.Select(i => i.OpCode).ToHashSet();
        Xunit.Assert.True(candidates.Any(opcodes.Contains), $"None of opcodes found: {string.Join(", ", candidates)}");
    }

    private static System.Reflection.Metadata.MethodDefinitionHandle ResolveMethodHandle(ManagedAssembly assembly, string methodSpec)
    {
        var metadata = assembly.Metadata;
        var parts = methodSpec.Split("::", StringSplitOptions.None);
        if (parts.Length != 2)
        {
            throw new ArgumentException("Method spec must be Type::Method.", nameof(methodSpec));
        }

        var typeName = parts[0];
        var methodName = parts[1];
        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var typeDef = metadata.GetTypeDefinition(typeHandle);
            var ns = metadata.GetString(typeDef.Namespace);
            var name = metadata.GetString(typeDef.Name);
            var full = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            if (!string.Equals(full, typeName, StringComparison.Ordinal) &&
                !string.Equals(name, typeName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var methodHandle in typeDef.GetMethods())
            {
                var methodDef = metadata.GetMethodDefinition(methodHandle);
                if (string.Equals(metadata.GetString(methodDef.Name), methodName, StringComparison.Ordinal))
                {
                    return methodHandle;
                }
            }
        }

        throw new MissingMethodException($"Method '{methodSpec}' not found.");
    }

    private static string Compile(string source, bool allowUnsafe = false)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dotforge-tests");
        Directory.CreateDirectory(tempRoot);
        var assemblyPath = Path.Combine(tempRoot, $"{Guid.NewGuid():N}.dll");

        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp12));
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: $"DotforgeTest_{Guid.NewGuid():N}",
            syntaxTrees: [tree],
            references: refs,
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.ConsoleApplication,
                optimizationLevel: OptimizationLevel.Debug,
                nullableContextOptions: NullableContextOptions.Enable,
                allowUnsafe: allowUnsafe));

        var emitResult = compilation.Emit(assemblyPath);
        if (!emitResult.Success)
        {
            var diagnostics = string.Join(Environment.NewLine, emitResult.Diagnostics);
            throw new InvalidOperationException($"Test assembly compilation failed:{Environment.NewLine}{diagnostics}");
        }

        return assemblyPath;
    }
}
