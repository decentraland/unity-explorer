# DCL.Analyzers — Roslyn analyzers for unity-explorer

Semantic checks running inside Unity's C# compilation (and Rider/VS), covering
what the regex layer (`scripts/lint/custom-rules.sh`) cannot: symbol
resolution, type checks, statement ordering. Full semantics live in each
analyzer's XML doc.

Severities: Unity's csc **ignores** `.editorconfig` `dotnet_diagnostic` entries
(verified with a probe violation: it compiled as a warning despite an `error`
pin), so the corruption-class rules (DCLA001, DCLA005) carry
`DiagnosticSeverity.Error` in their descriptors — that is what fails the Unity
build. The pins in `Explorer/.editorconfig` govern IDEs and `dotnet` builds,
including the `**/Tests/**` downgrade, which is therefore IDE-only: Unity test
assemblies get the error severity too.

## Rules

| ID | Severity | What it catches |
|---|---|---|
| DCLA001 | error | `ref` local from `World.Get`/`TryGetRef` used after a structural change (`Add/Remove/Create/Destroy`) relocated it. Reachability-aware: exclusive branches, pre-call arguments, and the `x = ref World.Get(...)` re-fetch idiom stay silent; `CommandBuffer` never trips it. |
| DCLA002 | warning | Detached UniTask flow (`async UniTaskVoid`, same-file `.Forget()`) with no exception handling of its own. `Forget(handler)` counts as guarded. |
| DCLA003 | warning | Heap allocation in per-frame code: system `Update()`/`[Query]` bodies, plus any `[Utility.HotPath]` method in any assembly. Throw paths and `Exception` construction are exempt (error-path work). |
| DCLA004 | warning | Pooled `Get()` rental that provably never escapes or releases. Any escape silences it, including self-release member calls on non-collection rentals. |
| DCLA005 | error | Enum crossing a `[DllImport]` boundary (param, return, unmanaged-struct field) without an explicit underlying type. Source-declared enums only. |

## Integration

`Explorer/Assets/DCL/DCL.Analyzers.dll` carries the `RoslynAnalyzer` label
(all platforms disabled). In practice Unity feeds it to more compilations
than the label's folder suggests — including registry/git packages resolved
into `Library/PackageCache` (observed: DCLA005 firing inside
`com.decentraland.pulse.transport` and `com.unity.cloud.ktx`). Vendored
sources can't be fixed here, so the analyzers scope themselves: every rule
skips syntax trees whose path contains `/PackageCache/` (`VendoredCode.cs`);
first-party code under `Assets/` is analyzed in full. Built as
netstandard2.0 against Microsoft.CodeAnalysis.CSharp 4.3.1 (loads in any
Roslyn host ≥ 4.3; Unity 6000.x bundles ≥ 4.9).

## Building

```bash
bash scripts/build-analyzers.sh   # tests + Release build + DLL sync
```

Self-wraps in `nix-shell -p dotnet-sdk_10` when dotnet is absent. The SDK is
pinned exactly in `Analyzers/global.json` — the build is deterministic
(`ContinuousIntegrationBuild=true`), and CI's "Fail on DLL drift" step
byte-compares its own rebuild against the committed DLL, so a stale or
out-of-band DLL cannot merge. The DLL is LFS-tracked; commit it with the
source change (CI tells you when you forget).

Reproducibility notes, each learned from a real drift-gate failure:
`Analyzers/**` is pinned to LF in `.gitattributes` (the deterministic MVID
hashes source bytes, so a CRLF checkout builds different bytes);
`build-analyzers.sh` wipes `bin`/`obj` first (stale incremental state changed
the output); and `IncludeSourceRevisionInInformationalVersion` is off in the
csproj (the SDK's implicit SourceLink otherwise embeds the git HEAD sha, so a
committed DLL could never match a rebuild at any other commit). If the gate
still fails unexpectedly, the job uploads its own build as the
`DCL.Analyzers.dll-canonical` artifact — download, copy over
`Explorer/Assets/DCL/DCL.Analyzers.dll`, commit.

## Adding a rule

New `DiagnosticAnalyzer` (next `DCLA00N`) + tests stubbing external types by
metadata name (see any existing test file), `bash scripts/build-analyzers.sh`,
a row here, a severity pin in `Explorer/.editorconfig`.
