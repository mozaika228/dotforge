# DotForge Production Definition Of Done

This checklist defines the minimum bar for shipping to production (release tag or default branch release candidate).

## 1. Functional Correctness

- All CI jobs pass on `main` (`restore`, `build`, `test`).
- New runtime features include automated tests (unit/integration) covering happy path and failure path.
- No known `P0` or `P1` correctness bugs remain open for the release scope.
- `dotforge run`, `inspect`, `disasm`, `verify`, and `runtime` commands are validated on representative assemblies.

## 2. Runtime Safety

- Metadata validation and IL verification gates are enabled and documented for production usage.
- Exception handling behavior is deterministic for covered scenarios (`catch`, `finally`, unwind path).
- GC invariants are preserved in tests (allocation, collection, write barrier, handle semantics).
- Threaded host execution paths are tested for race/regression with parallel runs.

## 3. Performance And Capacity

- Baseline performance numbers are captured for interpreter throughput and allocation-heavy scenarios.
- No regression above agreed threshold (default: 10%) versus previous release baseline.
- Runtime snapshot metrics (`jit`, `gc`, run counters) are available for diagnostics.

## 4. Security And Supply Chain

- Dependencies restore from trusted sources only (NuGet default or explicitly approved feeds).
- No high/critical vulnerabilities in direct dependencies at release time.
- Release artifacts are reproducible from tagged source + documented toolchain (`global.json`).

## 5. Operability

- Logging/diagnostic path exists for GC and runtime execution (`DOTFORGE_GC_LOG`, runtime snapshot).
- Failure modes are actionable: error messages identify failing stage (loader, verifier, VM, interop, JIT plan).
- README and CLI help are aligned with actual commands and arguments.

## 6. Release Hygiene

- Changelog/release notes include major runtime changes, breaking changes, and migration notes.
- License, badges, and repository metadata are valid.
- Release candidate is tagged only after all checklist items are complete.

## 7. Documentation Required For Completion

- Architecture section reflects the current runtime shape.
- New public API surfaces are documented (example usage + constraints).
- Known limitations are explicitly listed (for example: partial JIT backend, subset of IL support).

## 8. Final Sign-Off

- Engineering sign-off: feature owner confirms scope and risk.
- QA sign-off: test evidence attached (CI run link and test summary).
- Release sign-off: repository state clean, commit/tag pushed, rollback plan defined.
