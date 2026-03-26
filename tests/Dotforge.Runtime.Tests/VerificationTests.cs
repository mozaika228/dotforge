using Dotforge.Metadata;
using Dotforge.Metadata.Loader;
using Dotforge.Metadata.Verification;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dotforge.Runtime.Tests;

public sealed class VerificationTests
{
    [Xunit.Fact]
    public void ValidAssemblyPassesMetadataAndIlVerification()
    {
        var source = """
            public static class Program
            {
                public static int Main()
                {
                    int x = 40;
                    int y = 2;
                    return x + y;
                }
            }
            """;

        var assemblyPath = Compile(source, "VerifyOk");
        using var assembly = ManagedAssembly.Load(assemblyPath);
        var md = MetadataValidator.Validate(assembly);
        var il = IlVerifierLite.Verify(assembly);
        Xunit.Assert.False(md.HasErrors);
        Xunit.Assert.False(il.HasErrors);
    }

    [Xunit.Fact]
    public void LoaderContextResolvesLocalAssemblyReference()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dotforge-load-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var libPath = Path.Combine(tempRoot, "MyLib.dll");
        var appPath = Path.Combine(tempRoot, "MyApp.dll");

        CompileToPath("""
            public static class Helper
            {
                public static int Value() => 7;
            }
            """, "MyLib", libPath, outputKind: OutputKind.DynamicallyLinkedLibrary);

        CompileToPath("""
            public static class Program
            {
                public static int Main() => Helper.Value();
            }
            """, "MyApp", appPath, [MetadataReference.CreateFromFile(libPath)], OutputKind.ConsoleApplication);

        using var context = new AssemblyLoadContextLite(tempRoot);
        using var appAssembly = context.Load(appPath);
        var unresolved = context.ResolveAllReferences(appAssembly);

        Xunit.Assert.False(unresolved.Any(x => string.Equals(x, "MyLib", StringComparison.OrdinalIgnoreCase)));
        Xunit.Assert.NotNull(context.ResolveAssemblyReference("MyLib"));
    }

    private static string Compile(string source, string name)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dotforge-tests");
        Directory.CreateDirectory(tempRoot);
        var assemblyPath = Path.Combine(tempRoot, $"{name}_{Guid.NewGuid():N}.dll");
        CompileToPath(source, name, assemblyPath);
        return assemblyPath;
    }

    private static void CompileToPath(
        string source,
        string assemblyName,
        string outputPath,
        IReadOnlyList<MetadataReference>? extraRefs = null,
        OutputKind outputKind = OutputKind.ConsoleApplication)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp12));
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToList();

        if (extraRefs is not null)
        {
            refs.AddRange(extraRefs);
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: $"{assemblyName}_{Guid.NewGuid():N}",
            syntaxTrees: [tree],
            references: refs,
            options: new CSharpCompilationOptions(
                outputKind: outputKind,
                optimizationLevel: OptimizationLevel.Debug,
                nullableContextOptions: NullableContextOptions.Enable));

        var emitResult = compilation.Emit(outputPath);
        if (!emitResult.Success)
        {
            var diagnostics = string.Join(Environment.NewLine, emitResult.Diagnostics);
            throw new InvalidOperationException($"Compilation failed:{Environment.NewLine}{diagnostics}");
        }
    }
}
