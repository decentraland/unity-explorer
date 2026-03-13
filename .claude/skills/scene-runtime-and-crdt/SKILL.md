---
name: scene-runtime-and-crdt
description: "Scene runtime internals — V8/ClearScript engine, CRDT protocol, JS module system, scene lifecycle, and threading. Use when implementing or modifying scene runtime modules (require/wrapper/API pattern), working with CRDTProtocol or CRDTWorldSynchronizer, handling MultiThreadSync for ECS access, debugging scene state transitions, or implementing new JS API modules."
user-invocable: false
---

# Scene Runtime & CRDT

## Sources

- `docs/scene-runtime.md` -- SDK7 execution via ClearScript/V8, CRDT bridge, JS modules
- `docs/architecture-overview.md` -- Worlds, threading, ECS-async marriage

---

## Scene Lifecycle State Machine

**File:** `Explorer/Assets/DCL/Infrastructure/SceneRunner/Scene/SceneState.cs`

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

**Transitions:**
- `NotStarted` -> `Running` via `SceneFacade.StartUpdateLoopAsync` (calls `SetRunning`)
- `Running` -> `EngineError` / `EcsError` / `JavaScriptError` via `SceneExceptionsHandler` when tolerance exceeded
- `Running` -> `Disposing` -> `Disposed` via `SceneFacade.DisposeAsync`

**`ISceneStateProvider`** exposes `Atomic<SceneState> State`, `TickNumber`, `IsCurrent`, and `EngineStartInfo`. The `Atomic<T>` wrapper provides thread-safe reads/writes since state is mutated from background threads.

**`IsNotRunningState()`** returns true for `Disposing`, `Disposed`, `JavaScriptError`, or `EngineError` -- used as a guard to break the update loop.

---

## Threading Model (CRITICAL)

> See `docs/scene-runtime.md` -- "the scene lifecycle is thread agnostic and executes in a separate thread."

**No thread affinity.** Each scene runs on the thread pool. After every `await`, the continuation may resume on a different thread. You cannot assume thread consistency.

### MultiThreadSync -- Mutex for ECS Access

**File:** `Explorer/Assets/DCL/PerformanceAndDiagnostics/Optimization/Multithreading/MultiThreadSync.cs`

Arch ECS is not thread-safe. All ECS reads and writes must be serialized through `MultiThreadSync`:

```csharp
// Acquire a scope before touching ECS state (from any thread)
using MultiThreadSync.Scope mutex = multiThreadSync.GetScope(syncOwner);

// Now safe to call crdtWorldSynchronizer.ApplySyncCommandBuffer(...)
// or any World read/write
```

- `GetScope(Owner)` blocks until the queue grants access (10s timeout)
- `Owner` is a named `ManualResetEventSlim`-based waiter -- create one per logical caller
- The scope is a `readonly struct` implementing `IDisposable` -- release is automatic

### SyncedGroup -- Automatic System Synchronization

**File:** `Explorer/Assets/DCL/Infrastructure/ECS/Groups/SyncedGroup.cs`

Systems in scene worlds use `SyncedGroup` subclasses (`SyncedSimulationSystemGroup`, etc.) that guard `Update`/`BeforeUpdate`/`AfterUpdate` behind a `SceneState.Running` check. This prevents systems from running during disposal:

```csharp
public override void Update(in float t, bool throttle)
{
    if (sceneStateProvider.State != SceneState.Running)
        return;
    UpdateInternal(in t, throttle);
}
```

### When Explicit Mutex Is Needed

- `SyncedGroup` handles synchronization for the normal ECS update loop -- no manual action needed
- Use `MultiThreadSync.GetScope()` explicitly for ECS access **outside** the update loop (e.g., async flows in `LoadSystemBase`, `EngineAPIImplementation.ApplySyncCommandBuffer`)

---

## Module Implementation Pattern

> See `docs/scene-runtime.md` -- "How to Implement a Module"

Each JS API module follows a **4-file pattern**:

### 1. Interface

Defines the C# contract. Example: `IEngineApi`

### 2. Implementation

Core logic, thread-agnostic. Example: `EngineAPIImplementation` -- receives CRDT bytes, deserializes, reconciles, syncs to ECS, serializes outgoing messages.

### 3. JsApiWrapper

Bridges JS calls to C#. Extends `JsApiWrapper<TApi>` which holds the API impl and a `CancellationTokenSource` for disposal. The wrapper catches exceptions and routes them through `ISceneExceptionsHandler`:

```csharp
// From EngineApiWrapper.cs (condensed)
public class EngineApiWrapper : JsApiWrapper<IEngineApi>
{
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
}
```

### 4. JS Module + Registration

- JS module in `Explorer/Assets/StreamingAssets/Js/Modules/EngineApi.js`
- Compiled modules loaded via `SceneModuleHub.LoadAndCompileJsModules`
- Wrappers registered via `SceneRuntimeImpl.Register<T>(name, wrapper)` and added as host objects
- Full module wiring done in `SceneFactory.CreateSceneAsync` which calls `sceneRuntime.RegisterAll(...)`

---

## CRDT Protocol Internals

**File:** `Explorer/Assets/DCL/Infrastructure/CRDT/Protocol/CRDTProtocol.cs`

