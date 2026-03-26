using Dotforge.Metadata;
using Dotforge.Metadata.Reflection;
using Dotforge.Runtime.Gc;
using Dotforge.Runtime.Jit;
using Dotforge.Runtime.TypeSystem;

namespace Dotforge.Runtime.Services;

public sealed class RuntimeHost : IDisposable
{
    private readonly bool _ownsAssembly;
    private int? _lastExitCode;

    public RuntimeHost(ManagedAssembly assembly, MiniVm? vm = null, bool ownsAssembly = false)
    {
        Assembly = assembly;
        Vm = vm ?? new MiniVm();
        _ownsAssembly = ownsAssembly;

        var types = new MetadataCatalog(assembly).GetTypes();
        Reflection = new RuntimeReflectionService(types);
        TypeSystem = RuntimeTypeSystem.Build(assembly);
    }

    public ManagedAssembly Assembly { get; }
    public MiniVm Vm { get; }
    public RuntimeReflectionService Reflection { get; }
    public RuntimeTypeSystem TypeSystem { get; }

    public static RuntimeHost Load(string assemblyPath, MiniVm? vm = null)
    {
        return new RuntimeHost(ManagedAssembly.Load(assemblyPath), vm, ownsAssembly: true);
    }

    public int RunEntryPoint()
    {
        _lastExitCode = Vm.ExecuteEntryPoint(Assembly);
        return _lastExitCode.Value;
    }

    public GcStats GetGcStats() => Vm.GetGcStats();

    public IReadOnlyDictionary<int, JitCompilationPlan> GetJitPlans() => Vm.GetJitPlans();

    public RuntimeSnapshot CaptureSnapshot()
    {
        var types = Reflection.GetTypes();
        var methodCount = types.Sum(static t => t.Methods.Count);
        return new RuntimeSnapshot(
            TypeCount: types.Count,
            MethodCount: methodCount,
            JitPlanCount: Vm.GetJitPlans().Count,
            GcStats: Vm.GetGcStats(),
            LastExitCode: _lastExitCode);
    }

    public void Dispose()
    {
        if (_ownsAssembly)
        {
            Assembly.Dispose();
        }
    }
}
