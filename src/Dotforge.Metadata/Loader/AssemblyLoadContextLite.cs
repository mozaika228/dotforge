using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotforge.Metadata.Loader;

public sealed class AssemblyLoadContextLite : IDisposable
{
    private readonly Dictionary<string, ManagedAssembly> _loadedBySimpleName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _probingDirectories = [];

    public AssemblyLoadContextLite(params string[] probingDirectories)
    {
        foreach (var dir in probingDirectories.Where(static x => !string.IsNullOrWhiteSpace(x)))
        {
            var full = Path.GetFullPath(dir);
            if (Directory.Exists(full))
            {
                _probingDirectories.Add(full);
            }
        }
    }

    public IReadOnlyDictionary<string, ManagedAssembly> LoadedAssemblies => _loadedBySimpleName;

    public ManagedAssembly Load(string path)
    {
        var asm = ManagedAssembly.Load(path);
        var name = GetSimpleAssemblyName(asm.Metadata);
        _loadedBySimpleName[name] = asm;
        AddProbingDirectory(Path.GetDirectoryName(asm.Path));
        return asm;
    }

    public ManagedAssembly? ResolveAssemblyReference(string simpleName)
    {
        if (_loadedBySimpleName.TryGetValue(simpleName, out var loaded))
        {
            return loaded;
        }

        foreach (var dir in _probingDirectories)
        {
            var candidate = Path.Combine(dir, $"{simpleName}.dll");
            if (!File.Exists(candidate))
            {
                continue;
            }

            var asm = Load(candidate);
            return asm;
        }

        return null;
    }

    public IReadOnlyList<string> ResolveAllReferences(ManagedAssembly root)
    {
        var unresolved = new List<string>();
        var metadata = root.Metadata;
        foreach (var referenceHandle in metadata.AssemblyReferences)
        {
            var reference = metadata.GetAssemblyReference(referenceHandle);
            var name = metadata.GetString(reference.Name);
            if (ResolveAssemblyReference(name) is null)
            {
                unresolved.Add(name);
            }
        }

        return unresolved;
    }

    private void AddProbingDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var full = Path.GetFullPath(directory);
        if (!_probingDirectories.Contains(full, StringComparer.OrdinalIgnoreCase))
        {
            _probingDirectories.Add(full);
        }
    }

    private static string GetSimpleAssemblyName(MetadataReader metadata)
    {
        var def = metadata.GetAssemblyDefinition();
        return metadata.GetString(def.Name);
    }

    public void Dispose()
    {
        foreach (var asm in _loadedBySimpleName.Values.Distinct())
        {
            asm.Dispose();
        }

        _loadedBySimpleName.Clear();
    }
}
