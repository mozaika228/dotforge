using Dotforge.Runtime.Gc;

namespace Dotforge.Runtime.Services;

public sealed record RuntimeSnapshot(
    int TypeCount,
    int MethodCount,
    int JitPlanCount,
    GcStats GcStats,
    int? LastExitCode);
