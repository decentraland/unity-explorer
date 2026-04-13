---
name: plugin-architecture
description: "Plugin and DI architecture. Use when adding or modifying plugins (IDCLWorldPlugin, IDCLGlobalPlugin), system registration, dependency injection, containers, assembly structure, or Addressables provisioning."
user-invocable: false
---

# Plugin & Dependency Architecture

## Sources

- `docs/architecture-overview.md` — Plugin system, containers, dependency management
- `docs/directories-and-assemblies-structure.md` — Folder and assembly organization rules

---

## Plugin Types

### IDCLWorldPlugin — Scene-Scoped

Created per scene world. Receives world-specific dependencies and injects systems into that world.

**Lifecycle:**
1. `InitializeAsync(TSettings, CancellationToken)` — Load addressable assets, create pools
2. `InjectToWorld(ref ArchSystemsWorldBuilder, ...)` — Register systems into the world
3. `Dispose()` — Clean up resources

**Key parameters in `InjectToWorld`:**
- `ECSWorldInstanceSharedDependencies sharedDependencies` — scene state, partition, scene data
- `PersistentEntities persistentEntities` — long-lived entities
- `List<IFinalizeWorldSystem> finalizeWorldSystems` — register cleanup systems here
- `List<ISceneIsCurrentListener> sceneIsCurrentListeners` — register scene-current listeners here

### IDCLGlobalPlugin — Application-Scoped

Created once for the application lifetime. Injects systems into the global world only. Same lifecycle as world plugin but with `GlobalPluginArguments` instead of world-specific dependencies.

---

## Plugin Settings

- Settings classes implement `IDCLPluginSettings` and are `[Serializable]`
- Asset references use `AssetReferenceT<T>` or `AssetReferenceGameObject` (Addressables)
- Settings are stored in `PluginSettingsContainer` ScriptableObject
- `IAssetsProvisioner` loads addressable assets; returns `ProvidedAsset<T>` or `ProvidedInstance<T>` for disposal tracking

---

## Container Architecture

### StaticContainer

Created first. Produces common dependencies and world plugins.
- Creates `IComponentPoolsRegistry`, `CacheCleaner`, `IAssetsProvisioner`
- Instantiates world plugins (`IDCLWorldPlugin`) with their dependencies
- Provides `StaticSettings` (all plugin settings)

### DynamicWorldContainer

Created after StaticContainer. Holds global plugins and runtime state.
- Instantiates global plugins (`IDCLGlobalPlugin`)
- Creates `RealmController`, `GlobalWorldFactory`
- Manages scene lifecycle

### ComponentsContainer

Registers SDK component types for CRDT deserialization. Each SDK component must be registered here.

---

## Assembly Structure

### Core principles

- **Strive for large assemblies instead of splitting into small ones.** Avoid creating new `.asmdef` or `.asmref` files unless necessary — the folder or a parent folder may already be covered by an existing assembly definition.
  - **Why:** Fewer assemblies reduces compilation time and simplifies dependency resolution. Each new `.asmdef` adds build overhead and creates another node in the dependency graph.
- When a new assembly reference **is** needed, prefer `.asmref` to fold code into an existing large assembly rather than introducing a new `.asmdef`.
- Only create a dedicated `.asmdef` when the feature **must be referenced by other assemblies** or requires strict dependency isolation.
- Control exposure via `public` / `internal` access levels — this is the primary encapsulation tool, not assembly boundaries.
- `DCL.Plugins` is the only "global" assembly: it can reference any assembly but should not be referenced itself (except by tests).
  - **Why:** This prevents circular references and keeps plugin isolation intact — plugins produce systems without knowledge about unrelated assemblies.

### Assembly rules for ECS Systems

ECS Systems **must always** use an `.asmref` pointing to `DCL.Plugins`:

```json
{ "reference": "DCL.Plugins" }
```

### Assembly rules for Tests

Tests use an `.asmref` pointing to the shared test assembly:

```json
{ "reference": "DCL.EditMode.Tests" }
```

For PlayMode tests use `DCL.PlayMode.Tests` instead.

### Naming convention for `.asmdef` and `.asmref`

- File names **must start with `DCL.`** followed by the feature name: `DCL.<Feature>`.
- Use **dot-separated suffixes** when subdividing: `DCL.<Feature>.<SubFeature>`.
- `.asmref` files inside an ECS `Systems/` folder **must use the `.Systems` suffix**: `DCL.<Feature>.Systems.asmref`.
- Test `.asmref` files use corresponding suffixes: `DCL.<Feature>.EditMode.Tests.asmref`.

Examples from the project:

| File | Location |
|------|----------|
| `DCL.Multiplayer.Movement.Systems.asmref` | `Multiplayer/Movement/Systems/` |
| `DCL.Landscape.Systems.asmref` | `Landscape/Systems/` |
| `DCL.Input.Systems.asmref` | `Input/Systems/` |

### Feature folder structure

```
Assets/DCL/<Feature>/
├── Components/
├── Systems/
│   ├── DCL.<Feature>.Systems.asmref  ← { "reference": "DCL.Plugins" }
│   ├── <Feature>Plugin.cs            ← plugin lives WITH the systems it injects
│   ├── <Feature>LifecycleSystem.cs
│   └── <Feature>ApplyPropertiesSystem.cs
├── Tests/
│   └── EditMode/
│       ├── <Feature>SystemShould.cs
│       └── EditMode.asmref               ← { "reference": "DCL.EditMode.Tests" }
```

### Plugin file placement

- `<Feature>Plugin.cs` **must be in the `Systems/` folder** alongside the ECS systems it injects.
- If the plugin has **no ECS systems**, do not create a `Systems/` folder. Place the plugin directly in:
  - `Assets/DCL/PluginSystem/Global/` — for global plugins (`IDCLGlobalPlugin`)
  - `Assets/DCL/PluginSystem/World/` — for world plugins (`IDCLWorldPlugin`)

---

## Detailed Reference

For detailed code examples, see [reference.md](reference.md).

---

## Cross-References

- **cross-world-ecs-access** — Global world injection chain, `InjectToWorld` usage from plugin to system
- **ecs-system-and-component-design** — System design patterns, `BaseUnityLoopSystem`, query best practices
- **sdk-component-implementation** — SDK component plugin pattern, `ComponentsContainer` registration
