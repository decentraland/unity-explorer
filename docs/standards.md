# Standards

## Memory Allocations

### Minimize Garbage Collection (GC) Pressure

Reduce the frequency and size of memory allocations to minimize the impact on garbage collection.
Reuse objects and data structures whenever possible instead of creating new ones.
Avoid creating temporary objects within frequently called methods or update loops. [Heap Allocation Viewer](https://plugins.jetbrains.com/plugin/9223-heap-allocations-viewer) is a perfect plugin for **Rider** that shows all types of allocations.
Use Object Pooling:

### Implement object pooling to reuse frequently created and destroyed objects.

Pooling allows you to recycle objects instead of allocating and deallocating them, reducing GC overhead.
Try to assume the upper estimation of the initial pool size to avoid **runtime** allocations as much as possible.
We have some custom pooling classes available under Utility:

[Pools](https://github.com/decentraland/unity-explorer/tree/main/Explorer/Assets/Scripts/Utility/Pool)

[Thread Safe Pools](https://github.com/decentraland/unity-explorer/tree/main/Explorer/Assets/Scripts/Utility/ThreadSafePool)

### Avoid permutations of collections
Preferring loose contracts (`IReadOnlyCollection`, `IReadOnlyList`, etc) over final types (`array`, `List<T>`) makes your classes and functions much more flexible.

Don't try to match the contract by calling `ToList()` and `ToArray()`, avoid them by all means.

Use `Span<T>`, `Memory<T>`, and `ArraySegment<T>` if you require a slice of an original array.

Use [`stackalloc`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/stackalloc) if you require a small temporary fixed-sized array.

Use [Unity Native Collections](https://docs.unity3d.com/Packages/com.unity.collections@1.2/manual/index.html) if compatibility with **Jobs** or Unity's low-level API is required. If the size of the collection is known beforehand, consider using `Fixed` collections which are fully allocated on stack and, thus, produce no `GC` pressure.

### Be Mindful of Serialization and Deserialization
- JSON. Using JSON is not a great idea overall. It creates a significant GC Pressure. If it is still needed consider reusing the existing objects and filling them with data instead of creating new ones. Consider using Unity's `JsonUtility` as it's more performant.
- Protobuf. Instead of creating a new instance, parse into the existing one.

### Be Mindful of Boxing and Unboxing
Avoid the use of boxing and unboxing operations, which can create unnecessary memory allocations.
Use generic collections and data structures to avoid boxing of value types.

Avoid the use of `object`.

Avoid passing a `structure` as an interface.

### Avoid String Concatenation

String concatenation using the "+" operator can create multiple intermediate string objects.
Instead, use `StringBuilder` or `string.Format` for efficient string concatenation and formatting. Avoid any string manipulation in hot paths.
If it is expected that `StringBuilder` will be used frequently cache it, clear and then reuse it.

### Do not use LINQ, it allocates too much memory.

See: [https://www.jacksondunstan.com/articles/4840](https://www.jacksondunstan.com/articles/4840)

## Be Mindful of [Lambdas](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/lambda-expressions) & Delegates overhead
Avoid unintentional variable captures: every time you invoke a function with such a delegate a new instance of a class is created by the compiler leading to the temporary allocation.

Use `static` keyword for a `lambda` or a `static` local function to explicitly indicate that there is no intention for capture: it will also help the compiler with caching a delegate so it will be instantiated only once.

## URLs handling
To simplify slashes and possible combinations handling a small library "URLHelpers" is introduced to avoid direct and error-prone `string` manipulation:
- every part of a URL is represented by a named structure (`URLDomain`, `URLParameter`, etc)
- unlike with `string` only valid concatenation operations are allowed on these structures. E.g. it's impossible to combine several domains together
- `URLBuilder` serves to create a final `URLAddress` which should be used as an address in the `WebRequest`
- you can read about URL constituents [here](https://blog.hubspot.com/marketing/parts-url): at the moment not every part is represented by a separate structure, some of them are merged together. As needed additional granularity may be added in the future

## Optimize Data Structures

Choose data structures that are efficient in terms of memory usage and access patterns.
For example, use lists or arrays instead of dictionaries or hash sets when a key-value mapping is not required.

> **Warning:** If you need to iterate over a considerable number of elements `List` and `Array` are the only good choices as they are contiguous memory regions and, thus, can be pre-loaded into the CPU cache as a single chunk. It's especially important if you perform this in a system's `Update`

## Open your mind to `structures` preference
Structs are value types and can be allocated on the stack in a short-living context, reducing memory overhead and GC pressure.
In our environment where we try to minimize runtime allocations and, thus, pre-allocate as much as possible beforehand, it's vital to understand that `structures` are more lightweight than `classes`. All their shortcomings are easy to overcome, especially with the concept of `components` in ECS.

Familiarize yourself with [this article](https://medium.com/@norm.bryar/truly-leverage-c-structs-part-1-4a3f707c40ee). A lot of information about advantages, disadvantages, and the ways of solving them is given there: pay attention to `ref`, `ref readonly`, `in` as they enable avoiding expensive copying with ease.

## Dispose of Resources Properly

If your code uses objects that implement the IDisposable interface (e.g., `FileStream`, `Texture2D`, `UnityWebRequest`), ensure proper disposal.
Call `Dispose()` or use the "using" statement to release unmanaged resources promptly.

> **Warning:** Consider disposing of Unity's objects manually (if they implement `IDisposable` and were created explicitly), don't let `GC` do all the job. In some cases (proven with `UnityWebRequest`) objects left unattended will lead to native crashes on application exit.

## Profile and Optimize

Always profile your code to identify memory allocation hotspots and optimize them.

Unity provides profiling tools like the Unity Profiler to help you identify performance bottlenecks and memory issues.

## Profile multi-threaded code
All the communication between C# and JS executes off the main thread, moreover, it's highly advised to keep all heavy processing such as serialization/deserialization and multi-iteration algorithms in a thread pool and Unity Jobs.

By default, in the Profiler only the main thread is shown: it's not enough to understand allocations and computational overhead.
It is easier to investigate background threads by switching to the "Timeline" view:
- You have to enable thread profiling manually by calling [Profiler.BeginThreadProfiling](https://docs.unity3d.com/ScriptReference/Profiling.Profiler.BeginThreadProfiling.html) and [Profiler.EndThreadProfiling()](https://docs.unity3d.com/ScriptReference/Profiling.Profiler.EndThreadProfiling.html). It's already done in the communication layer: `Profiler.BeginThreadProfiling("SceneRuntime", "CrdtSendToRenderer")`
Then they will appear on the Timeline

![image](https://github.com/decentraland/unity-explorer/assets/118179774/a0484f16-7d41-408a-aa0d-27ba8f3ea111)
- Unity does not instrument managed threads by default, you have to use [CustomSampler](https://docs.unity3d.com/ScriptReference/Profiling.CustomSampler.html) to instrument regions without "Deep Profile"
- With "Deep Profile" you are able to see the whole calls' hierarchy:
<img width="700" alt="image" src="https://github.com/decentraland/unity-explorer/assets/118179774/e6cbc77a-a80c-4ca0-bbed-7fd35e731247">

**Warning:** **Keep in mind that a single invocation can be distributed between several frames so you should not make any assumptions about frame rate until you start synchronizing your code**

- Developing a feature and making any changes to the code being executed on the background thread it's a must to validate allocations. From the Timeline it's easier to do by switching back to "Hierarchy"

<img width="350" alt="image" src="https://github.com/decentraland/unity-explorer/assets/118179774/e8772cae-17f6-40d4-ac68-7c90a5dc2abd">

From there you can easily inspect allocations occurred:

<img width="700" alt="image" src="https://github.com/decentraland/unity-explorer/assets/118179774/84f54aa0-2bca-4551-841e-e463d593ba2b">

### Profile Jobs
In a similar fashion, you can add custom samples to a job to profile code regions.
However, it might be more tricky to find them in the Timeline or Hierarchy. Every iteration of a job is a tiny piece, especially if it is compiled with Burst. You can find it by searching in the Hierarchy and switching between different Jobs Threads as shown in the following picture:

![Screenshot 2023-10-05 143613](https://github.com/decentraland/unity-explorer/assets/118179774/f1f5b936-a6bd-4df2-a5d3-8a086d984d07)
<img alt="Screenshot 2023-10-05 143459" src="https://github.com/decentraland/unity-explorer/assets/118179774/1e3ea000-a5e8-402e-bdea-20e3e888ec7f">

**Warning:** **Despite it's possible to make a managed allocation in a job, it's highly discouraged and leads to significant performance penalties. Thus, it must be avoided by any means.**

## Use the Unity Memory Profiler
Utilize the Unity Memory Profiler to analyze and optimize memory allocations.

## Performance

## Tests

- Newly written code should always utilize a reasonably high test coverage.
- We use the [NUnit](https://nunit.org/) and [NSubstitute](https://nsubstitute.github.io/) testing framework.
- For Integration Tests consider using `DemoWorlds`
- Consider covering your code with [Performance Tests](https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.0/manual/index.html) (see `BillboardTest` or `CacheCleanerIntegrationTests` for examples)

## Error Handling & Reporting
