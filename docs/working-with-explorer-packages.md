# Working with Explorer Packages

Every Decentraland-authored UPM package the Explorer consumes lives in this repository under
[`Explorer/PackagesLocal/`](../Explorer/PackagesLocal/). Both Unity projects resolve them through `file:` references in
their `Packages/manifest.json`, so a clone of this repository is self-contained: no private package registry, no
Git-over-SSH dependency, and no deploy key is needed to open either project or to run CI.

| Package | Provides |
| --- | --- |
| `com.decentraland.livekit-sdk` | LiveKit client SDK with the Linux FFI loader (Apache-2.0, see its `LICENSE`/`NOTICE`) |
| `decentraland.grassshader` | Stylized grass shader and the grass colour-map baker |
| `decentraland.renderfeatures` | Avatar outline, object highlight, ocean and skybox-to-cubemap URP render features |
| `org.decentraland.unityuniversalinstancer` | GPU-driven indirect instancing runtime behind the `GPUInstancerPro` C# surface |
| `org.decentraland.instancing-assets` | Landscape prototype bindings for the instancer; together they raise `GPUI_PRO_PRESENT` |
| `org.decentraland.unityscrollview` | Virtualized list and grid views behind the `SuperScrollView` surface |
| `org.decentraland.unityfilebrowser` | Native open/save/folder dialogs behind the `Crosstales.FB` surface |

The one package outside that folder is `com.decentraland.unity-shared-dependencies` (avatar/scene shaders and glTFast
wrappers shared with `avatar-preview-renderer`): it lives at the repository root as
[`unity-shared-dependencies/`](../unity-shared-dependencies/) and both projects reference it as
`file:../../unity-shared-dependencies`.

---

## Referencing a package

Paths are relative to the project's `Packages/` folder:

```json
"decentraland.renderfeatures": "file:../PackagesLocal/decentraland.renderfeatures"
```

from `Explorer/Packages/manifest.json`, and

```json
"decentraland.renderfeatures": "file:../../Explorer/PackagesLocal/decentraland.renderfeatures"
```

from `avatar-preview-renderer/Packages/manifest.json`. Unity records a `file:` dependency in `packages-lock.json` with
`"source": "local"` and no hash; commit both files together.

Packages that ship tests are listed under `"testables"` in the Explorer manifest so the Test Runner picks them up.

---

## Changing a package

1. Edit the package in place under `Explorer/PackagesLocal/<package>/`. Unity reloads local packages on focus, so the
   change is live in both projects immediately.
2. Run the package's own tests from the Test Runner (EditMode and, where present, PlayMode).
3. Commit the package change together with the Explorer or `avatar-preview-renderer` change that needs it. There is no
   separate repository to merge first and no commit hash to bump.

---

## Adding a package

1. Create `Explorer/PackagesLocal/<package>/` with a `package.json`, a `Runtime/` assembly definition and, if it has
   tests, a `Tests/` assembly definition.
2. Add the `file:` reference to each project's `manifest.json` that consumes it and let Unity regenerate
   `packages-lock.json`.
3. If the package must gate call sites behind a define, add a `versionDefines` entry on the consuming assembly
   definition instead of a global scripting define, so the code compiles cleanly when the package is absent.

Third-party code is not vendored into `PackagesLocal`. Keep upstream open-source dependencies as Git or registry
references in the manifest with their licence files intact, and keep proprietary Asset Store packages out of the
repository entirely.
