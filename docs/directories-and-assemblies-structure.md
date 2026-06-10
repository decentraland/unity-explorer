# Directories & Assemblies Structure

> **Status:** reflects the assembly consolidation completed in June 2026, which reduced the project from 139 `.asmdef` files to 62. The rules below drove that consolidation and remain in force.

## Encapsulated Features

Every feature can contain an arbitrary range of `Assets` which can include but are not limited to:
- Scripts
- Textures
- Models (FBX, OBJ, etc.)
- Audio
- Prefabs
- Scriptable Objects

The default place for a feature is a subdirectory at `Assets/DCL` path.

In order to maintain a reasonable number of assemblies and a manageable number of dependencies between them consider the following rules:
- **Strive for large assemblies instead of splitting into small ones.** Avoid creating new `.asmdef` or `.asmref` files unless necessary â€” the folder or a parent folder may already be covered by an existing assembly definition.
- When a new assembly reference **is** needed, prefer `.asmref` ([Assembly Definition References](https://docs.unity3d.com/Manual/class-AssemblyDefinitionReferenceImporter.html)) to fold code into an existing large assembly rather than introducing a new `.asmdef`.
- Only create a dedicated `.asmdef` when the feature **must be referenced by other assemblies** or requires strict dependency isolation.
- Control exposed members by `public` access level: if members should not be exposed make them `internal` instead. It makes the difference when we pursue the minimum number of assemblies.
- ECS Systems **must always** use an `.asmref` pointing to `DCL.Plugins`.
- Use different directories for **Unit Tests** but connect all of them to the single root test assemblies via `.asmref`: `DCL.EditMode.Tests` for edit-mode tests (the vast majority) and `DCL.PlayMode.Tests` for play-mode tests. We don't care about the number of dependencies in the case of Tests as any other assembly never references Tests. See [Testing Guide](testing-guide.md).
- Editor-only tooling folds into the single `DCL.Editor` assembly via `.asmref` (the only exception is `Utility.Editor`, which must stay separate â€” `DCL.Plugins` references it, and `DCL.Editor` references `DCL.Plugins`).

## Pure technical implementations

Some features may include code only being purely technical ones. Among the currently implemented ones are:
- CRDT Protocol and Bridge
- Diagnostics Utilities
- General Utilities
- Scene Runtime
- SDK Components handling that results in code only without settings. However, it's still advised to migrate them to a separate fully-fledged feature to be more flexible and maintain a unified structure.
- Prioritization, Realm and Scenes lifecycle

In this case, the feature can be created inside the `Assets/DCL/Infrastructure` folder.
It's a less preferable way of structuring things: in the future, we will be taking more subjects out of there and placing them as an [encapsulated feature](#encapsulated-features).

Regarding distribution between assemblies, the same rules are applied:
- Merging several folders together can be even more aggressive.
- All Components related Tests should be connected to `DCL.EditMode.Tests` like any other unit tests.

## Container, Plugins, and Global code

All containers and plugins belong to a "global" visibility level:
- "DCL.Plugins" is the only "global" assembly that can contain any number of references but should not be referenced itself (apart from Tests). Other "global" directories are connected to it by "Assembly Reference".
- Their Tests are still connected to the `DCL.EditMode.Tests` / `DCL.PlayMode.Tests` assemblies.
- Plugins can reference any types from any assemblies to execute logic on them but they should produce systems and dependencies without knowledge about unrelated assemblies. Thus, we maintain a limited number of references across features.

### Plugin file placement

- `<Feature>Plugin.cs` **must be placed in the `Systems/` folder** alongside the ECS systems it injects.
- If the plugin has **no ECS systems**, do not create a `Systems/` folder. Place the plugin directly in:
  - `Assets/DCL/PluginSystem/Global/` â€” for global plugins (`IDCLGlobalPlugin`)
  - `Assets/DCL/PluginSystem/World/` â€” for world plugins (`IDCLWorldPlugin`)

## Current assembly layout

There are **62 `.asmdef` files** under `Explorer/Assets`: 52 first-party runtime, 3 test, 2 editor-only, 2 native plugin wrappers, 2 vendored, 1 generated. Everything else compiles into one of them via `.asmref` (325 of them at the time of writing). Assemblies coming from UPM packages (e.g. `LiveKit`, `RichTypes`, `REnum`, `Runtime.Wearables`, `DCL.RPC`, `Decentraland.ClearScript`, `UniTask`) live in `Explorer/Packages` / git packages and are not counted here.

Paths below are relative to `Explorer/Assets`. "Folds" lists notable folders connected to the assembly via `.asmref` â€” the folder names on disk did **not** change during consolidation, so a folder name frequently differs from the assembly it compiles into.

### Foundation leaves

Lean, high fan-in assemblies. They are **intentionally kept small** and must never reference feature assemblies â€” merging them upward creates cycles for their many consumers.

| Assembly | Path | Purpose / notable folds |
|---|---|---|
| `Utility` | `DCL/Infrastructure/Utility` | Core helpers, math, pools, threading. Folds `PerformanceAndDiagnostics` (Diagnostics/ReportHub, Optimization, Profiling), `Platforms`, networking abstractions. |
| `DCL.Prefs` | `DCL/Prefs` | Player preferences abstraction. Stays separate: `Utility` references it. |
| `DCL.Network` | `DCL/NetworkDefinitions` | HTTP/networking stack. Folds `WebRequests`, `FeatureFlags`, `CommunicationData`, `Multiplayer/Connectivity`. |
| `Web3` | `DCL/Web3` | Wallet identities and auth-chain primitives. Folds `Plugins/RustEthereum/SignServerWrap`. |
| `DCL.Platform` | `DCL/Infrastructure/Global/AppArgs` | App/platform identity. Folds `Global/AppArgs`, `Global/Versioning`, `Clipboard`, `Time`, `ScriptableObjectsConfigurations`. |
| `DCL.Input` | `DCL/Input` | Input abstraction over Unity InputSystem (systems fold to `DCL.Plugins`). |
| `ECS` | `DCL/Infrastructure/ECS` | Core ECS plumbing: groups, lifecycle, throttling abstractions. |
| `CRDT` | `DCL/Infrastructure/CRDT` | CRDT protocol implementation. |
| `Realm` | `DCL/Infrastructure/ECS/SceneLifeCycle/Realm` | Realm domain data leaf. |
| `SceneRunner.Scene` | `DCL/Infrastructure/SceneRunner/Scene` | Scene data/facade abstractions consumed by everything scene-aware. |
| `Character` | `DCL/Character/CharacterObject` | Character object leaf. |
| `DCL.CharacterMotion.Components` | `DCL/Character/CharacterMotion/Components` | Character motion data components leaf. |
| `ScenePermissions` | `DCL/Infrastructure/ScenePermissions` | Scene permission model. |
| `AssetsProvision` | `DCL/AssetsProvision` | Addressables provisioning helpers. |
| `DebugUtilities` | `DCL/PerformanceAndDiagnostics/DebugUtilities` | Debug widget/binding primitives (see [Debug Container](debug-container-and-widgets.md)). |
| `Quality.RenderFeatures` | `DCL/Quality/RenderFeatures` | URP render features used by quality settings. |
| `DCL.UIToolkit` | `DCL/UIToolkit/Elements` | UIToolkit custom elements (zero references). |

### Infrastructure (scene & ECS stack)

| Assembly | Path | Purpose / notable folds |
|---|---|---|
| `ECS.Unity` | `DCL/Infrastructure/ECS/Unity` | The big Unity-facing ECS assembly. Folds `ECS/StreamableLoading`, `ECS/Prioritization`, most of `SDKComponents` (incl. `MediaStream`, `NFTShape`, `Billboard`, `SceneUI`, `TextShape`), `Audio`, `Character/CharacterCamera`, `SDKEntityTriggerArea`, `PerformanceAndDiagnostics/Gizmos`. |
| `CRDT.ECS.Bridge` | `DCL/Infrastructure/CrdtEcsBridge` | CRDT â‡„ ECS bridge. Folds `WorldSynchronizer`, SDK observable events. |
| `SceneRuntime` | `DCL/Infrastructure/SceneRuntime` | V8/ClearScript JS runtime and JS API modules. Folds `CrdtEcsBridge/JsModulesImplementation`, `SceneRunner/Debugging`, `SceneRunner/Mapping`. |
| `SceneLifeCycle` | `DCL/Infrastructure/ECS/SceneLifeCycle` | Scene loading lifecycle and definition handling (systems fold to `DCL.Plugins`). |
| `DCL.GlobalWorld` | `DCL/Infrastructure/Global/Dynamic/GlobalWorld` | Global world factory/accessor. Folds `Global/Dynamic/PortableExperiences`. |
| `MVC` | `DCL/Infrastructure/MVC` | MVC framework core (see [MVC](mvc.md)); `MVC/ViewDependencies` folds into `DCL.SharedAPI`, `MVC/MVCFacade` into `DCL.Plugins`. |
| `Global.Dynamic.DebugSettings` | `DCL/Infrastructure/Global/Dynamic/DebugSettings` | Debug bootstrap settings; stays separate because production code (`DCL.UI.Flows`) consumes it. |

### Features

| Assembly | Path | Purpose / notable folds |
|---|---|---|
| `DCL.AvatarRendering` | `DCL/AvatarRendering/AvatarShape` | Avatar rendering anchor. Folds `Wearables`, `Emotes`, `Loading`, `Thumbnails`, `NameTags`, `SpringBones`. |
| `DCL.Character` | `DCL/Character/CharacterMotion` | Character motion/camera logic anchor. Folds `CharacterPreview`. |
| `DCL.Profiles` | `DCL/Profiles` | Profile fetching/caching (`Helpers` and `SharedAPI` subfolders fold into `DCL.SharedAPI`). |
| `DCL.Multiplayer` | `DCL/Multiplayer` | Multiplayer anchor: movement, profiles, SDK sync. Folds `Connections` (LiveKit rooms, Archipelago). |
| `DCL.SharedAPI` | `DCL/SharedAPI` | Cross-feature contracts and event buses. Folds 16 small API/bus folders: `SharedAPI/Events`, `NotificationsBus`, `MVC/ViewDependencies`, `Friends/UserBlocking`, `Navmap/NavmapBus`, `Passport/Bridge`, `SceneRestrictionBusController`, `TeleportPrompt/TeleportBus`, `UserInAppInitializationFlow/PublicAPI`, etc. |
| `DCL.ApiServices` | `DCL/Lambdas` | Lambdas client and REST API services. Folds `PlacesAPIService`, `EventsApi`, `BadgesAPIService`, `NftInfoAPIService`. |
| `DCL.Social` | `DCL/Social` | The big social/UI feature bucket. Folds 20 feature folders: `Chat`, `Friends`, `Communities`, `Passport`, `Navmap`, `Places`, `Events`, `ExplorePanel`, `EmotesWheel`, `EmojiPanel`, `VoiceChat`, `Translation`, `Donations`, `InWorldCamera/CameraReelGallery`, `InWorldCamera/PhotoDetail`, `UI/GenericContextMenu/Controllers`, and more. |
| `DCL.Chat.History` | `DCL/Chat/History` | Chat history storage leaf. Stays separate: folding it into `DCL.Social` cycles via `DCL.RealmNavigation`. |
| `DCL.InWorldCamera` | `DCL/InWorldCamera/InWorldCamera` | In-world (screencapture) camera anchor. Folds `CameraReelStorageService`, `ReelActions` (gallery/detail UI folds into `DCL.Social`). |
| `Backpack` | `DCL/Backpack` | Backpack feature (gifting notifications fold into `DCL.Social`). |
| `DCL.SmartWearables` | `DCL/SmartWearables` | Smart wearables runtime. Stays separate from `DCL.AvatarRendering` due to structural `SceneLifeCycle` coupling. |
| `DCL.LOD` | `DCL/LOD` | Scene LODs. Folds `ResourcesUnloading`, `Roads`. |
| `DCL.Landscape` | `DCL/Landscape` | Terrain/landscape generation. Folds `Rendering/GPUInstancing`. |
| `MapRenderer` | `DCL/MapRenderer` | Map rendering. Folds `MapPins`. |
| `DCL.RealmNavigation` | `DCL/RealmNavigation` | Realm/teleport navigation flow. |
| `DCL.SceneLoadingScreens` | `DCL/SceneLoadingScreens` | Loading screens. Stays separate from `DCL.UI.Flows`: cycle via `DCL.RealmNavigation`. |
| `DCL.SkyBox` | `DCL/SkyBox` | Skybox and time-of-day. |
| `Settings` | `DCL/Settings` | Settings feature. Folds `Quality`, `Chat/Settings`, `VoiceChat/Settings`. |
| `DCL.Analytics` | `DCL/PerformanceAndDiagnostics/Analytics` | Analytics core. Folds `Plugins/RustSegment/SegmentServerWrap`. Stays split from its implementation: merging cycles via `DCL.Profiles`. |
| `DCL.Analytics.Implementation` | `DCL/PerformanceAndDiagnostics/Analytics/EventBased` | Event/decorator-based analytics emitters. Folds `Analytics/DecoratorBased`. |
| `CurrentSceneRoomMetadata` | `DCL/CurrentSceneRoomMetadata` | Current scene room metadata. Folds `SceneBannedUsers`. |
| `DCL.Playgrounds` | `DCL/Playgrounds` | Demo/playground scenes and debug scene scripts. |

### UI

| Assembly | Path | Purpose / notable folds |
|---|---|---|
| `UI` | `DCL/UI` | Shared UI widgets and primitives. Folds `UI/ErrorPopup`, `UI/GenericContextMenu`, `UI/InputSuggestions`, `UI/LayoutGroups`. |
| `DCL.UI.Hud` | `DCL/UI/MainUIContainer` | Persistent HUD anchor. Folds `Minimap`, `UI/Sidebar`, `Notifications`, `MarketplaceCredits`, `UI/ConnectionStatusPanel`, `UI/DebugMenu`. |
| `DCL.UI.Prompts` | `DCL/NftPrompt` | Modal prompts anchor. Folds `ChangeRealmPrompt`, `ExternalUrlPrompt`, `TeleportPrompt`. |
| `DCL.UI.Flows` | `DCL/AuthenticationScreenFlow` | Startup/auth flow anchor. Folds `UserInAppInitializationFlow`, `ApplicationsGuards`. |
| `RewardPanel` | `DCL/RewardPanel` | Reward panel. Stays separate from `DCL.UI.Hud`: cycle via `DCL.Social`. |

### Global / composition root

| Assembly | Path | Purpose / notable folds |
|---|---|---|
| `DCL.Plugins` | `DCL/PluginSystem` | The composition root: all plugins, containers, bootstrap, and **every ECS `Systems/` folder in the project** (84 `.asmref`s) â€” avatar, character, SDK components, scene lifecycle, interaction, rendering, diagnostics, etc. References almost everything; referenced only by Tests, `DCL.Editor` and `DCL.Playgrounds`. |

### Tests & Editor

| Assembly | Path | Purpose / notable folds |
|---|---|---|
| `DCL.EditMode.Tests` | `DCL/Tests/Editor` | Single root for all edit-mode tests (~90 folded `Tests/` folders). |
| `DCL.PlayMode.Tests` | `DCL/Tests/PlayMode` | Single root for all play-mode and performance tests (~17 folded folders). |
| `ECS.TestSuite` | `DCL/Infrastructure/ECS/TestSuite` | Test helpers (`UnitySystemTestBase<T>`); referenced by both test roots. |
| `DCL.Editor` | `DCL/Editor` | Single root for editor-only tooling (~17 folded `Editor/` folders). |
| `Utility.Editor` | `DCL/Infrastructure/Utility/Editor` | Editor utilities referenced by `DCL.Plugins` â€” cannot fold into `DCL.Editor` (cycle). |

### Native, vendored & generated

| Assembly | Path | Purpose / notable folds |
|---|---|---|
| `DCL.Native` | `Plugins/NativeWindowManager` | Native interop anchor. Folds `Plugins/NativeAudioAnalysis`, `Plugins/WindowsRegistry`. |
| `DCL.Native.Processes` | `Plugins/DclNativeProcesses` | Native process launching leaf (referenced by `Utility` â€” cannot fold into `DCL.Native`). |
| `DOTween.Modules` | `Plugins/DOTween/Modules` | Vendored DOTween modules. |
| `SocketIOClient` | `Plugins/SocketIO/SocketIOClient` | Vendored Socket.IO client. Folds `SocketIOClient.Newtonsoft.Json`. |
| `Decentraland.Protocol.GeneratedCode` | `Protocol/DecentralandProtocol` | Protobuf-generated protocol code. Folds `Infrastructure/ProtobufPartialClasses`. |

## Adding new code â€” decision guide

Work through these in order; the first match wins:

1. **Default: no new assembly files at all.** Put the code in a folder already covered by an existing assembly (check parent folders for an `.asmdef`/`.asmref`). This is the right answer for the overwhelming majority of changes.
2. **New feature folder?** Add a single `.asmref` at its root pointing to the **nearest domain anchor** from the tables above (e.g. a new social panel â†’ `DCL.Social`; a new SDK component's non-system code â†’ `ECS.Unity`; a new HUD element â†’ `DCL.UI.Hud`).
3. **Special folders always have a fixed target:**
   - `Systems/` (ECS systems + the feature's `Plugin.cs`) â†’ `.asmref` to `DCL.Plugins`
   - `Tests/` â†’ `.asmref` to `DCL.EditMode.Tests` (or `DCL.PlayMode.Tests`)
   - `Editor/` â†’ `.asmref` to `DCL.Editor`
4. **New `.asmdef` only when justified:** the code is a new lean leaf that will be referenced by several other assemblies, or it genuinely requires strict isolation (different define constraints, unsafe code scope, platform restrictions). Expect this to be rare.
5. **Before adding an asmdef reference, check the dependency direction.** Leaves (foundation tables) must never gain references to feature assemblies, and anchors must not reference each other in both directions. The project maintains graph tooling for this: a script that regenerates the resolved dependency graph from all `.asmdef`/`.asmref` files, plus a simulation step that checks a proposed new edge against the transitive closure for cycles **before** the edge is added. Re-run the regeneration after any assembly change and treat any `UNRESOLVED:` entry or new cycle as a blocker. If you find yourself needing `ObjectProxy` or an event bus solely to dodge a reference, the dependency direction is wrong â€” restructure instead.

## Gotcha: namespace shadowing inside `namespace DCL.*`

Project namespaces such as `DCL.Time` and `DCL.Chat` **shadow global type names** for any code inside a `namespace DCL.*` block: C# resolves `Time` to the `DCL.Time` namespace instead of `UnityEngine.Time`, and `Chat` to the `DCL.Chat` namespace instead of the protobuf-generated `Chat` message type. Folding code into larger assemblies surfaces these collisions, because more namespaces become visible to the same compilation.

When you hit one:
- **Fully qualify** the type at the use site: `UnityEngine.Time.deltaTime`, `Decentraland.Kernel.Comms.Rfc4.Chat`.
- Or add a **namespace-scoped using alias** at the top of the file: `using Time = UnityEngine.Time;`.
- Avoid introducing new `DCL.<X>` namespaces where `<X>` collides with a widely used global type (`Time`, `Chat`, `Input`, `Random`, etc.).
