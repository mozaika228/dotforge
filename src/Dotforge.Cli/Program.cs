using Dotforge.Metadata;
using Dotforge.Metadata.Reflection;
using Dotforge.Runtime;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  dotforge <path-to-managed-assembly>");
    Console.Error.WriteLine("  dotforge inspect <path-to-managed-assembly>");
    return 1;
}

var inspectMode = string.Equals(args[0], "inspect", StringComparison.OrdinalIgnoreCase);
if (inspectMode && args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotforge inspect <path-to-managed-assembly>");
    return 1;
}

var assemblyPath = inspectMode ? args[1] : args[0];
using var assembly = ManagedAssembly.Load(assemblyPath);
if (inspectMode)
{
    var catalog = new MetadataCatalog(assembly);
    foreach (var type in catalog.GetTypes())
    {
        Console.WriteLine($"type 0x{type.Token:X8} {type.FullName}");
        foreach (var field in type.Fields)
        {
            Console.WriteLine($"  field 0x{field.Token:X8} {field.Name}");
        }

        foreach (var method in type.Methods)
        {
            var mode = method.IsStatic ? "static" : "instance";
            Console.WriteLine($"  method 0x{method.Token:X8} {mode} {method.Name} ({method.ParameterCount}) -> {method.ReturnTypeCode}");
        }
    }

    return 0;
}
else
{
    var vm = new MiniVm();
    return vm.ExecuteEntryPoint(assembly);
}
