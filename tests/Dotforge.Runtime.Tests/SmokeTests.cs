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
    public void ExecutesNewobjAndFieldFlow()
    {
        var source = """
            public sealed class Box
            {
                public int Value;
                public Box(int value)
                {
                    Value = value;
                }

                public int GetValue()
                {
                    return Value;
                }
            }

            public static class Program
            {
                public static int Main()
                {
                    var box = new Box(33);
                    return box.GetValue();
                }
            }
            """;

        var assemblyPath = Compile(source);
        using var assembly = ManagedAssembly.Load(assemblyPath);
        var vm = new MiniVm();
        var result = vm.ExecuteEntryPoint(assembly);
        Xunit.Assert.Equal(33, result);
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

    private static string Compile(string source)
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
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                nullableContextOptions: NullableContextOptions.Enable));

        var emitResult = compilation.Emit(assemblyPath);
        if (!emitResult.Success)
        {
            var diagnostics = string.Join(Environment.NewLine, emitResult.Diagnostics);
            throw new InvalidOperationException($"Test assembly compilation failed:{Environment.NewLine}{diagnostics}");
        }

        return assemblyPath;
    }
}
