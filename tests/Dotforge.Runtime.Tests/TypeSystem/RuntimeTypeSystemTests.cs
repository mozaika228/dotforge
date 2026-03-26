using Dotforge.Metadata;
using Dotforge.Runtime.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dotforge.Runtime.Tests.TypeSystem;

public sealed class RuntimeTypeSystemTests
{
    [Xunit.Fact]
    public void ReadsGenericTypeAndMethodArity()
    {
        var source = """
            public class Box<T>
            {
                public T Value;
                public U Echo<U>(U x) => x;
            }

            public static class Program
            {
                public static int Main() => 0;
            }
            """;

        var assemblyPath = Compile(source);
        using var assembly = ManagedAssembly.Load(assemblyPath);
        var ts = RuntimeTypeSystem.Build(assembly);
        var box = ts.GetType("Box");

        Xunit.Assert.NotNull(box);
        Xunit.Assert.Equal(1, box!.GenericArity);
        Xunit.Assert.Equal("T", box.GenericParameters.Single());
        var echo = box.Methods.Single(m => m.Handle.Name == "Echo");
        Xunit.Assert.Equal(1, echo.GenericArity);
        Xunit.Assert.Equal("U", echo.GenericParameters.Single());
    }

    [Xunit.Fact]
    public void InstantiatesGenericTypeDescriptor()
    {
        var source = """
            public class Box<T> { }
            public static class Program { public static int Main() => 0; }
            """;

        var assemblyPath = Compile(source);
        using var assembly = ManagedAssembly.Load(assemblyPath);
        var ts = RuntimeTypeSystem.Build(assembly);
        var box = ts.GetType("Box");
        Xunit.Assert.NotNull(box);

        var inst = ts.InstantiateType(
            box!.Handle,
            [new RuntimeTypeHandle(0, "System.Int32")]);

        Xunit.Assert.Equal("Box<System.Int32>", inst.ToString());
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
            assemblyName: $"DotforgeTypeTest_{Guid.NewGuid():N}",
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
