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

### Dependency flow direction (critical)

Dependencies flow **top-down from the composition root into containers, and then from containers into plugins**. The direction is one-way.

```
Composition root (MainSceneLoader / Bootstrap)
       │ constructs, injects deps into
       ▼
   Containers (StaticContainer, DynamicWorldContainer, feature-scoped containers)
       │ provide deps to
       ▼
   Plugins (IDCLWorldPlugin, IDCLGlobalPlugin)
       │ register systems via
       ▼
   ECS Systems
```

**Rules:**

- **Plugins read from containers. Plugins never construct, initialize, or mutate a container.** If you find yourself writing `container.SomeField = new Thing()` inside `InitializeAsync`, the graph is inverted — stop and restructure.
- Containers are constructed from a single place (the composition root or a parent container). The constructor takes pre-built dependencies; the container exposes them.
- If a plugin needs a dependency that doesn't exist yet, **create a scoped container** for the feature and construct it from the composition root. You can have as many small scoped containers as you want — they are cheap, and they keep the dependency graph honest.
- **Never introduce a new `ObjectProxy`.** The codebase was swept of it; the only legitimate remaining instances model true runtime lifecycles (`MainPlayerAvatarBaseProxy`, `ExposedCameraData.CameraEntityProxy`). For everything else use a decoupling recipe below.

### Decoupling without ObjectProxy

Match the situation to the recipe (full rationale in `docs/architecture-overview.md` § "Deferred dependencies — decoupling without ObjectProxy"):

| Situation | Fix | Existing example |
|---|---|---|
| Service trapped in a late, UI-owning container | Split services into their own container created before any consumer | `FriendsServicesContainer` (services) vs `FriendsContainer` (UI) |
| Dependency exists only when a feature flag is on | Pass `T?` (null = disabled) and null-check where `.Configured` used to be, or use a null-object | `IFriendsService?`; `NullUserBlockingCache`, `NullRoomHub` |
| Scene-world plugin needs comms/multiplayer services | Construct the plugin in `DynamicWorldContainer.WorldPlugins`, not in `StaticContainer` with an empty slot | `AvatarAttachPlugin`, `SceneMaskedEmotePlugin`, `RealmInfoPlugin` |
| Dependency is per-scene data | Add it to `ECSWorldInstanceSharedDependencies`, threaded from `SceneFactory` | `IRoomHub` for the media streaming room |
| Object created in a plugin's async `InitializeAsync` but consumed by earlier objects | Create it eagerly in a container; the plugin only *attaches* the UI-bound parts | `NavmapCommandBus` + `NavmapCommandFactory.AttachUiControllers` |

**Symptoms of inverted flow:**

- Plugin code that assigns to fields on a container
- A plugin constructor that calls `new SomeContainer(...)`
- Chat commands or services that read "the current debug command list" via a static or mutable container field populated from a plugin
- Calls to a container's `Initialize()` from anywhere other than the composition root

### StaticContainer

Created first. Produces common dependencies and world plugins.
- Creates `IComponentPoolsRegistry`, `CacheCleaner`, `IAssetsProvisioner`
- Instantiates most world plugins (`IDCLWorldPlugin`) as `ECSWorldPlugins`
- Provides `StaticSettings` (all plugin settings)

### DynamicWorldContainer

Created after StaticContainer. Holds global plugins and runtime state.
- Instantiates global plugins (`IDCLGlobalPlugin`)
- Instantiates the world plugins whose dependencies (comms, multiplayer) only exist here, exposed as `WorldPlugins`; the bootstrap concatenates them with `StaticContainer.ECSWorldPlugins` for initialization and scene-world creation
- Creates `RealmController`, `GlobalWorldFactory`
- Manages scene lifecycle
- Never writes into `StaticContainer` — if a value created here is needed by something in `StaticContainer`, that something is constructed in the wrong container

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
