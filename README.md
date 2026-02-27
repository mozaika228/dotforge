# dotforge

Experimental CLR-like runtime scaffold for a .NET-style language.

Current vertical slice:
- Load managed assembly metadata (ECMA-335 tables via `System.Reflection.Metadata`).
- Resolve entry point method (`Main` token from CLR header).
- Decode a small IL opcode subset.
- Interpret method body in a minimal stack VM.
- Execute simple object flows: `newobj`, `.ctor`, `ldfld`, `stfld`.
- Handle `System.Console.WriteLine` as an intrinsic call.

## Layout

- `src/Dotforge.Metadata`: PE + metadata loading.
- `src/Dotforge.IL`: IL instruction model + decoder.
- `src/Dotforge.Runtime`: minimal VM execution engine.
- `src/Dotforge.Cli`: command line host.
- `tests/`: test placeholder project structure.

## Scope (M0)

This is intentionally a foundation, not a full CLR.
JIT/GC/type-system/reflection/EH should be added incrementally on top of this baseline.
