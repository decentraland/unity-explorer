# Architecture Overview

The common ECS concepts are very well described in the [Unity Entities documentation](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/concepts-intro.html). Here, the description of the concepts tailored to the project's needs is given.

## Entities
An `entity` represents an instance of something discrete:
- everything in ECS is represented by `entities`: there are no other means to express one's intentions
- `Entity` is a compound of one or more components: `entity` can't exist without components
- `Entity` is just an `integer` representation
- `Entity` exists only in the context of the `world` it was created in; in order to save an `entity` for "later" `EntityReference` should be used.
- `Entity` does not contain a reference to the `world` it was created in

### Single-instance entities
Design-wise there could be entities that should exist in a single copy in the World.
Unlike real singletons, each entity belongs to the World so it can't be static or accessed in a static manner.

The main benefit of using a single-instance entity is avoiding querying each frame since you can poll the entity directly and ensuring it exists in a single instance (instead of tacitly assuming a query is executed only once)

You may consider the following approaches to single-instance entities:
- Use `SingleInstanceEntity` structure: it is cached (by a query) once and then stored/cached in the system. Then the Component is accessed via Extensions
- Introduce your own structure that encapsulates the `World` and the `Entity`, introduce methods to get [by ref] desired components (e.g. `PlayerInteractionEntity`

### No Goes
- It's a bad practice to store `Entities` outside of ECS

## Components
`Components` contain data that systems can read or write.

In `ECS` components may contain data only and must not contain any logic executed on components. For simplification components apart from the data itself may execute the following responsibilities:
- Encapsulate a pool solely dedicated to this component type or its fields.
- Provide a static factory method
- Provide a non-empty constructor

In `Arch` both classes (reference types) and structures (value types) can be added as components.

In order to reduce memory footprint and simplify lifecycle management it's **preferred to use structures wherever possible**. However, there are several cases when the usage of classes is favorable:
- Adding `MonoBehaviors` directly: we follow a hybrid `ECS` approach so it is allowed to store Unity objects in `ECS` and access them from systems
- Adding existing classes whose lifecycle is handled outside of ECS as components, e.g. `ISceneFacade` and `Protobuf` instances
- Referencing a component that can be changed outside of the current `World`

Refer to [Design guidelines](development-guide.md#design-components) for development practices.

## Systems
A system provides the logic that transforms [component](#components) data from its current state to its next state. For example, a system might update the positions of all moving entities by their velocity multiplied by the time interval since the previous update.

A system runs **on the main thread once per frame**. Systems are organized into a hierarchy of system groups that you can use to organize the order that systems should update in.

Systems in their group of responsibility (feature) rely on data produced in a certain order: by design, systems should have dependencies on each other in the form of `components` (data) produced and modified by them. This is the only allowed and needed type of communication between different systems.

Refer to [Design guidelines](development-guide.md#design-systems) for development practices.

### Creation and Update Order
We are using a [separate library](https://github.com/mikhail-dcl/Arch.SystemGroups) that automates systems creation and their dependencies resolution.

## Worlds
A **world** is a collection of systems and entities. All worlds:
- are fully independent of each other, can't reference entities from other worlds
- can be disposed in separation
- may have a unique set of systems

Currently, we have the following worlds:
- The global world. Exists in a single entity, instantiated in the very beginning:
   - Handles realm and scenes lifecycle
   - Handles Player and Camera entities
   - Handles Avatars received from comms
- Scene world. Each JavaScript scene is reflected onto an individual ECS World:
   - Peer communication and dependencies between worlds are strictly forbidden

## ECS and OOP
`ECS` is an architectural pattern that does not benefit much from the traditional OOP principles.

### Abstraction
You can't operate with systems and components in an abstract way: neither instantiate systems and then reference them anyhow nor query components by a base type or an interface.

The following use cases can be considered for interfaces:
- generic constraints

### Polymorphism and Inheritance
It's not prohibited for systems to have a common behavior in a base class

### Generics
Both components and systems can be generic. There are no limitations. Though you can't specify a dependency on an open generic type.

### Events/Callbacks
The callbacks mechanism is in contradiction with `ECS`: you should never create, subscribe to or store any `delegate`-like data.

It is also forbidden to propagate data from Unity objects to `Systems` via `events`.

The main reason for that is the lifecycle of the subscriptions: they can be invoked at any moment, while `Systems` should execute logic in their `Update` function only.

## ECS and asynchronous programming

`async/await` is a nice pattern, however, it does not fit `ECS` at all: still, it is possible to marry it with `systems`, though it's very hard to maintain. There is only one implementation of `gluing` two worlds together: `LoadSystemBase<TAsset, TIntention>`.

We should be very cautious in trying to implement more of them as the logic to keep everything intact is perplexed and hardly approachable.

In order to benefit from an asynchronous resolution there is a concept of `Promises` ([Asset Promises page](asset-promises.md)):
- It is represented by `AssetPromise<TAsset, TLoadingIntention>`
- Each `Promise` is an entity
- `Promise` is polled by a system each frame to check if it is resolved
- Once `Promise` is consumed by the system that originated it, the `entity` is destroyed

> For documentation on the third-party libraries used (ArchECS, ClearScript, Sentry), see [Third-Party Libraries](third-party-libraries.md).

# Dependencies management

## Plugins System
Each `IDCLPlugin` encapsulates its own unique dependencies **that are not shared with other plugins** (if you need a shared dependency you should introduce it in a [container](#containers)). The responsibility of the plugin are:
- instantiate any number of dependencies needed in a given scope
- instantiate any number of `ECS` systems based on **shared dependencies**, **settings** provided by `Addressables`, and scoped **dependencies**:
  - Inject into the real world scene
  - Inject a subset of systems (as needed) into an empty scene (e.g. Textures Loading is not needed for Empty Scenes but Transform System are)


Plugins are *produced* within [**Containers**](#containers) and *initialized* from `DynamicSceneLoader` or `StaticSceneLauncher`.

### Plugin Settings
Each plugin may have as many settings as needed, these settings are not shared between plugins and exist in the corresponding scope.

`IDCLPluginSettings` is a contract that should be implemented by all types of setting, every type should be annotated with `Serializable` attribute. Each type of `IDCLPluginSettings` represents a set of dependencies that belong exclusively to the context of the plugin. They may include:
- Pure configuration values such as `int`, `float`, `string`, etc. All fields/properties should be serializable by Unity.
- Addressable references to assets: [`AssetReferenceT<T>`](https://docs.unity3d.com/Packages/com.unity.addressables@1.21/manual/AssetReferences.html). This way main assets are referenced: `Scriptable Objects`, `Material`, `Texture2D`, etc.
- Components referenced on prefabs: `ComponentReference<TComponent>`. This way prefabs are referenced.

`IAssetsProvisioner` is responsible to create an acquired instance (`ProvidedAsset<T>` or `ProvidedInstance<T>`) from the reference. They provide a capability of being disposed of [so the underlying reference counting mechanism](https://docs.unity3d.com/Packages/com.unity.addressables@1.21/manual/MemoryManagement.html) is properly triggered.

It's strictly discouraged to reference assets directly (and, thus, create a strong reference): the idea is to keep the system fully dependent on `Addressables`, disconnect from the source assets come from, and prevent a widely known issue of asset duplication in memory.

> There is no assumption made about where dependencies may come from: in the future we may consider distributing different versions of bundles from a remote server, thus, disconnecting binary distribution from upgradable/adjustable data.

All `IDCLPluginSettings` are stored in a single `Scriptable Object` `PluginSettingsContainer`, this capability is provided by reference serialization: `[SerializeReference] internal List<IDCLPluginSettings> settings;`. The capability of adding them is provided by a custom editor script. Implementations must be `[Serializable]` so Unity is capable of storing them.

If no settings are required `NoExposedPluginSettings` can be used to prevent contamination with empty classes.

**There are two scopes with plugins:**
- **Global**: corresponds to anything that exists in a single instance in a global world. E.g. camera, characters, comms, etc. Global plugins are not created for static scenes and testing purposes.

  ![Global plugins settings](https://github.com/decentraland/unity-explorer/assets/118179774/4333c6f8-89e9-4376-9ae7-6faf8ab6e818)

  `DCLGlobalPluginBase<TSettings>` provides a common initialization scheme for plugins with dependency on the global `World` and/or `Player Entity` (or any other entities that can exist in the world):
     - In `InitializeAsyncInternal` assets can be loaded asynchronously, and all the dependencies that do not rely on the world can be set up immediately
     - `InitializeAsyncInternal` returns a special continuation delegate that will be executed when the global world is injected. It's assumed that it's possible to make all the required closures to shorten the code as much as possible and avoid introducing boilerplate fields explicitly.
     - it allows avoiding extra methods in `Controllers` to inject `World` and `Entity` which break `constructor` conceptually

     ```csharp
     protected override async UniTask<ContinueInitialization?> InitializeAsyncInternal(MinimapSettings settings, CancellationToken ct)
        {
            MinimapView? prefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.MinimapPrefab, ct: ct)).Value.GetComponent<MinimapView>();

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> world, in GlobalPluginArguments _) =>
            {
                mvcManager.RegisterController(new MinimapController(MinimapController.CreateLazily(prefab, null),
                    mapRendererContainer.MapRenderer, mvcManager, placesAPIService, TrackPlayerPositionSystem.InjectToWorld(ref world)));
            };
        }
      ```

- **World**: corresponds to everything else that exists per *scene* basis

  ![World plugins settings](https://github.com/decentraland/unity-explorer/assets/118179774/5004501a-3c77-49f8-9e2a-ca2ea2426000)

> You may have one `Global` and one `World` plugin which correspond to a single feature (logical) scope if such necessity arises. E.g. `Interaction Components` exist in two plugins as some systems should run in the Global world while others in the Scene World.

> It's mandatory for each plugin to initialize successfully; it's considered that no plugins are optional; thus, upon failure the client won't be launched and the related error will be logged.

## Containers
**Containers** are final classes that produce dependencies in a given context:
- Created in a `static` manner
- `StaticContainer` is the first class to create:
   - It produces common dependencies needed for other **containers** and [**plugins**](#plugins-system)
   - It produces **world** plugins.
- `DynamicWorldContainer`
   - is dependent on `StaticContainer`
   - produces **global** plugins
   - creates `RealmController` and `GlobalWorldFactory`
- It's highly encouraged to break `Static` and `DynamicWorld` containers into smaller encapsulated pieces as the number of dependencies and contexts grows to prevent creating a god-class from the container.
   - e.g. `ComponentsContainer` and `DiagnosticsContainer` are created in the `Static` container and responsible for building up the logic of utilizing `Protobuf` and other components such as pooling, serialization/deserialization, and special disposal behavior:
      - Each substituent is responsible for creating and holding its own context so it naturally follows the Single Responsibility principle
   - it's possible to introduce as many containers as needed and reference them from `Static` or `DynamicWorldContainer`. All of them should live in the same assemblies: don't introduce an assembly per container, refer to [Assemblies Structure](directories-and-assemblies-structure.md) for more details.

## Singletons

Despite the `Singleton` pattern generally being considered an anti-pattern, it can still be applied with certain restrictions and under certain circumstances to simplify dependency management:

- The class should be non-abstract: the singleton itself should not represent an abstraction such as an interface or a base class. The singleton can participate in the inheritance chain, but only the final type can be a singleton. Thus, we guarantee a singleton is not a replacement for a proper dependency management;
- The class shouldn't depend on other abstractions;
- The class can depend on other singletons
- The class can rely on the existing static APIs (e.g. Unity API);
- The class should represent a piece of functionality that exists solely in a single instance, and is globally accessible by nature:
   - For example, if the class contains logic related to the specific scene/world, it can't be a singleton
   - On the contrary, if the class represents logic related to `Input` or the Global World it can be a singleton
- The class must be used statically, its instance should not be passed around: thus, the developer guarantees that they understand it's **publicly** accessible;
- Using a singleton should be **justified**: it should lead to the **significant simplification of dependency management**;
- The class should respect one of the following life cycles:
   - Is always available: constructed via implicit instantiation or manually during containers' initialization;
   - Constructed for the user session: in this case, the singleton must be `Reset` on logout/re-login, and strictly can't be accessible before the session is initialized;
- The singleton must be reset between Unit Tests to prevent state leakage.

To facilitate the usage of singletons and following the requirements the [CodeLess](https://github.com/mikhail-dcl/CodeLess) project was created.

## Abstractions

### When abstractions are not needed

First, always follow the YAGNI principle when developing a certain feature: don't introduce unneeded abstractions/interfaces if the application is not justified by design requirements and can't be easily foreseen for the nearest future.

Several tips can help with a proper decision:
- If a class has exactly one reasonable implementation and you don't foresee needing a variant, skip the interface;
- If the interface you are going to introduce is fully symmetric;
- No Testing/Mocking expected for the given scope
- When the concrete class is trivial to instantiate or wrap without mocking, there's no mocking gain from an abstraction
- View layer is usually not testable and doesn't require any abstractions
- Simple Data Structures: Plain Objects that only carry data and have no behavior don't need interfaces—they don't encapsulate logic worth abstracting.
- Performance-Critical Paths: Virtual dispatch or interface calls incur a tiny cost. In hot loops or latency-sensitive code, concrete calls are marginally faster—avoid abstraction here.
- Utility classes and extensions for which dependencies can be passed as arguments, in case the number of arguments is reasonable. And the class itself can be simply static.

### Auto-generation of interfaces
If some aspects of the aforementioned are not true and you still need an abstraction, consider using the [CodeLess](https://github.com/mikhail-dcl/CodeLess) project to generate interfaces for you, reducing the amount of boilerplate code:

- The interface is fully symmetric to the class implementation: the majority of open and internal members should be exposed via an interface. The minority can be ignored by `IgnoreAutoInterfaceMember` attribute;
- The interface is independent: there is no necessity to inherit from other interfaces, and segregate into smaller ones
- The class, an interface will be generated from, implements only that interface, `IDisposable` can be an exception
- The generated interface must be obvious to a developer (as it's not part of the source code base)

## Contextual Asset Loading
### Overview
The ContextualAsset system is a set of utility types designed to load and unload assets only when needed, freeing memory when the asset is not actively used. This approach greatly reduces RAM consumption, particularly for UI-heavy scenarios where certain resources are only relevant in specific contexts.

It consists of:

* ContextualAsset<T> – Generic wrapper for deferred, context-based asset loading/unloading.
* LocalizedContextualAsset<T> – Specialization for localized content (e.g., different languages or regions).
* ContextualImage – UI component specifically for on-demand image loading. It wraps over ContextualAsset<T>

### Benefits
1. Reduced RAM Usage
Assets are only loaded when the relevant context is active (e.g., a specific screen, mode, or scene). Once the context changes, the assets can be released, preventing unnecessary memory retention. Example: Instead of keeping all loading screen images in memory for the entire session, load only the one needed for the current screen, and unload it after use.

2. Performance-Friendly
By controlling load/unload points, you can avoid:
* Large memory spikes from many assets being loaded at once.
* Performance drops caused by excessive simultaneous asset streaming.

3. Context Management
The contextual system ties asset lifetime to a scope where it should be used:
* When entering a scope: load an asset asynchronously.
* When leaving a scope: release the asset.
This makes it easy to add memory-efficient asset usage with minimal logic.

4. Integration With UI
ContextualImage integrates directly with Unity's UI system, automatically replacing Image.sprite only when needed, and freeing it when not.

### Example Use Cases
UI Screens with Rarely Used Assets:
* Preview Image in auth screen.
* Loading Screen Tips (text + image pairs).
* ScreenShot Camera Grid overlay.

### Implementation Notes
* Works with Addressables.
* Load is asynchronous by default.
* Unload is deterministic when the context ends. But be careful: Addressables won't release asset until next Unity's cleanup cycle. If you need to release it manually use `Resources.UnloadUnusedAssets`

### Performance Impact
Based on internal testing, this system saves significant RAM by unloading unneeded assets between screens. Measurements are attached in this PR https://github.com/decentraland/unity-explorer/pull/5058

## Union types with REnum

### What Are Union Types?

A union type (also known as a sum type or tagged union) allows a variable to hold data of one of several distinct types, each with its own associated values and structure. At runtime, the type of the value is tracked along with the value itself.

This is especially useful when modeling data that can take on several shapes — such as results that are either success or error, or input events from APIs.

### Union Types in Other Languages

Many modern languages have first-class support for union types:

Rust: Uses enum to define tagged unions. Each variant can have its own data.
```rust
enum Address {
    House { street: String, number: u32 },
    Apartment { street: String, unit: u8 },
}
```

TypeScript: Supports union types directly via the | operator.
```typescript
type Address = HouseAddress | ApartmentAddress;
```

Swift: Uses enum with associated values.
```swift
enum Address {
    case house(street: String, number: Int)
    case apartment(street: String, unit: Int)
}
```

These languages use union types to simplify logic, reduce errors, and enable exhaustive pattern matching.

### Union types in C#

C# lacks native support for tagged unions. Instead, developers often use:
* Class hierarchies and polymorphism — verbose and GC-heavy
* Libraries like OneOf — easier, but still relies on runtime boxing
* Manual switch statements on enums — error-prone and not type-safe

### REnum

REnum is a Roslyn Source Generator that brings Rust-style enums to C#, enabling:
1. Zero-cost union types using value types (structs)
2. Pattern matching using .Match()
3. Exhaustive and safe access to union cases using .IsXXX(out T)

It generates all required code at compile time, ensuring no runtime overhead, no boxing, and no GC pressure, which is ideal for performance-sensitive apps like games.

#### Example usage

Type declaration:

```csharp
[REnum]
[REnumField(typeof(HouseAddress))]
[REnumField(typeof(ApartmentAddress))]
public partial struct Address {}
```

Pattern matching:

```csharp
string result = address.Match(
    "Address: ",
    static (prefix, house) => $"{prefix}{house.Street}",
    static (prefix, apt) => $"{prefix}{apt.Street}"
);
```

Safe access:

```csharp
if (address.IsHouseAddress(out var house)) {
    // use house variable here
}
```

To see more examples visit https://github.com/NickKhalow/REnum

## Exceptions-free `async` flow

In some sensitive flows, especially with deep hierarchy, you may want to avoid handling exceptions and rely on the `Result` reported from the functions being called.

```csharp
    public readonly struct Result
    {
        public readonly bool Success;
        public readonly string? ErrorMessage;

        private Result(bool success, string? errorMessage)
        {
            this.Success = success;
            this.ErrorMessage = errorMessage;
        }

        public static Result SuccessResult() =>
            new (true, null);

        public static Result ErrorResult(string errorMessage) =>
            new (false, errorMessage);

        public static Result CancelledResult() =>
            new (false, nameof(OperationCanceledException));
    }
```

Several guidelines should be respected if a method follows the given principle with a return value like `UniTask<Result>`:

- The method should guarantee it does not throw any exceptions as a managed way of handling the flow
- The method itself can call other APIs that can throw exceptions (as generally it's impossible to compile them out in C# and avoid, especially considering how Unity APIs and `UniTask` themselves are designed)

E.g. it can be achieved like this:
```csharp
        public UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct) =>
            InternalExecuteAsync(teleportParams, ct).SuppressToResultAsync(ReportCategory.SCENE_LOADING, createError);

        /// <summary>
        ///     This function is free to throw exceptions
        /// </summary>
        protected abstract UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct);

...

        public static async UniTask<Result> SuppressToResultAsync(this UniTask coreOp, ReportData? reportData = null, Func<Exception, Result>? exceptionToResult = null)
        {
            try
            {
                await coreOp;
                return Result.SuccessResult();
            }
            catch (OperationCanceledException) { return Result.CancelledResult(); }
            catch (Exception e)
            {
                ReportException(e);
                return exceptionToResult?.Invoke(e) ?? Result.ErrorResult(e.Message);
            }

            void ReportException(Exception e)
            {
                if (reportData != null)
                    ReportHub.LogException(e, reportData.Value);
            }
        }

```
- Watch out for cancellation token handling: the method should handle it gracefully without calling `ThrowIfCancellationRequested();`.

```csharp
                if (ct.IsCancellationRequested)
                    return Result.CancelledResult();
```

- When an exception-free method is called, it's expected that it won't throw any exceptions. The method should only handle its own exceptions to comply with the `Result` signature
- Try to propagate the exceptions-free flow to the upper-layer methods while it's reasonable, don't mix/alternate the hierarchy with both throwing and non-throwing notations
