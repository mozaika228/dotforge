using Dotforge.Metadata;
using Dotforge.Metadata.Reflection;
using Dotforge.Runtime.Gc;
using Dotforge.Runtime.Jit;
using Dotforge.Runtime.TypeSystem;

namespace Dotforge.Runtime.Services;

public sealed class RuntimeHost : IDisposable
{
    private readonly object _gate = new();
    private readonly bool _ownsAssembly;
    private GcStats _lastObservedGcStats = new(0, 0, 0, 0, 0, 0, 0);
    private int _executionCount;
    private int _successfulRuns;
    private int _failedRuns;
    private int _maxObservedJitPlans;
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
        lock (_gate)
        {
            return ExecuteWithVmCore(Vm);
        }
    }

    public int RunEntryPointIsolated()
    {
        var vm = new MiniVm();
        return ExecuteWithVmCore(vm);
    }

    public async Task<IReadOnlyList<int>> RunEntryPointParallelAsync(int runCount, CancellationToken cancellationToken = default)
    {
        if (runCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runCount), runCount, "runCount must be positive.");
        }

        var tasks = Enumerable.Range(0, runCount)
            .Select(_ => Task.Run(RunEntryPointIsolated, cancellationToken))
            .ToArray();

        await Task.WhenAll(tasks);
        return tasks.Select(static t => t.Result).ToArray();
    }

    public GcStats GetGcStats() => Vm.GetGcStats();

    public IReadOnlyDictionary<int, JitCompilationPlan> GetJitPlans() => Vm.GetJitPlans();

    public RuntimeSnapshot CaptureSnapshot()
    {
        var types = Reflection.GetTypes();
        var methodCount = types.Sum(static t => t.Methods.Count);
        lock (_gate)
        {
            return new RuntimeSnapshot(
                TypeCount: types.Count,
                MethodCount: methodCount,
                JitPlanCount: Math.Max(Vm.GetJitPlans().Count, _maxObservedJitPlans),
                GcStats: _lastObservedGcStats,
                LastExitCode: _lastExitCode,
                ExecutionCount: _executionCount,
                SuccessfulRuns: _successfulRuns,
                FailedRuns: _failedRuns);
        }
    }

    public void Dispose()
    {
        if (_ownsAssembly)
        {
            Assembly.Dispose();
        }
    }

    private int ExecuteWithVmCore(MiniVm vm)
    {
        lock (_gate)
        {
            _executionCount++;
        }

        try
        {
            var exitCode = vm.ExecuteEntryPoint(Assembly);
            var gc = vm.GetGcStats();
            var jitPlans = vm.GetJitPlans().Count;

            lock (_gate)
            {
                _successfulRuns++;
                _lastExitCode = exitCode;
                _lastObservedGcStats = gc;
                _maxObservedJitPlans = Math.Max(_maxObservedJitPlans, jitPlans);
            }

            return exitCode;
        }
        catch
        {
            lock (_gate)
            {
                _failedRuns++;
            }

            throw;
        }
    }
}
