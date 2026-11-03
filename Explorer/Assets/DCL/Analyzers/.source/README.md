# DCL.Analyzers

Source of the Roslyn analyzer DLL Unity loads from `Explorer/Assets/DCL/Analyzers/DCL.Analyzers.dll` (asset label `RoslynAnalyzer`, all platforms disabled). The DLL sits outside any `.asmdef` folder, so Unity applies it to every assembly under `Assets/`; package assemblies under `Packages/` are not analyzed. This folder starts with a dot, so Unity does not import it.

Rules and how to fix them: [`docs/code-style-guidelines.md` § Throwing dictionary APIs](../../../../../docs/code-style-guidelines.md#throwing-dictionary-apis).

## Running it from the shell

```bash
bash scripts/lint/run-dcl-analyzers.sh                         # every project assembly
bash scripts/lint/run-dcl-analyzers.sh DCL.Social DCL.Plugins  # only these assemblies
```

It recompiles each assembly with Unity's bundled compiler and the response file Unity generated for it in `Explorer/Library/Bee`, so the results match an Editor compile, and it never writes to `Library/`. Unity must have compiled the project once, and a `.cs` file added since then is only picked up after Unity recompiles. Exit code 1 means findings; the script header lists the options and exit codes.

## Changing the analyzer

Requires the .NET SDK pinned in `global.json`.

```bash
bash Explorer/Assets/DCL/Analyzers/.source/build.sh        # run the tests, build, install the DLL
bash Explorer/Assets/DCL/Analyzers/.source/build.sh check  # fail if the committed DLL differs from this source
```

The build is deterministic: commit the DLL together with the source change that produced it, and `check` confirms the two match.

- `DCL.Analyzers/` targets `netstandard2.0` and compiles against `Microsoft.CodeAnalysis` 3.8, so the Roslyn shipped with Unity can load it.
- `DCL.Analyzers.Tests/` compiles small C# samples in memory and asserts which lines are reported (`// expect: DCL0001`).
