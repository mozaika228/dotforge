namespace Dotforge.Runtime.Jit;

public enum IrOpCode
{
    Label,
    ConstI4,
    LoadArg,
    LoadLocal,
    StoreLocal,
    Add,
    Sub,
    Mul,
    Div,
    Ceq,
    Cgt,
    Clt,
    Br,
    BrTrue,
    BrFalse,
    Ret
}
