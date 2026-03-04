# AssetPromise Explained

`AssetPromise` is a core architectural piece of the asset loading mechanism in the project's ECS (Entity Component System) framework. It provides a unified, trackable, and cancellable way to request and handle assets that may load asynchronously, such as textures, GLTFs, audio clips, or data from a web request.

At its heart, `AssetPromise` is a lightweight `struct` that acts as a handle to an underlying ECS `Entity`. This entity represents the loading operation itself and holds all its state in various components. This design keeps the promise handle small and cheap to pass around, while the actual loading state is managed by the ECS world and its systems.

## Anatomy of an AssetPromise

An `AssetPromise` is a generic struct defined as `AssetPromise<TAsset, TLoadingIntention>`.

- `TAsset`: The type of the asset you expect to receive once the promise is fulfilled (e.g., `Texture2DData`, `GltfContainerAsset`, `AudioClipData`).
- `TLoadingIntention`: A component that describes *what* to load and *how*. This "intention" component holds all the necessary parameters for the loading systems, such as URLs, caching policies, and, crucially, a `CancellationTokenSource` for cancellation. Every intention must implement the `IAssetIntention` interface.

### Type Aliases for Clarity

Given the descriptive but long generic types, it's a very common and recommended practice to use `using` aliases to create shorthand names for specific promise types. This dramatically improves code readability.

```csharp
// Example from DCL.AvatarRendering.AvatarShape.Systems.AvatarLoaderSystem.cs
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution, DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
```

## The Lifecycle of an AssetPromise

The lifecycle of an `AssetPromise` involves creation, polling for the result, consuming it, and handling cleanup.

### 1. Creation

There are two main ways to create an `AssetPromise`:

**A) Standard Creation: `AssetPromise.Create(...)`**

This is the most common method. It creates a new entity in the ECS world to represent the loading process.

```csharp
// Example from DCL/Infrastructure/ECS/SceneLifeCycle/Systems/LoadFixedPointersSystem.cs

var promise = AssetPromise<SceneEntityDefinition, GetSceneDefinition>
   .Create(World, new GetSceneDefinition(new CommonLoadingArguments(url), ipfsPath), PartitionComponent.TOP_PRIORITY);
```

- `World`: The ECS world where the loading entity will be created.
- `new GetSceneDefinition(...)`: An instance of the loading intention, containing all necessary data.
- `PartitionComponent.TOP_PRIORITY`: A component that helps prioritize the work of loading systems.

This call creates an entity with the `GetSceneDefinition`, `PartitionComponent`, and `StreamableLoadingState` components. Specialized systems will then query for these entities to execute the loading logic.

**B) Finalized Creation: `AssetPromise.CreateFinalized(...)`**

This method creates a promise that is already resolved. It does **not** create an entity in the world. This is useful for returning a cached asset or an immediate failure without engaging the whole loading system pipeline.

```csharp
// A finalized promise is "born" consumed and has a result from the start.
var result = new StreamableLoadingResult<MyAsset>(new MyAsset());
AssetPromise<MyAsset, MyIntention> promise = AssetPromise<MyAsset, MyIntention>.CreateFinalized(intention, result);

// promise.IsConsumed is true right after creation.
```

### 2. Retrieving the Result

Once a promise is created, the system that performs the loading will eventually add a `StreamableLoadingResult<TAsset>` component to the promise's entity upon completion. You can then retrieve this result in a few ways.

**A) Peeking at the Result: `TryGetResult()`**

This method checks if the result is available without altering the promise or its underlying entity. It's safe to call multiple times. It's useful for systems that need to check the status of a dependency without taking ownership of it.

```csharp
if (myPromise.TryGetResult(World, out StreamableLoadingResult<MyAsset> result))
{
    // The result is ready!
    // The promise entity still exists.
    if (result.Succeeded)
        DoSomethingWith(result.Asset);
}
```

**B) Consuming the Result: `TryConsume()`**

This is the primary method for getting the result and taking ownership of it. Upon retrieving the result, it **destroys the underlying entity**, cleaning up all associated loading state components.

**Important:** A promise can only be consumed once. Calling `TryConsume` on an already consumed promise will throw an `Exception`.

```csharp
if (myPromise.TryConsume(World, out StreamableLoadingResult<MyAsset> result))
{
    // The result is ready and has been transferred to you.
    // The promise entity has been destroyed.
    if (result.Succeeded)
        TakeOwnershipOf(result.Asset);

    // The promise is now considered "consumed".
}
```

A helper extension `SafeTryConsume` exists to simplify cases where a promise might have already been consumed.

### 3. Asynchronous `async/await` Usage

For non-ECS systems or `async`-based logic, you can use `UniTask` extensions to await a promise.

- `ToUniTaskAsync(world)`: Awaits the promise and **consumes** it, returning the modified (and now consumed) promise struct.
- `ToUniTaskWithoutDestroyAsync(world)`: Awaits the promise but does **not** consume it, making it equivalent to waiting until `TryGetResult` returns `true`.

```csharp
async UniTask LoadMyAssetAsync(World world, AssetPromise<MyAsset, MyIntention> promise)
{
    // Wait for the result and consume the promise
    var consumedPromise = await promise.ToUniTaskAsync(world);

    if (consumedPromise.Result.Value.Succeeded)
        Debug.Log("Asset loaded!");
}
```

