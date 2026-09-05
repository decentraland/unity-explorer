# Scene Runtime

## Abstract

The SceneRuntime is responsible for running scenes that use SDK7.

To allow that, we're using [ClearScript](third-party-libraries.md) which is a V8 Wrapper [(V8 is the most popular JavaScript Engine developed by Google)](https://v8.dev/).

The JavaScript context starts evaluating the code in the [Init.js](https://github.com/decentraland/unity-explorer/blob/main/Explorer/Assets/StreamingAssets/Js/Init.js) to provide the following functionality:
- require
- console (logging)
- fetch (not implemented yet)
- websocket (not implemented yet)

Then it evaluates the SDK7 Source Code from the User.

![scene_runner](https://github.com/decentraland/unity-explorer/assets/12563266/3ebf5d2e-dc2f-45fc-bb3d-d17d5a2fb673)

An SDK7 Scene exposes two methods that must be called from who manages the SceneRunner.
- `onStart()`: called before the first frame.
- `onUpdate(deltaTime)`: called once per frame.

Then the Scene can call `require` to load [modules](#modules).

## Modules

_(aka Kernel API)_

The modules are the exchange of data between the Explorer and the SDK7, to provide the functionality for the SDK7.
Those modules are defined in the [protocol](https://github.com/decentraland/protocol/tree/main/proto/decentraland/kernel/apis).

To load a module, the JavaScript code calls `require(moduleName)`.

_Example: If a content creator wants to teleport the user that is running the scene, they can use the function `TeleportTo` from the `RestrictedActionsService` module. That function is part of the SDK7 and the Explorer must implement it and provide the functionality for it._

### Engine API Implementation

The Engine API is the main module where the [CRDT](#crdt) messages are exchanged between the Explorer and the Scene Runtime to sync the entities and components.

### Going Deep in `require(moduleName)` Function

The SDK7 scenes use the `require` function for loading modules.

![require](https://github.com/decentraland/unity-explorer/assets/12563266/ee301e7e-08a7-4066-a78b-12bae76ee81e)

When an SDK7 scene calls require, the first entry point is [the Init.js](https://github.com/decentraland/unity-explorer/blob/2854a05f9a2b0264c2a8c248e70beff1a008abab/Explorer/Assets/StreamingAssets/Js/Init.js#L1-L22). There we can see that we call [UnityOpsApi.LoadAndEvaluateCode(moduleName)](https://github.com/decentraland/unity-explorer/blob/2854a05f9a2b0264c2a8c248e70beff1a008abab/Explorer/Assets/Scripts/SceneRuntime/Apis/UnityOpsApi.cs#L38-L52) that is calling the C# Implementation. That is evaluating the compiled V8 Code for that module, which is loaded [in the GetJsModuleDictionary](https://github.com/decentraland/unity-explorer/blob/2854a05f9a2b0264c2a8c248e70beff1a008abab/Explorer/Assets/Scripts/SceneRuntime/Factory/SceneRuntimeFactory.cs#L76-L92) and those JavaScript codes can be found [in the streaming assets javascript modules](https://github.com/decentraland/unity-explorer/tree/main/Explorer/Assets/StreamingAssets/Js/Modules).

After the `require`, the scene can call the function for that module. The following diagram explains how it works using the `ReadFile` function from the `Runtime` Module:

![calling-module](https://github.com/decentraland/unity-explorer/assets/12563266/ac63bcd9-a626-44f2-ae56-7b7dda60ae94)

### How to Implement a Module

_(recommended to read [Going Deep in require](#going-deep-in-requiremodulename-function) before)_

To implement a module, you need to:

- Create the interface for it. [(Example)](https://github.com/decentraland/unity-explorer/blob/main/Explorer/Assets/Scripts/SceneRuntime/Apis/Modules/IEngineApi.cs)
- Create the implementation of the Interface. [(Example)](https://github.com/decentraland/unity-explorer/blob/main/Explorer/Assets/Scripts/CrdtEcsBridge/Engine/EngineAPIImplementation.cs)
- Create the wrapper (that is used by JavaScript). That uses the interface mentioned above. [(Example)](https://github.com/decentraland/unity-explorer/blob/main/Explorer/Assets/Scripts/SceneRuntime/Apis/Modules/EngineApiWrapper.cs)
- Create the JavaScript Module. [(Example)](https://github.com/decentraland/unity-explorer/blob/main/Explorer/Assets/StreamingAssets/Js/Modules/EngineApi.js)
- Adding the JavaScript Module to the list of the JS modules. [(here)](https://github.com/decentraland/unity-explorer/blob/2854a05f9a2b0264c2a8c248e70beff1a008abab/Explorer/Assets/Scripts/SceneRuntime/Factory/SceneRuntimeFactory.cs#L76-L92)
- Register the module in the Scene Runtime Implementation. [(Example)](https://github.com/decentraland/unity-explorer/blob/13e6e830ab648e29c64bb97a6302282551fb1236/Explorer/Assets/Scripts/SceneRunner/SceneFactory.cs#L143-L154)

Each module follows a **4-file pattern**: interface, implementation, wrapper, and JS module. The wrapper class extends `JsApiWrapper<TApi>` which holds a reference to the API implementation and a `CancellationTokenSource` used for disposal. It catches exceptions from the implementation and routes them through `ISceneExceptionsHandler`.

Here is a condensed example from `EngineApiWrapper` (`Explorer/Assets/DCL/Infrastructure/SceneRuntime/Apis/Modules/EngineApi/EngineApiWrapper.cs`):

```csharp
public class EngineApiWrapper : JsApiWrapper<IEngineApi>
{
    private readonly IInstancePoolsProvider instancePoolsProvider;
    protected readonly ISceneExceptionsHandler exceptionsHandler;
    private PoolableByteArray lastInput = PoolableByteArray.EMPTY;

    public EngineApiWrapper(
        IEngineApi api,
        ISceneData sceneData,
        IInstancePoolsProvider instancePoolsProvider,
        ISceneExceptionsHandler exceptionsHandler,
        CancellationTokenSource disposeCts)
        : base(api, disposeCts) { /* ... */ }

    [UsedImplicitly]
    public PoolableByteArray CrdtSendToRenderer(ITypedArray<byte> data)
    {
        if (disposeCts.IsCancellationRequested)
            return PoolableByteArray.EMPTY;

        try
        {
            instancePoolsProvider.RenewCrdtRawDataPoolFromScriptArray(data, ref lastInput);
            return api.CrdtSendToRenderer(lastInput.Memory);
        }
        catch (Exception e)
        {
            if (!disposeCts.IsCancellationRequested)
                exceptionsHandler.OnEngineException(e);
            return PoolableByteArray.EMPTY;
        }
    }

    protected override void DisposeInternal()
    {
        lastInput.ReleaseAndDispose();
    }
}
```

Wrappers are registered as host objects in `SceneRuntimeImpl`:

```csharp
// SceneRuntimeImpl.Register<T> adds the wrapper as a ClearScript host object
public void Register<T>(string itemName, T target) where T: JsApiWrapper
{
    jsApiBunch.AddHostObject(itemName, target);
}
```

The full wiring happens in `SceneFactory.CreateSceneAsync`, which calls `sceneRuntime.RegisterAll(...)` to wire all module wrappers. JS modules are compiled in bulk via `SceneModuleHub.LoadAndCompileJsModules`.

## Implementation Flow

<img width="1149" alt="image" src="https://github.com/decentraland/unity-explorer/assets/118179774/94efb171-74fe-453f-9cad-9d4bffc36545">

Scene downloading is initiated from Unity's ECS systems. The downloading itself is performed via the usual `UnityWebRequest`. `ISceneFactory` is responsible for doing this in an `async` manner.
`ISceneFactory` exposes additional overloads to create scenes from files for testing purposes.

Apart from initiating Unity's web requests the scene lifecycle is thread agnostic and, thus, **executes in a separate thread**. It's a vital constituent of the performance the project is able to achieve:
- Each instance of `SceneEngine` is relying on the thread pool
- When the call to `Engine` is `awaited` its continuation is scheduled on the thread pool

> **Warning:** A single scene does not utilize a single thread. Threads will be changed according to the thread pool after each `await`. It means a developer can't make any assumptions about thread consistency.

- API implementations must be **thread-agnostic**
- Resources shared between them must be **thread-safe**

The scene itself is represented by `ISceneFacade`. It has the following capabilities:
- `StartUpdateLoop`
- `SetTargetFPS`: the update frequency of JS Scene is controlled from C#
- `DisposeAsync`

When the scene is created its life cycle is controlled by `ECS`. `ISceneFacade` is added as a component to the entity representing the scene.

The process of scene downloading is described in detail in a [separate section](#scene-downloading).

When the scene code along with the modules is loaded, `SceneRuntimeImpl` is responsible for creating a separate instance of the execution engine via [ClearScript](third-party-libraries.md).

> **Warning:** There is no such concept as engine pooling: every scene creates a unique instance, and when it goes out of scope the instance is disposed of. It creates a considerable GC pressure but `ScriptEngine` is not reusable. `ClearScript` takes care of disposing of unmanaged resources.

Proceed to [Systems](systems.md#scene-lifecycle) to familiarize yourself with the ECS systems that manage the scenes' life cycle.

## Scene Lifecycle States

**File:** `Explorer/Assets/DCL/Infrastructure/SceneRunner/Scene/SceneState.cs`

Each scene's lifecycle is modeled by the `SceneState` enum:

```csharp
public enum SceneState : byte
{
    NotStarted,      // Created but not started
    Running,         // Active update loop
    EngineError,     // Scene communication broken (exception tolerance exceeded)
    EcsError,        // ECS World execution error
    JavaScriptError, // JS code error (tolerance exceeded)
    Disposing,       // Signaled for disposal
    Disposed,        // Fully disposed
}
```

### State Transitions

```
                    +--> EngineError
                    |
NotStarted --> Running --+--> EcsError
                    |
                    +--> JavaScriptError
                    |
                    +--> Disposing --> Disposed
```

- **`NotStarted` -> `Running`**: Triggered by `SceneFacade.StartUpdateLoopAsync`, which calls `SetRunning` after applying any static CRDT messages (`main.crdt`).
- **`Running` -> error states**: `SceneExceptionsHandler` tracks exceptions with a sliding-window counter. When the per-minute tolerance is exceeded (30 for JS errors, 3 for engine errors), the scene is suspended.
- **`Running` -> `Disposing` -> `Disposed`**: `SceneFacade.DisposeAsync` sets `Disposing`, waits for the JS update loop to finish, disposes dependencies, then sets `Disposed`.

### ISceneStateProvider

**File:** `Explorer/Assets/DCL/Infrastructure/SceneRunner/Scene/ISceneStateProvider.cs`

```csharp
public interface ISceneStateProvider
{
    bool IsCurrent { get; set; }
    Atomic<SceneState> State { get; set; }
    uint TickNumber { get; set; }
    ref readonly SceneEngineStartInfo EngineStartInfo { get; }
    void SetRunning(SceneEngineStartInfo startInfo);
}
```

- `IsCurrent` -- whether the player is currently standing in this scene.
- `State` -- wrapped in `Atomic<SceneState>` because state transitions happen from background threads. Always use `State.Set()` / `State.Value()`, never direct assignment.
- `TickNumber` -- incremented after each successful `UpdateScene` call.

### IsNotRunningState Guard

The extension method `IsNotRunningState()` returns `true` for `Disposing`, `Disposed`, `JavaScriptError`, or `EngineError`. It is used as the primary guard to break the update loop in `SceneFacade.StartUpdateLoopAsync`:

```csharp
if (SceneStateProvider.IsNotRunningState())
    break;
```

## Threading Model

**No thread affinity.** Each scene runs on the thread pool. After every `await` the continuation may resume on a different thread. You cannot assume thread consistency.

### MultiThreadSync -- Queue-Based ECS Mutex

**File:** `Explorer/Assets/DCL/PerformanceAndDiagnostics/Optimization/Multithreading/MultiThreadSync.cs`

`Arch` ECS is not thread-safe. All ECS reads and writes must be serialized through `MultiThreadSync`, a queue-based synchronization primitive that ensures only one caller at a time can access the ECS world.

Key internals:
- **`Owner`** -- A named waiter backed by a `ManualResetEventSlim`. Each logical caller creates one (e.g. `EngineAPIImplementation` has its own `syncOwner`).
- **`GetScope(Owner)`** -- Enqueues the owner, blocks until it reaches the front (10-second timeout), and returns a `Scope`.
- **`Scope`** -- A `readonly struct` implementing `IDisposable`. On disposal it dequeues the owner and signals the next waiter in line.

Usage:

```csharp
// Acquire a scope before touching ECS state (from any thread)
using MultiThreadSync.Scope mutex = multiThreadSync.GetScope(syncOwner);

// Now safe to call crdtWorldSynchronizer.ApplySyncCommandBuffer(...)
// or any World read/write
```

The `BoxedScope` variant allows deferred acquire/release for cases where the scope cannot be neatly wrapped in a `using` block.

### SyncedGroup -- Automatic System Synchronization

**File:** `Explorer/Assets/DCL/Infrastructure/ECS/Groups/SyncedGroup.cs`

Systems in scene worlds use `SyncedGroup` subclasses (`SyncedInitializationSystemGroup`, `SyncedSimulationSystemGroup`, `SyncedPresentationSystemGroup`, `SyncedPreRenderingSystemGroup`) that guard `Update`, `BeforeUpdate`, and `AfterUpdate` behind a `SceneState.Running` check. This prevents systems from running during disposal or after errors:

```csharp
public abstract class SyncedGroup : CustomGroupBase<float>
{
    private readonly ISceneStateProvider sceneStateProvider;

    public override void Update(in float t, bool throttle)
    {
        if (sceneStateProvider.State != SceneState.Running)
            return;

        UpdateInternal(in t, throttle);
    }
    // BeforeUpdate and AfterUpdate follow the same pattern
}
```

### When to Use Explicit Mutex vs SyncedGroup

| Situation | Approach |
|-----------|----------|
| Normal ECS system `Update` | `SyncedGroup` handles it automatically |
| Async flow outside `Update` (e.g. `LoadSystemBase`) | Use `MultiThreadSync.GetScope()` explicitly |
| `EngineAPIImplementation.ApplySyncCommandBuffer` | Uses `MultiThreadSync.GetScope()` to lock during CRDT application |
| Read-only ECS access from background thread | Still requires `MultiThreadSync.GetScope()` |

> **Warning:** If you don't acquire a mutex, you will face random unidentifiable exceptions from `Arch` internals.

## Scene Downloading

TODO insert a principle scheme

## CRDT

We have [our own custom](https://github.com/decentraland/unity-explorer/tree/main/Explorer/Assets/Scripts/CRDT) allocation-free highly-performant implementation of [the CRDT protocol](https://adr.decentraland.org/adr/ADR-117).

**Core characteristics:**
- The process executes off the main thread
- `PoolableCollection`s based on `ArrayPool<T>.Shared` hide the complexity of having individual pools for different collection types and provide thread-safety out of the box
- No temporary allocations: Messages processing is driven by the implementation of `IMemoryOwner<byte>` that uses prewarmed pools under the hood. When the message is disposed of the rented buffer returns to the pool. This pool is thread-safe
- State storing is based on `structures` that are designed to be as lightweight as possible
- Messages deserialization is based on `ReadOnlyMemory<byte>` that is continuously advanced forward to prevent allocations
- Deserialization uses `ByteUtils` to slice memory regions into typed structures in an `unsafe` manner. This process is much faster than the managed one and is close to `reinterpret_cast` from `C`

## CRDT - ECS Bridge

### Messages Reconciliation

**File:** `Explorer/Assets/DCL/Infrastructure/CRDT/Protocol/CRDTProtocol.cs`

`CRDTProtocol.ProcessMessage` dispatches each incoming `CRDTMessage` by its `CRDTMessageType` and returns a `CRDTReconciliationResult` describing the effect on local state:

**LWW messages** (`PUT_COMPONENT`, `DELETE_COMPONENT`, `AUTHORITATIVE_PUT_COMPONENT`):

1. If the entity was previously deleted (tracked in `deletedEntities`), the message is ignored.
2. If no local state exists or the incoming timestamp is higher, the local state is replaced. `AUTHORITATIVE_PUT_COMPONENT` skips the timestamp check entirely (server wins).
3. On equal timestamps, a byte-level data comparison (`CRDTMessageComparer.CompareData`) breaks the tie -- the lexicographically larger data wins.
4. The old `IMemoryOwner<byte>` data is disposed when replaced; incoming data is disposed if the message loses reconciliation.

The returned `CRDTReconciliationEffect` is one of: `ComponentAdded`, `ComponentModified`, `ComponentDeleted`, or `NoChanges`.

**GOVS messages** (`APPEND_COMPONENT`):

Accumulated in a sorted list per entity+component pair. A binary search prevents duplicates (matching on both timestamp and data). The list is capped at 100 entries per entity-component pair; when exceeded, all entries are cleared before adding the new one.

**Entity deletion** (`DELETE_ENTITY`):

Removes all LWW and APPEND data for the entity, disposes all associated `IMemoryOwner<byte>` buffers, and records the entity version in `deletedEntities` so future messages for older versions are rejected.

**Zero-allocation design notes:**
- State is stored in `PooledDictionary` / `PooledList` backed by `ArrayPool<T>.Shared`
- Message data uses `IMemoryOwner<byte>` -- disposing returns the buffer to the pool
- Deserialization operates on `ReadOnlyMemory<byte>` advanced forward (no copies)
- `ByteUtils` performs unsafe memory slicing (`reinterpret_cast`-style)

### SDK Components

**File:** `Explorer/Assets/DCL/Infrastructure/Global/ComponentsContainer.cs`

SDK components are mapped to CRDT component IDs via the `ISDKComponentsRegistry`, which is built by `ComponentsContainer.Create()`. Each component is registered with a `ComponentID` (protocol-defined integer) and a builder that specifies serialization, pooling, and reset behavior:

```csharp
sdkComponentsRegistry
    .Add(SDKComponentBuilder<SDKTransform>.Create(ComponentID.TRANSFORM)
        .WithPool(sdkTransform => { sdkTransform.Clear(); ... })
        .WithCustomSerializer(new SDKTransformSerializer())
        .Build())
    .Add(SDKComponentBuilder<PBGltfContainer>.Create(ComponentID.GLTF_CONTAINER)
        .AsProtobufComponent())
    // ... 40+ additional SDK components
```

Each registered component provides:
- A `ComponentID` integer mapping the CRDT component ID from the [protocol](https://github.com/decentraland/protocol)
- A serializer (Protobuf for most, custom for `SDKTransform`)
- A pool with `onGet`/`onRelease` callbacks for zero-allocation reuse
- A component type classification (`.AsProtobufComponent()` for scene-authored, `.AsProtobufResult()` for engine-authored responses)

When `CRDTWorldSynchronizer` processes an incoming CRDT message, it looks up the component by its `ComponentID` in the registry to deserialize and apply it to the ECS world.

### Synchronization with ECS

`Arch` is not thread-safe so it's vital to access and modify the ECS state from one thread at a time. It does not matter though from which thread.
To provide the best performance possible this possibility is utilized:
- `MultiThreadSync` is used for synchronization (see [Threading Model](#threading-model) above).
- Both `EngineAPIImplementation` and ECS Systems/Worlds are synchronized by the same instance.
- When new changes come from the scene the last application step provided by `ICRDTWorldSynchronizer.ApplySyncCommandBuffer` acquires a mutex and forbids the main thread (where systems run) to manipulate ECS state.
- While new components are being added from `ApplySyncCommandBuffer` the rendering thread "waits" so it's vital to keep this step optimized as much as possible to ensure the stable framerate.
- On the level of systems the synchronization capability is provided by the `SyncedGroup`. It ensures that `Update`, `Initialize` and `Dispose` calls are synchronized so no manual actions are required.
- When access to ECS state is used (even read-only access should be synchronized) outside of the `Update` loop, `MultiThreadSync` corresponding to the given scene should be used explicitly by acquiring `GetScope` in a `using` block. E.g. it's utilized by `LoadSystemBase` that launched an `async` flow which is not aligned with the `Update` cycle.

> **Warning:** If you don't acquire a mutex, you will face random unidentifiable exceptions from `Arch` internals.

#### ICRDTWorldSynchronizer

**File:** `Explorer/Assets/DCL/Infrastructure/CrdtEcsBridge/WorldSynchronizer/ICRDTWorldSynchronizer.cs`
**Implementation:** `Explorer/Assets/DCL/Infrastructure/CrdtEcsBridge/WorldSynchronizer/CrdtEcsSynchronizer.cs`

```csharp
public interface ICRDTWorldSynchronizer : IDisposable
{
    IWorldSyncCommandBuffer GetSyncCommandBuffer();
    void ApplySyncCommandBuffer(IWorldSyncCommandBuffer syncCommandBuffer);
}
```

The concrete `CRDTWorldSynchronizer` uses a `SemaphoreSlim` (not a `Mutex`) to guard command buffer access. This is intentional: a `Mutex` requires acquire and release to happen on the same thread, but in a thread-pool environment `GetSyncCommandBuffer` and `ApplySyncCommandBuffer` will typically execute on different threads.

The flow:
1. **`GetSyncCommandBuffer()`** -- Waits on the semaphore (5-second timeout), then returns a fresh `WorldSyncCommandBuffer`. Only one buffer can be rented at a time.
2. The caller fills the buffer with CRDT mutations via `SyncCRDTMessage` and `FinalizeAndDeserialize`.
3. **`ApplySyncCommandBuffer(buffer)`** -- Applies all mutations to the `World` and entity map, then releases the semaphore so the next call to `GetSyncCommandBuffer` can proceed.

### Outgoing Messages

`Systems` have a capability to write and propagate messages to JavaScript scene. This communication enables JavaScript to understand Player and Camera position, player input, etc.

`Systems` should use `IECSToCRDTWriter` to `PUT`, `APPEND`, or `DELETE` components and entities according to the CRDT Protocol. Then these changes are propagated to the scene by `EngineAPIImplementation`.

- Data should be binary serializable by `Protobuf` (most of the components) or custom logic (e.g. `SDKTransform`)
- `Model` passed to `IECSToCRDTWriter` is not stored but directly serialized into a `byte` buffer. Thus, it's not necessary to pool `Messages`, you can have a single shared instance that is filled with data on demand, then serialized and reused.
- `Byte` buffers are heavily amortized by pooling so this process can be counted as runtime allocation-free.
- When this data is sent to the scene by `EngineApiImplementation` buffers are returned to the pool.
- For `PUT` and `DELETE` messages it's assumed that the last message overrides the previous one while they are not sent to the scene. **Keep in mind that you can write messages more frequently than the scene updates.** Thus, the scene will receive the most recent state on its next update.
- You should be reasonable in writing messages, especially `APPEND` ones, to distant throttled scenes. Generally, you should limit it by the `bucket` the scene belongs to.

#### Full Inbound Flow: Scene -> ECS -> Response

**File:** `Explorer/Assets/DCL/Infrastructure/CrdtEcsBridge/JsModulesImplementation/EngineAPIImplementation.cs`

When the JS scene calls `CrdtSendToRenderer`, the following steps execute on a background thread in `EngineAPIImplementation`:

```
JS scene -> EngineApiWrapper.CrdtSendToRenderer(ITypedArray<byte>)
         -> EngineAPIImplementation.CrdtSendToRenderer(ReadOnlyMemory<byte>)
```

1. **Deserialize** -- `crdtDeserializer.DeserializeBatch` converts the raw bytes into a `List<CRDTMessage>`.
2. **Reconcile** -- Each message passes through `crdtProtocol.ProcessMessage`, which returns a `CRDTReconciliationResult` with the conflict resolution outcome.
3. **Buffer** -- `worldSyncBuffer.SyncCRDTMessage(message, effect)` prepares ECS mutations in a command buffer.
4. **Apply** -- `ApplySyncCommandBuffer` acquires a `MultiThreadSync` scope, calls `crdtWorldSynchronizer.ApplySyncCommandBuffer` to write into the ECS World, then opens the system update gate.
5. **Respond** -- `SerializeOutgoingCRDTMessages` collects pending outgoing messages from `IOutgoingCRDTMessagesProvider`, serializes them into a `PoolableByteArray`, and syncs them into the local CRDT state via `EnforceLWWState` to keep timestamps correct.

The serialized response is returned through the wrapper back to the JS context.

## Gotchas

- **V8 engines are not poolable.** Each scene creates a new `V8ScriptEngine`; when disposed, the engine is gone. This creates GC pressure but `ScriptEngine` is not reusable. ClearScript handles unmanaged cleanup.
- **`PoolableByteArray` must be disposed.** It wraps a pooled `byte[]` with a release callback. Failing to dispose leaks the rented array. Use `ReleaseAndDispose()` for wrapper cleanup.
- **`SceneExceptionsHandler` per-minute tolerance thresholds.** JS errors allow 30 per minute, engine errors allow 3 per minute. Exceptions are tracked with sliding-window timestamps. Exceeding the threshold suspends the scene (transitions to `JavaScriptError` or `EngineError`).
- **`Atomic<SceneState>` is required** because state transitions happen from background threads. Always use `State.Set()` / `State.Value()`, never direct assignment.
- **`CRDTWorldSynchronizer` uses `SemaphoreSlim`, not `Mutex`**, because acquire and release happen on different threads. Using a `Mutex` would throw an `ApplicationException`.
- **No thread affinity.** After any `await` in scene code, the thread may change. Never cache `Thread.CurrentThread` or use thread-local storage. API implementations must be thread-agnostic.

## SDK7 to SDK6 Adaptation Layer

The Adaptation Layer is a bridge to adapt scenes from SDK6 to SDK7. Basically, it's an SDK7 Scene that implements SDK6. [You can see that project here](https://github.com/decentraland/sdk7-adaption-layer).

### Injection Code

In order to run the Adaptation Layer, the Explorer is required to inject the SDK7 Source Code when it tries to download an SDK6 Scene.

We can see the difference in the following diagram:

![adaptation_layer (1)](https://github.com/decentraland/unity-explorer/assets/12563266/38f539c5-16b8-4c8d-b5bc-56b9e533111d)

Going a bit deep into how it works, the SDK7 Adaptation Layer loads the SDK6 Source Code of the Scene (using `RequireFile` from the `Runtime` Module), and then it evaluates it, and starts adapting the SDK6 behavior to SDK7.

There is no need to take any other consideration of how the Adaptation Layer works after you load the SDK7 Adaptation Layer for the SDK6 Scene. The Explorer is running an SDK7 Scene like any other scene.

### Modifying SDK7 Adaptation Layer

To change something in the SDK7 Adaptation Layer you need to go to [its repo](https://github.com/decentraland/sdk7-adaption-layer). You can debug it as an SDK7 Scene using the Unity Renderer Implementation.

If you want to test it in the Explorer Alpha, you can build it using `npm run build` and copying the `index.js` that produces to the Streaming Assets, and loading it locally instead of the remote one changing [this code](https://github.com/decentraland/unity-explorer/blob/13e6e830ab648e29c64bb97a6302282551fb1236/Explorer/Assets/Scripts/SceneRunner/SceneFactory.cs#L128).
