namespace Dotforge.Runtime.Jit.Passes;

public sealed class IrPassManager
{
    private readonly IReadOnlyList<IIrPass> _passes;

    public IrPassManager(params IIrPass[] passes)
    {
        _passes = passes;
    }

    public IrFunction Run(IrFunction function)
    {
        var current = function;
        foreach (var pass in _passes)
        {
            current = pass.Run(current);
        }

        return current;
    }
}
