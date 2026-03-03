# DotForge

[![Build](https://github.com/mozaika228/dotforge/actions/workflows/ci.yml/badge.svg)](https://github.com/mozaika228/dotforge/actions/workflows/ci.yml)
[![Coverage](https://codecov.io/gh/mozaika228/dotforge/branch/main/graph/badge.svg)](https://codecov.io/gh/mozaika228/dotforge)
[![License: MIT](https://img.shields.io/github/license/mozaika228/dotforge)](LICENSE)

DotForge is a CLR-style runtime project focused on executing managed IL with a production-oriented architecture: metadata loading, IL decoding, interpreter execution, and generational GC foundations.

## Architecture Overview

```mermaid
flowchart LR
  A[Managed Assembly PE] --> B[Metadata Loader<br/>ECMA-335 tables]
  B --> C[IL Decoder]
  C --> D[Interpreter VM]
  D --> E[Object/Array Model]
  E --> F[Generational GC<br/>Gen0/Gen1 + write barrier]
  B --> G[Reflection Catalog<br/>Type/Method/Field tables]
  C --> H[JIT IR Plan (in progress)]
```

## Features

- ECMA-335 metadata loading (`TypeDef`, `MethodDef`, `FieldDef`, member refs).
- IL decoding for core control-flow and object ops:
  - args/locals (`ldarg*`, `ldloc*`, `stloc*`)
  - constants and strings (`ldc.i4*`, `ldstr`, `ldnull`)
  - math/comparisons (`add`, `sub`, `mul`, `div`, `ceq`, `cgt`, `clt`)
  - branching (`br*`, `brtrue*`, `brfalse*`, `leave*`)
  - object/field (`newobj`, `ldfld`, `stfld`)
  - arrays (`newarr`, `ldlen`, `ldelem.i4`, `stelem.i4`, `ldelem.ref`, `stelem.ref`)
  - boxing (`box`, `unbox`, `unbox.any`)
  - calls (`call`, `callvirt`, `calli` baseline for `ldftn` function pointers)
  - exceptions (`throw`, catch-region handling baseline)
- Inline cache for `callvirt` dispatch (`method token + runtime type`).
- Runtime object model for `int32`, `string`, object instances, and arrays.
- Generational GC foundation:
  - Gen0 nursery + Gen1 old generation
  - mark/sweep-style collection flow
  - write barrier + remembered set
  - collection logging (`DOTFORGE_GC_LOG=1`)
- Metadata reflection catalog for type/method/field inspection.
- JIT planning scaffold (`IL -> IR`) to support native backend work.

## CLI

- `dotforge run <assembly>`: execute assembly entry point (`Main`).
- `dotforge inspect <assembly>`: dump metadata types/methods/fields.
- `dotforge disasm <assembly> <method-token-or-Type::Method>`: dump decoded IL.

Examples:

```bash
dotforge run ./samples/Hello.dll
dotforge inspect ./samples/Hello.dll
dotforge disasm ./samples/Hello.dll Program::Main
```

## Build & Run

```bash
dotnet restore dotforge.sln
dotnet build dotforge.sln -c Release
dotnet test dotforge.sln -c Release
```

## Repository Structure

- `src/Dotforge.Metadata`: PE/metadata reader + reflection catalog.
- `src/Dotforge.IL`: IL opcode model and decoder.
- `src/Dotforge.Runtime`: VM, object model, arrays, GC foundation, JIT IR scaffold.
- `src/Dotforge.Cli`: `run`, `inspect`, `disasm` commands.
- `tests/Dotforge.Runtime.Tests`: xUnit integration and opcode coverage tests.
- `.github/workflows/ci.yml`: CI restore/build/test + coverage artifact upload.

## Roadmap

- JIT backend: SSA/IR lowering -> native codegen (LLVM/C++ backend).
- Full reflection: richer signatures, generics, attributes, runtime handles.
- Exception handling: complete `try/catch/finally`, filter semantics, unwind fidelity.
- AOT pipeline for ahead-of-time compilation and startup optimization.
