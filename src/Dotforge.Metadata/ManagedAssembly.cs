using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotforge.Metadata;

public sealed class ManagedAssembly : IDisposable
{
    private readonly FileStream _stream;
    private readonly PEReader _peReader;

    private ManagedAssembly(string path, FileStream stream, PEReader peReader, MetadataReader metadata)
    {
        Path = path;
        _stream = stream;
        _peReader = peReader;
        Metadata = metadata;
    }

    public string Path { get; }
    public MetadataReader Metadata { get; }

    public static ManagedAssembly Load(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));
        }

        var fullPath = System.IO.Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Assembly was not found.", fullPath);
        }

        var stream = File.OpenRead(fullPath);
        PEReader peReader;
        try
        {
            peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                throw new InvalidOperationException($"'{fullPath}' does not contain CLR metadata.");
            }

            var metadata = peReader.GetMetadataReader();
            return new ManagedAssembly(fullPath, stream, peReader, metadata);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public MethodDefinitionHandle GetEntryPoint()
    {
        var corHeader = _peReader.PEHeaders.CorHeader
            ?? throw new InvalidOperationException("Invalid CLR header.");

        if (corHeader.EntryPointTokenOrRelativeVirtualAddress == 0)
        {
            throw new InvalidOperationException("Assembly has no managed entry point.");
        }

        var entryHandle = MetadataTokens.EntityHandle(corHeader.EntryPointTokenOrRelativeVirtualAddress);
        if (entryHandle.Kind != HandleKind.MethodDefinition)
        {
            throw new NotSupportedException($"Unsupported entry point handle kind: {entryHandle.Kind}.");
        }

        return (MethodDefinitionHandle)entryHandle;
    }

    public MethodBodyBlock GetMethodBody(MethodDefinitionHandle methodHandle)
    {
        var method = Metadata.GetMethodDefinition(methodHandle);
        if (method.RelativeVirtualAddress == 0)
        {
            throw new InvalidOperationException("Method has no body RVA.");
        }

        return _peReader.GetMethodBody(method.RelativeVirtualAddress);
    }

    public void Dispose()
    {
        _peReader.Dispose();
        _stream.Dispose();
    }
}
