# DCL.Analyzers — Roslyn analyzers for unity-explorer

Compile-time semantic checks that run inside Unity's C# compilation (and in
Rider/VS), covering the rules the regex layer (`scripts/lint/custom-rules.sh`)
fundamentally cannot: anything requiring symbol resolution, type checks, or
statement ordering.

## Rules

| ID | Severity | What it catches |
|---|---|---|
| DCLA001 | Warning | A `ref` local obtained from `Arch.Core.World.Get`/`TryGetRef` is used **after** a structural change (`World.Add/Remove/Create/Destroy/…`) in the same method body. Structural changes relocate entity data between archetype chunks, so the ref points at stale memory — writes are silently lost, reads observe garbage (CLAUDE.md § Safe Component Mutation). `EntityCommandBuffer` operations never trip it (type-checked). Ordering is linear source position with two reachability carve-outs (calibrated on a real 219-assembly compile): uses inside the structural call's own argument list (pre-call evaluation) and call/use pairs in mutually exclusive branches (if/else, switch sections, ternary arms — the `TryGetRef`-then-branch idiom) stay silent. Residual FPs (exclusivity via early return) are suppressible with `#pragma warning disable DCLA001`. |
| DCLA002 | Warning | A detached UniTask flow — an `async UniTaskVoid` method/local function/lambda, or a same-file method (incl. local functions, extension methods, partial implementations) detached via a bare UniTask `.Forget()` — has no exception handling of its own (no `catch (Exception)`/general catch and no `SuppressToResultAsync`/`SuppressToResult` in its own body — guards inside nested lambdas/local functions don't count, and neither does `SuppressCancellationThrow`, which only swallows cancellation), so an unhandled exception is silently swallowed instead of being reported via `ReportHub.LogException` (CLAUDE.md § Async Flow Guidelines; async-programming skill). Only Cysharp's own `Forget` counts; `Forget(exceptionHandler)` is guarded at the callsite; an `async UniTaskVoid` target of `.Forget()` is reported once, at its declaration. |
| DCLA003 | Warning | A heap allocation inside a `BaseUnityLoopSystem` `Update()` override or a `[Query]`-attributed method of such a system (the generated update path dispatches to those, per entity) — reference-type construction (incl. target-typed `new` and anonymous objects), array creation, capturing lambda, non-constant string interpolation/concatenation (incl. `+=`), or a LINQ (`Enumerable`/`Queryable`) call — accumulates into per-frame GC pressure across multiple world executions (CLAUDE.md § Performance Constraints); the check is body-only (allocations in callees are not chased), and error-path work is exempt: allocations under a `throw`, and construction of `Exception`-derived types anywhere (thrown, wrapped into a Result, or logged — calibrated on a real compile where every such hit was a failure branch). Any method in any assembly can opt in with `[Utility.HotPath]` — for per-frame or per-network-call code outside ECS systems (URL handlers, MCP runtime). |
| DCLA004 | Warning | A local rented with `Get()` from an object pool (`UnityEngine.Pool` `ListPool`/`HashSetPool`/`DictionaryPool`/`GenericPool`/`ObjectPool` or any `IObjectPool`/`IExtendedObjectPool` implementation) provably leaks — never passed to `Release`/`Return` or any other call, never returned, stored, aliased (incl. is-pattern designations), captured outside its declaring scope, or scoped by a `using` — permanently removing the instance from the pool (CLAUDE.md § Component Clean-up Patterns; code-standards skill § Memory; ecs-system-and-component-design skill § cleanup); deliberately conservative, any escape silences the rule, including member invocations on rentals from arbitrary-object pools (the self-release idiom — the object's own method may schedule `pool.Release(this)`); BCL collection rentals can't self-release, so member calls on them don't silence it, and rentals declared inside a lambda/local function are checked within that scope. A rental assigned inside another call's argument list (`Attach(x = pool.Get())`) escapes through the argument and stays silent. |
| DCLA005 | Warning | An enum crossing a `[DllImport]` boundary (parameter, return type, or a field of an unmanaged struct parameter) without an explicit underlying type (`: byte`, `: int`, …) — native code compiles against a fixed ABI layout and C#'s implicit `int` default is a convention nothing pins (review-enforced, PR #9088). Only source-declared enums are checked; metadata enums cannot reveal whether their base was written explicitly. |

## How Unity picks it up

`Explorer/Assets/DCL/DCL.Analyzers.dll` carries the `RoslynAnalyzer` asset
label with all platforms disabled (same pattern as the CodeLess and Arch
source-generator DLLs). Unity feeds a labeled analyzer to `csc` for every
assembly whose asmdef lives in the analyzer's folder or below — placing it at
`Assets/DCL/` covers all first-party code and nothing under `Assets/Plugins`.

The DLL targets `netstandard2.0` against `Microsoft.CodeAnalysis.CSharp 4.3.1`
— the standard analyzer floor, loadable by any Roslyn host ≥ 4.3 (Unity
6000.x bundles ≥ 4.9, as do Rider and the dotnet SDK).

## Building

```bash
bash scripts/build-analyzers.sh   # tests + Release build + sync DLL into Assets/DCL
```

Requires a dotnet 8 SDK (auto-wraps itself in `nix-shell -p dotnet-sdk_8` when
dotnet is absent). The DLL is LFS-tracked like every other `.dll`; commit the
updated DLL together with the source change. CI (`analyzers` job in
`test.yml`) runs the test suite on every change under `Analyzers/` — it does
not rebuild the shipped DLL, so keep it in sync via the script.

## Adding a rule

1. New `DiagnosticAnalyzer` class in `DCL.Analyzers/`, next ID (`DCLA00N`).
2. Tests in `DCL.Analyzers.Tests/` — stub external types (see the Arch stub in
   `StructuralChangeAfterRefTests`); the analyzer must match on metadata names,
   never on assembly identity, so stubs work.
3. `bash scripts/build-analyzers.sh`, commit source + DLL together.
4. Add the rule row to this table, and cite the rule's source doc in its
   descriptor description.
