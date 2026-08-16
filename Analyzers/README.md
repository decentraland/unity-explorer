# DCL.Analyzers — Roslyn analyzers for unity-explorer

Semantic checks running inside Unity's C# compilation (and Rider/VS), covering
what the regex layer (`scripts/lint/custom-rules.sh`) cannot: symbol
resolution, type checks, statement ordering. Full semantics live in each
analyzer's XML doc; severities are pinned in `Explorer/.editorconfig`.

## Rules

| ID | Severity | What it catches |
|---|---|---|
| DCLA001 | error | `ref` local from `World.Get`/`TryGetRef` used after a structural change (`Add/Remove/Create/Destroy`) relocated it. Reachability-aware: exclusive branches, pre-call arguments, and the `x = ref World.Get(...)` re-fetch idiom stay silent; `CommandBuffer` never trips it. |
| DCLA002 | warning | Detached UniTask flow (`async UniTaskVoid`, same-file `.Forget()`) with no exception handling of its own. `Forget(handler)` counts as guarded. |
| DCLA003 | warning | Heap allocation in per-frame code: system `Update()`/`[Query]` bodies, plus any `[Utility.HotPath]` method in any assembly. Throw paths and `Exception` construction are exempt (error-path work). |
| DCLA004 | warning | Pooled `Get()` rental that provably never escapes or releases. Any escape silences it, including self-release member calls on non-collection rentals. |
| DCLA005 | error | Enum crossing a `[DllImport]` boundary (param, return, unmanaged-struct field) without an explicit underlying type. Source-declared enums only. |
| DCLA006 | warning | Type owning a teardown method (Dispose/DisposeAsync/OnDestroy/OnDisable) subscribes (`+=`) to a C# event it does not own and never unsubscribes anywhere in the type. Corpus-calibrated silencers: own events, local-rooted receivers, parameters not retained by a constructor, create-and-dispose pairings (field new'd by the type and touched in teardown), and self-removing handlers; one `-=` of an event silences its subscriptions type-wide. UnityEvent AddListener is deliberately out of scope (serialized-child wiring is the standard idiom). |

## Integration

`Explorer/Assets/DCL/DCL.Analyzers.dll` carries the `RoslynAnalyzer` label
(all platforms disabled) — Unity feeds it to `csc` for every asmdef at or
below `Assets/DCL/`, so first-party code only, nothing vendored. Built as
netstandard2.0 against Microsoft.CodeAnalysis.CSharp 4.3.1 (loads in any
Roslyn host ≥ 4.3; Unity 6000.x bundles ≥ 4.9).

## Building

```bash
bash scripts/build-analyzers.sh   # tests + Release build + DLL sync
```

Self-wraps in `nix-shell -p dotnet-sdk_8` when dotnet is absent. The DLL is
LFS-tracked; commit it with the source change. CI (`analyzers` job) runs the
test suite on `Analyzers/` changes but does not rebuild the shipped DLL.

## Adding a rule

New `DiagnosticAnalyzer` (next `DCLA00N`) + tests stubbing external types by
metadata name (see any existing test file), `bash scripts/build-analyzers.sh`,
a row here, a severity pin in `Explorer/.editorconfig`.
