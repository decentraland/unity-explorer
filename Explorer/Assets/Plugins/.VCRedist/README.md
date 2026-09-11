# App-local Visual C++ runtime (Windows x64)

`x64/` holds the Microsoft Visual C++ **release** CRT DLLs that
`Explorer/Assets/Editor/VCRedistBuildPostprocessor.cs` copies next to the built
Windows executable, so the player runs without the VC++ Redistributable
installed. Background and refresh procedure:
[`docs/build-and-ci.md`](../../../../docs/build-and-ci.md).

## What shipped

Extracted from Microsoft's official redistributable on 2026-08-26:

- `https://aka.ms/vs/17/release/vc_redist.x64.exe`
- resolves to `VC_redist.x64.exe`, sha256 `cc0ff0eb1dc3f5188ae6300faef32bf5beeba4bdd6e8e445a9184072096b713b`
- CRT toolset **14.44.35211.0**, `_amd64` payload

| File | Bytes |
|---|---|
| `msvcp140.dll` | 557,728 |
| `vcruntime140.dll` | 124,544 |
| `vcruntime140_1.dll` | 49,792 |