### LWW vs GOVS Processing

`ProcessMessage` dispatches by `CRDTMessageType`:

- **LWW** (`PUT_COMPONENT`, `DELETE_COMPONENT`, `AUTHORITATIVE_PUT_COMPONENT`): Timestamp comparison. Higher timestamp wins. On tie, byte-level data comparison breaks it. Returns `CRDTReconciliationResult` with effect (`ComponentAdded`, `ComponentModified`, `ComponentDeleted`, `NoChanges`).
- **GOVS** (`APPEND_COMPONENT`): Accumulates values in a sorted list per entity+component. Binary search prevents duplicates. Capped at 100 entries per entity-component pair.
- **`DELETE_ENTITY`**: Removes all LWW and APPEND data for the entity, tracks version in `deletedEntities`.

### Zero-Allocation Design

- State stored in `PooledDictionary` / `PooledList` backed by `ArrayPool<T>.Shared`
- `IMemoryOwner<byte>` for message data -- disposed returns buffer to pool
- Deserialization via `ReadOnlyMemory<byte>` advanced forward (no copies)
- `ByteUtils` for unsafe memory slicing (`reinterpret_cast`-style)

### State Structure

```csharp
internal struct State
{
    // Entity Number -> deleted Entity Version
    internal readonly PooledDictionary<int, int> deletedEntities;
    // ComponentId -> (CRDTEntity -> EntityComponentData) for LWW
    internal readonly PooledDictionary<int, PooledDictionary<CRDTEntity, EntityComponentData>> lwwComponents;
    // ComponentId -> (CRDTEntity -> list of EntityComponentData) for APPEND
    internal readonly PooledDictionary<int, PooledDictionary<CRDTEntity, PooledList<EntityComponentData>>> appendComponents;
    internal int messagesCount;
}
```

---

## ECS-CRDT Bridge

### Inbound: Scene -> ECS

**File:** `Explorer/Assets/DCL/Infrastructure/CrdtEcsBridge/JsModulesImplementation/EngineAPIImplementation.cs`

The flow in `CrdtSendToRenderer` (called from background thread):

1. **Deserialize** -- `crdtDeserializer.DeserializeBatch` into `List<CRDTMessage>`
2. **Reconcile** -- Each message through `crdtProtocol.ProcessMessage`
3. **Buffer** -- `worldSyncBuffer.SyncCRDTMessage` prepares ECS mutations
4. **Apply** -- `ApplySyncCommandBuffer` acquires `MultiThreadSync` scope, then `crdtWorldSynchronizer.ApplySyncCommandBuffer` writes to ECS World
5. **Respond** -- `SerializeOutgoingCRDTMessages` returns buffered outgoing data

### ICRDTWorldSynchronizer

**File:** `Explorer/Assets/DCL/Infrastructure/CrdtEcsBridge/WorldSynchronizer/CrdtEcsSynchronizer.cs`

- `GetSyncCommandBuffer()` -- Rents a command buffer (semaphore-guarded, one at a time)
- `ApplySyncCommandBuffer(buffer)` -- Applies mutations to `World`, releases semaphore
- Uses `SemaphoreSlim` (not `Mutex`) because acquire/release may happen on different threads

### Outbound: ECS -> Scene

Systems write to `IECSToCRDTWriter` (`PutMessage`, `AppendMessage`, `DeleteMessage`). These are collected by `IOutgoingCRDTMessagesProvider` and serialized back to the scene in `EngineAPIImplementation.SerializeOutgoingCRDTMessages`. Outgoing LWW messages are also synced into the local `CRDTProtocol` state via `EnforceLWWState` to keep timestamps correct.

---

## Gotchas

- **V8 engines are not poolable.** Each scene creates a new `V8ScriptEngine`; when disposed, the engine is gone. This creates GC pressure but `ScriptEngine` is not reusable. ClearScript handles unmanaged cleanup.
- **No thread affinity.** After any `await` in scene code, the thread may change. Never cache `Thread.CurrentThread` or use thread-local storage.
- **API implementations must be thread-agnostic.** Shared resources must be thread-safe. This applies to all `JsApiWrapper<T>` implementations.
- **`PoolableByteArray` must be disposed.** It wraps a pooled `byte[]` with a release callback. Failing to dispose leaks the rented array. Use `ReleaseAndDispose()` for wrapper cleanup.
- **`SceneExceptionsHandler` has per-minute tolerance.** JS errors (30/min) and engine errors (3/min) before the scene is suspended. Exceptions are tracked with sliding-window timestamps.
- **`Atomic<SceneState>`** is required because state transitions happen from background threads. Always use `State.Set()` / `State.Value()`, never direct assignment.

---

## Cross-References

- **sdk-component-implementation** -- Outgoing CRDT via `IECSToCRDTWriter`, component registration in `ComponentsContainer`
- **async-programming** -- Cancellation token patterns, `SuppressToResultAsync` for exception-free flows
- **diagnostics-and-logging** -- `SceneExceptionsHandler` tolerance, `ReportHub.LogException` for scene errors, `ReportCategory.CRDT` / `CRDT_ECS_BRIDGE` / `JAVASCRIPT`
