namespace Dotforge.Runtime.Jit.Passes;

public interface IIrPass
{
    string Name { get; }
    IrFunction Run(IrFunction input);
}