### 4. Cancellation and Cleanup

There are two ways to interrupt or clean up a promise.

- `ForgetLoading(world)`: This method is used to explicitly **cancel** an in-flight loading operation. It triggers the `CancellationTokenSource` within the loading intention (which should cause the loading system to halt its work) and destroys the promise entity.

```csharp
// If we no longer need the asset, we can cancel the request.
myPromise.ForgetLoading(World);
```

- `Consume(world)`: If you no longer care about the result but don't necessarily need to cancel the operation (or it may have already finished), you can simply call `Consume`. This ensures the promise entity is destroyed, preventing resource leaks. If the promise was already consumed, it does nothing.

```csharp
// Clean up the loading entity, we don't care about the result anymore.
myPromise.Consume(World);
```

## Lifecycle Example: Following a Promise Through Systems

The interaction between `MapPinLoaderSystem` and `LoadTextureSystem` is a great example of the `AssetPromise` lifecycle. This scenario shows how one system can request an asset, another system can load it, and the first system can then use the result.

The process involves three distinct stages handled by two different systems:

1. **Creation (`MapPinLoaderSystem`)**: A system responsible for scene logic creates a promise for an asset it needs.
2. **Processing (`LoadTextureSystem`)**: A generic loading system fulfills the promise.
3. **Resolution (`MapPinLoaderSystem`)**: The original system retrieves the result and completes its work.

### Step 1: Promise Creation (`MapPinLoaderSystem`)

The `MapPinLoaderSystem` is responsible for displaying custom pins on the map. When a pin needs a custom texture, this system initiates the loading process. This happens in its `UpdateMapPin` method, which calls a helper function `TryCreateGetTexturePromise`.

```csharp
// Simplified logic from MapPinLoaderSystem.UpdateMapPin
private void UpdateMapPin(in Entity entity, ref PBMapPin pbMapPin, ref MapPinComponent mapPinComponent)
{
    // ...
    if (useCustomMapPinIcons)
    {
        TextureComponent? mapPinTexture = pbMapPin.Texture.CreateTextureComponent(sceneData);
        // Creates a texture loading promise if the URL has changed
        TryCreateGetTexturePromise(in mapPinTexture, ref mapPinComponent.TexturePromise);
    }
    // ...
}

private bool TryCreateGetTexturePromise(in TextureComponent? textureComponent, ref Promise? promise)
{
    // ... checks if a new promise is needed ...

    // Creates the promise, which in turn creates a new entity with the loading intention
    promise = Promise.Create(
        World,
        new GetTextureIntention(textureComponentValue.Src, ...),
        partitionComponent
    );

    return true;
}
```

When a pin's texture is set, `Promise.Create(...)` is called. This is the **creation** stage: a new entity is made in the ECS `World` specifically for this loading task. This new entity gets a `GetTextureIntention` component containing the image URL. The `MapPinLoaderSystem` saves the returned `Promise` handle in its `MapPinComponent` to track the operation.

### Step 2: Promise Processing (`LoadTextureSystem`)

Next, the `LoadTextureSystem` comes into play. Its only job is to load textures, and it does so by looking for entities with a `GetTextureIntention`.

```csharp
// LoadTextureSystem's core logic is in FlowInternalAsync.
protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetTextureIntention intention, ...)
{
    // 1. Uses a web request to fetch the texture from the URL in the intention
    IOwnedTexture2D? result = await webRequestController.GetTextureAsync(...);

    // 2. Wraps the loaded texture in a result object
    return new StreamableLoadingResult<Texture2DData>(new Texture2DData(result));
}
```

This is the **processing** stage. `LoadTextureSystem` finds the promise entity that `MapPinLoaderSystem` created. It reads the `GetTextureIntention`, performs the web request to download the image, and creates the texture. When it's done, it adds a `StreamableLoadingResult<Texture2DData>` component to that same promise entity.

### Step 3: Promise Resolution (`MapPinLoaderSystem`)

Finally, the process returns to the `MapPinLoaderSystem`. A separate query in this system is continuously checking on the promises it is tracking.

```csharp
// Second query in MapPinLoaderSystem
[Query]
private void ResolveTexturePromise(in Entity entity, ref MapPinComponent mapPinComponent)
{
    if (mapPinComponent.TexturePromise is null || mapPinComponent.TexturePromise.Value.IsConsumed) return;

    // Tries to consume the promise to get the result
    if (mapPinComponent.TexturePromise.Value.TryConsume(World, out StreamableLoadingResult<Texture2DData> texture))
    {
        // On success, sends the loaded texture to the UI via an event bus
        mapPinsEventBus.UpdateMapPinThumbnail(entity, texture.Asset);

        // Clears the promise handle from the component, marking the work as done
        mapPinComponent.TexturePromise = null;
    }
}
```

This is the **resolution** stage. `ResolveTexturePromise` calls `TryConsume` on its stored promise handle. Once `LoadTextureSystem` has added the result, `TryConsume` will succeed. This one call does two things: it retrieves the loaded texture and it destroys the temporary promise entity, cleaning up the loading state. The `MapPinLoaderSystem` then uses the texture to update the UI and clears its local promise handle, completing the lifecycle.
