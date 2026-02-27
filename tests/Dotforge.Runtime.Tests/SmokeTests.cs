namespace Dotforge.Runtime.Tests;

public sealed class SmokeTests
{
    [Xunit.Fact]
    public void RuntimeAssemblyLoads()
    {
        var vmType = typeof(MiniVm);
        Xunit.Assert.NotNull(vmType);
    }
}
