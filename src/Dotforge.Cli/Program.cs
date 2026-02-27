using Dotforge.Metadata;
using Dotforge.Runtime;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: dotforge <path-to-managed-assembly>");
    return 1;
}

var assemblyPath = args[0];
using var assembly = ManagedAssembly.Load(assemblyPath);
var vm = new MiniVm();
return vm.ExecuteEntryPoint(assembly);
