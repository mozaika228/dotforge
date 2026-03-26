using Dotforge.Runtime.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dotforge.Runtime.Tests;

public sealed class RuntimeServicesTests
{
    [Xunit.Fact]
    public void ReflectionServiceExposesTypesMethodsAndFields()
    {
        var source = """
            public class Box<T>
            {
                public static int Counter;
                public T Value;

                public U Echo<U>(U x) => x;
            }

            public static class Program
            {
                public static int Main() => 0;
            }
            """;

        var assemblyPath = Compile(source);
        using var host = RuntimeHost.Load(assemblyPath);

        var box = host.Reflection.GetType("Box`1");
        Xunit.Assert.NotNull(box);
        Xunit.Assert.Equal(1, box!.GenericArity);
        Xunit.Assert.Equal("T", box.GenericParameters.Single());

        var counter = box.Fields.Single(f => f.Name == "Counter");
        Xunit.Assert.True(counter.IsStatic);
        Xunit.Assert.Equal("Int32", counter.FieldTypeCode);

        var echo = box.Methods.Single(m => m.Name == "Echo");
        Xunit.Assert.Equal(1, echo.GenericArity);
        Xunit.Assert.Equal("U", echo.GenericParameters.Single());

        Xunit.Assert.NotNull(host.TypeSystem.GetType("Box"));
    }

    [Xunit.Fact]
    public void SnapshotCapturesRuntimeStateAfterExecution()
    {
        var source = """
            public static class Program
            {
                public static int Main()
                {
                    int a = 40;
                    int b = 2;
                    return a + b;
                }
            }
            """;

        var assemblyPath = Compile(source);
        using var host = RuntimeHost.Load(assemblyPath);
        var exitCode = host.RunEntryPoint();
        var snapshot = host.CaptureSnapshot();

        Xunit.Assert.Equal(42, exitCode);
        Xunit.Assert.Equal(42, snapshot.LastExitCode);
        Xunit.Assert.True(snapshot.TypeCount > 0);
        Xunit.Assert.True(snapshot.MethodCount > 0);
        Xunit.Assert.True(snapshot.JitPlanCount > 0);
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
            assemblyName: $"DotforgeRuntimeServices_{Guid.NewGuid():N}",
            syntaxTrees: [tree],
            references: refs,
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.ConsoleApplication,
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
