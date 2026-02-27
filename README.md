# dotforge

Experimental CLR-like runtime scaffold for a .NET-style language.

Current vertical slice:
- Load managed assembly metadata (ECMA-335 tables via `System.Reflection.Metadata`).
- Resolve entry point method (`Main` token from CLR header).
- Decode expanded IL subset (`ldarg`, `ldloc`, `stloc`, arithmetic, comparisons, branches, `throw`, `callvirt`, field ops).
- Interpret method body in a stack VM with locals.
- Execute simple object flows: `newobj`, `.ctor`, `ldfld`, `stfld`.
- Inline caching for `callvirt` call sites (`token + runtime type`).
- Basic managed exception handling for `catch` regions.
- Handle `System.Console.WriteLine` as an intrinsic call.
- Provide metadata reflection catalog (`types`, `methods`, `fields`) via `dotforge inspect`.
- Include a generational heap foundation (young/old, write barrier, minor/major collection API).

## Layout

- `src/Dotforge.Metadata`: PE + metadata loading.
- `src/Dotforge.IL`: IL instruction model + decoder.
- `src/Dotforge.Runtime`: minimal VM execution engine.
- `src/Dotforge.Cli`: command line host.
- `tests/`: test placeholder project structure.

## Scope (M0)

This is intentionally a foundation, not a full CLR.
JIT/GC/type-system/reflection/EH are incremental and currently partial.

## CI

GitHub Actions workflow is in `.github/workflows/ci.yml` (`restore + build + test` on push/PR to `main`).
