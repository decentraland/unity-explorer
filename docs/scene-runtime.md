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

### SDK Components

### Synchronization with ECS

`Arch` is not thread-safe so it's vital to access and modify the ECS state from one thread at a time. It does not matter though from which thread.
To provide the best performance possible this possibility is utilized:
- `MutexSync` is used for synchronization. It uses [`Mutex`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex?view=net-7.0) under the hood.
- Both `EngineAPIImplementation` and ECS Systems/Worlds are synchronized by the same instance of the `mutex`.
- When new changes come from the scene the last application step provided by `ICRDTWorldSynchronizer.ApplySyncCommandBuffer` acquires a mutex and forbids the main thread (where systems run) to manipulate ECS state.
- While new components are being added from `ApplySyncCommandBuffer` the rendering thread "waits" so it's vital to keep this step optimized as much as possible to ensure the stable framerate.
- On the level of systems the synchronization capability is provided by the `SyncedGroup`. It ensures that `Update`, `Initialize` and `Dispose` calls are synchronized so no manual actions are required.
- When access to ECS state is used (even read-only access should be synchronized) outside of the `Update` loop, `MutexSync` corresponding to the given scene should be used explicitly by acquiring `GetScope` in a `using` block. E.g. it's utilized by `LoadSystemBase` that launched an `async` flow which is not aligned with the `Update` cycle.

> **Warning:** If you don't acquire a mutex, you will face random unidentifiable exceptions from `Arch` internals.

### Outgoing Messages

`Systems` have a capability to write and propagate messages to JavaScript scene. This communication enables JavaScript to understand Player and Camera position, player input, etc.

`Systems` should use `IECSToCRDTWriter` to `PUT`, `APPEND`, or `DELETE` components and entities according to the CRDT Protocol. Then these changes are propagated to the scene by `EngineAPIImplementation`.

- Data should be binary serializable by `Protobuf` (most of the components) or custom logic (e.g. `SDKTransform`)
- `Model` passed to `IECSToCRDTWriter` is not stored but directly serialized into a `byte` buffer. Thus, it's not necessary to pool `Messages`, you can have a single shared instance that is filled with data on demand, then serialized and reused.
- `Byte` buffers are heavily amortized by pooling so this process can be counted as runtime allocation-free.
- When this data is sent to the scene by `EngineApiImplementation` buffers are returned to the pool.
- For `PUT` and `DELETE` messages it's assumed that the last message overrides the previous one while they are not sent to the scene. **Keep in mind that you can write messages more frequently than the scene updates.** Thus, the scene will receive the most recent state on its next update.
- You should be reasonable in writing messages, especially `APPEND` ones, to distant throttled scenes. Generally, you should limit it by the `bucket` the scene belongs to.

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
