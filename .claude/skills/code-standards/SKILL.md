---
name: code-standards
description: "C# naming conventions, member ordering, formatting rules, nullable reference types, memory/GC rules, test patterns, and PR standards. Use BEFORE writing, modifying, or reviewing ANY .cs file in this Unity project — invoke it before the first Edit/Write on production C#, not only for large changes. Applies to ECS systems, controllers, tests, utilities, and plugins."
user-invocable: false
---

# Code Standards & Conventions

## Sources

- `docs/code-style-guidelines.md` — Full naming and formatting rules
- `docs/standards.md` — Performance, memory, and quality standards
- `docs/branch-and-pr-standards.md` — Git workflow and PR conventions
- `CLAUDE.md` — Condensed project rules

---

## Naming Conventions

| Scope | Style | Example |
|-------|-------|---------|
| Namespaces, classes, structs, interfaces, enums, public methods/properties/fields | `PascalCase` | `BillboardSystem`, `Update()` |
| Non-public fields/properties, parameters, locals | `camelCase` | `exposedCameraData`, `cameraPosition` |
| Constants, static readonly | `ALL_UPPER_SNAKE_CASE` | `MINIMUM_DISTANCE_TO_ROTATE_SQR` |
| Interfaces | `I` prefix | `IExposedCameraData` |
| Async methods | `Async` suffix | `InitializeAsync`, `LoginAsync` |
| Events | Past tense, no `On` prefix | `ViewShowingComplete` |
| Unused parameters | `_`, `__`, `___` | `Update(float _)` |

## Member Ordering

Within a class, group members in this order:
1. Enums / delegates
2. Fields
3. Properties
4. Events
5. Methods
6. Nested classes

Within each group, order by visibility: **public → internal → protected internal → protected → private**.

**Field ordering within visibility:**
1. `const` / `static readonly`
2. `static`
3. `readonly`
4. Regular public fields
5. `[SerializeField]` fields
6. Non-public fields

**Method ordering:**
1. Constructor / setup
2. Destructor / Dispose
3. Public APIs
4. Unity callbacks
5. Internal methods
6. Protected methods
7. Private helpers (placed after the methods that call them)

### Code Example — Member Ordering

From `BillboardSystem.cs`:

```csharp
public partial class BillboardSystem : BaseUnityLoopSystem
{
    // 1. Constants first
    private const float MINIMUM_DISTANCE_TO_ROTATE_SQR = 0.25f * 0.25f;

    // 2. Readonly fields
    private readonly IExposedCameraData exposedCameraData;

    // 3. Constructor (internal visibility — see CLAUDE.md §1)
    internal BillboardSystem(World world, IExposedCameraData exposedCameraData) : base(world)
    {
        this.exposedCameraData = exposedCameraData;
    }

    // 4. Override method (Update is the system's main entry point)
    protected override void Update(float t)
    {
        // ...
        UpdateRotationQuery(World, cameraPosition, cameraRotationAxisZ);
    }

    // 5. Private helper — placed after the method that calls it
    [Query]
    private void UpdateRotation(
        [Data] in Vector3 cameraPosition,
        [Data] in Quaternion cameraRotationAxisZ,
        ref TransformComponent transform,
        in PBBillboard billboard)
    {
        // ...
    }
}
```

## Formatting

- **Indentation:** 4 spaces (no tabs)
- **Braces:** Allman/BSD style — opening brace on new line
- **`var`:** Only when type is evident from the right side
- **No LINQ** — allocates too much memory; use loops
- **String interpolation:** Use `$""` not `+` concatenation
- **Expression-bodied properties:** Same line. Expression-bodied methods: new line.
- **Unity callbacks:** Always use braces (even single-line)
- Follow [`.editorconfig`](../../../Explorer/.editorconfig) and enable "Format On Save"

## Memory & GC Rules

- Minimize GC pressure — reuse objects, use object pooling (`Utility/Pool`, `Utility/ThreadSafePool`). When you rent from a pool, the matching release belongs in the same lifecycle scope (`using` / `Dispose` / `finally`). For pooled lists held as fields, mirror every `Pool.Get()` against a `Pool.Release()` in `Dispose()` — silently dropping a rented list is a quiet leak that GCs the list reference but inflates the pool with new allocations on the next `Get()`.
- Prefer `IReadOnlyCollection<T>` / `IReadOnlyList<T>` over `List<T>` / arrays; avoid `ToList()` / `ToArray()`
- Use `Span<T>`, `Memory<T>`, `ArraySegment<T>`, `stackalloc` for slices
- Avoid boxing/unboxing — do not pass structs as interfaces, do not use `object`
- Use `StringBuilder` for string concatenation; avoid string manipulation in hot paths
- Lambdas: avoid unintentional variable captures; use `static` keyword on lambdas/local functions
- Prefer `struct` over `class` where possible; use `ref`, `ref readonly`, `in` to avoid copying
- Always call `Dispose()` or use `using`; manually dispose Unity objects implementing `IDisposable`

## Nullable Reference Types

The project is migrating to nullable reference types. ~80 of ~153 assemblies already enable it at the project level; the rest default to disabled.

### Rules for New and Modified Files

- **When modifying an existing file that lacks nullable annotations**, add proper nullable annotations as part of your change.
- **Do not add `#nullable disable`** — use it only as a last-resort escape for generated code or third-party interop.

### Annotation Rules

- **Parameters that legitimately accept null** must be typed `T?` (e.g., `string? name`).
- **Return types that may return null** must be typed `T?`.
- **Fields and properties that can be null** must be typed `T?`.
- **Never use the null-forgiving operator `!`** to silence warnings — fix the root cause instead. The only acceptable use is in test code where NSubstitute returns null proxies.

## Comments

- XML `/// <summary>` on public classes and non-obvious public methods
- Comments start uppercase, end with period
- No commented-out code
- No block `/* */` comments

## Test Conventions

- Class name: `{Feature}Tests` or `{Feature}Should`
- Method names reflect Arrange/Act/Assert intent (use `Should`, `When`)
- Body split by `// Arrange`, `// Act`, `// Assert`
- Use NUnit + NSubstitute
- High test coverage for new code

### Code Example — AAA Test Pattern

From `AvatarLoaderSystemShould.cs`:

```csharp
public class AvatarLoaderSystemShould : UnitySystemTestBase<AvatarLoaderSystem>
{
    [SetUp]
    public void Setup()
    {
        // Setup shared test state
        pbAvatarShape = new PBAvatarShape { BodyShape = BODY_SHAPE_MALE, Name = FAKE_NAME };
        IRealmData realmData = Substitute.For<IRealmData>();
        system = new AvatarLoaderSystem(world);
    }

    [Test]
    public void StartAvatarLoad()
    {
        //Arrange
        Entity entity = world.Create(pbAvatarShape, PartitionComponent.TOP_PRIORITY);

        //Act
        system.Update(0);

        //Assert
        AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
        Assert.AreEqual(avatarShapeComponent.BodyShape.Value, BODY_SHAPE_MALE);
        Assert.AreEqual(avatarShapeComponent.Name, FAKE_NAME);
    }

    [Test]
    public void UpdateAvatarLoad()
    {
        //Arrange
        Entity entity = world.Create(pbAvatarShape, PartitionComponent.TOP_PRIORITY);
        system.Update(0);

        //Act
        pbAvatarShape.BodyShape = BODY_SHAPE_FEMALE;
        pbAvatarShape.IsDirty = true;
        system.Update(0);

        //Assert
        ref AvatarShapeComponent avatarShapeComponent = ref world.Get<AvatarShapeComponent>(entity);
        Assert.AreEqual(avatarShapeComponent.BodyShape.Value, BODY_SHAPE_FEMALE);
    }
}
```

## Anti-Patterns (common in AI-authored code)

Reviewers have flagged these patterns as AI-generated code smells. The underlying rule is: **don't add structure until it pays for itself**. Splits, interfaces, and indirections must buy polymorphism, reuse, or test isolation — not exist "for SRP" alone.

### 1. Bridge / wrapper classes on the same abstraction layer

If class `B` exists only to forward calls to class `A`, with one caller, no polymorphism, and no test seam, delete `B` and call `A` directly.

```csharp
// WRONG — RemoteReactionReceiver is a bridge that only exists to hand
// results to SituationalRemoteTarget via a delegate.
public class RemoteReactionReceiver
{
    private readonly Action<ReceivedReaction> onReceived;
    public RemoteReactionReceiver(Action<ReceivedReaction> onReceived)
        => this.onReceived = onReceived;

    public void Tick(float dt) { /* ... */ onReceived(reaction); }
}

// RIGHT — return the value, let the parent process it.
public class RemoteReactionReceiver
{
    public void Tick(float dt, List<ReceivedReaction> reusableBuffer) { /* ... */ }
}
```

### 2. Delegate-wrapped properties

Don't wrap every property of a config in its own `Func<T>`. Pass the object.

```csharp
// WRONG
new SituationalRemoteTarget(
    getMaxDistance: () => reactionsConfig.MaxDistance,
    getSpawnRadius: () => reactionsConfig.SpawnRadius,
    getLifetime:    () => reactionsConfig.Lifetime);

// RIGHT
new SituationalRemoteTarget(reactionsConfig);
```

If you only need to capture one changing value (e.g. `messageId`), store it as a field on the consumer, not as a closure threaded through constructors.

### 3. Interfaces with one implementation and no test coverage

Delete the interface. The concrete class is the contract.

### 4. Defensive null-checks against non-null annotations

```csharp
// WRONG — field is declared as MessageReactionsView (non-null)
private MessageReactionsView reactionsView;

public void Refresh()
{
    if (reactionsView == null) return;   // can never fire
    reactionsView.UpdateCount(5);
}
```

If the declared type is `T` (not `T?`), don't null-check it. Every redundant check misleads the reader about what can actually happen at runtime.

If a value *can* be null, change the type to `T?`. If it can't, delete the guard.

### 5. Debug/mock code inside production paths

```csharp
// WRONG — runs on every reaction update in retail builds
int displayCount = messageConfig.DebugRandomizeReactionCounts
    ? Random.Range(1, 100)
    : count;

// RIGHT — editor-only, zero cost in production
int displayCount = count;
#if UNITY_EDITOR
    if (messageConfig.DebugRandomizeReactionCounts)
        displayCount = Random.Range(1, 100);
#endif
```

Runtime bool flags do not count as "debug-only" — the branch still compiles and executes. Use `#if UNITY_EDITOR`, or extract debug logic to an editor-only companion system that doesn't run in player builds.

### 6. Retry loops without a termination condition

A loop that re-queues unresolved items will spin forever when the upstream source consistently returns nothing. Always have a "give up" predicate — max attempts, a known-bad sentinel, or a timeout.

### 7. Extracting when you should merge

If `X` does nothing useful without `Y`, and there is no second consumer of `X`, merge them. Splits must pay for themselves in polymorphism, reuse, or test isolation.

### 8. Comments that narrate authoring, testing, or a good name

A comment must state only what the annotated code itself does or guarantees. Delete:

- **Authoring / testing narration** — how the code was written or must be verified. `// NOTE: the visual result must be verified in the Unity editor — it cannot be validated headlessly.` is a classic AI tell; it describes your workflow, not the code.
- **Restatements of a good name** — if the member name already says it, the comment is noise.
- **Caller / external-behavior narration** — see the CLAUDE.md anti-pattern "Comments that narrate caller/external behavior". A comment must not describe what other scopes do with the result.

When a comment *is* warranted, state the real non-obvious invariant explicitly:

```csharp
// WRONG — narrates the author's process
// The label size and position were tuned by eye in the editor.

// RIGHT — states an invariant the reader cannot infer from the code
// Not thread-safe — main-thread only (renders into a shared RenderTexture).
```

### 9. Leaking a class's responsibility into its consumer

The class that owns the data owns the decisions about it. If a player owns the video texture, it also owns *which* texture to surface (including a "camera-off" placeholder) — the consuming ECS system must not reach in, pick the texture, and blit it. Push the decision behind the owner's API and pass real collaborators into the constructor directly, rather than reconstructing or re-deciding them in the consumer.

```csharp
// WRONG — system reaches into the player and decides which texture to show
Texture tex = player.IsMuted && placeholder != null
    ? placeholder.TextureFor(player.StreamerName)
    : player.LastTexture();
Graphics.Blit(tex, target);

// RIGHT — the player owns the decision; the system just consumes it
Graphics.Blit(player.CurrentTexture, target);
```

### 10. Partially-initialized instances

A constructor returns a fully-valid object or throws. Do not scatter `EnsureX()` / `initialized` / `creationFailed` guards that let a half-built instance exist and degrade later — that shatters one invariant across many call sites. Require mandatory dependencies as non-null constructor arguments and validate invariants at creation time.

```csharp
// WRONG — instance can exist half-built; every method must re-check
public Texture? TextureFor(string name)
{
    if (!EnsureRig()) return null; // rig may have failed to build earlier
    // ...
}

// RIGHT — if the ctor returned, the rig exists. No guard, no nullable return.
public Texture TextureFor(string name) { /* rig is always valid here */ }
```

Same principle for one-time wiring: subscribe to an event/pipe **once in the constructor**, not lazily behind `if (!subscribed)` guards. A lazy re-subscribe path overwrites the stored delegate and leaves a dangling callback; the guards are redundant once the subscription is a construction invariant.

### 11. Names that hide narrowed or implicit behavior

If a member silently ignores a case, the name must say so. A "video muted" check that deliberately skips screen-share tracks is `IsCurrentCameraVideoMuted`, not `IsCurrentVideoMuted`. Rename types to their real responsibility (`CameraOffScreenComposer` → `AvatarPlaceHolderTextureSource` when the class's job is "provide the placeholder texture", not "compose a screen"). A field's name should reveal its role, not just its contents — a scratch collection is a `…Buffer`, a point-in-time copy a `…Snapshot`.

### 12. Booleans that conflate "absent" with a state; re-validating inside

`bool IsX` returning `false` for "the thing doesn't exist" is a lie — absence is not the negative state (a video that does not exist is neither muted nor unmuted). Prefer a method that takes the already-validated, non-null instance over a property that returns a default for the missing case and re-runs the caller's null/has checks.

```csharp
// WRONG — property conflates "no video" with "not muted", and re-checks
public bool IsCurrentVideoMuted => cvs.HasValue && cvs.Value.Track.Muted;

// RIGHT — caller validates once; method can't be called in an invalid state
private static bool IsMuted(in CurrentVideoStreamInfo info) => info.Track.Muted;
```

Make invalid states unrepresentable in the signature instead of defending against them inside.

### 13. Side-effects where a return value works; dependencies hidden behind default params

- If a function can return its result, return it — don't mutate shared state as a side channel.
- Don't give a real collaborator a `= null` default (`CreateForScene(..., AvatarPlaceHolderTextureSource? placeholder = null)`). Pass it explicitly at every call site — with a named argument (`placeholder: null`) when it is genuinely absent — so the wiring stays visible.

### 14. Caches / shared GPU resources without cost justification or an ownership contract

Weigh VRAM/heap against the work actually saved before adding a cache. A sub-millisecond per-frame recompute (one orthographic UI bake) often beats a per-key `RenderTexture` cache — 16 × 1024² BGRA32 = 64 MB of VRAM for a fallback graphic. And never hand out raw pooled/shared `RenderTexture` references without an ownership/lifetime contract: a caller that holds the reference past the frame you `Destroy()` it renders a destroyed texture (magenta/black). Prefer one reusable render target, recomposed only when its inputs change.

### 15. Scattered conditionals / string-typing instead of a domain type

Many `if/else` branches over a string prefix or a nullable "identity" field — or a condition like `x != null && x.StartsWith(SOME_PREFIX)` repeated across methods — mean a concept wants to be a **type**. Model it as an immutable domain object or a union (this repo uses [REnum](https://github.com/NickKhalow/REnum) for union types with exhaustive matching). The branching collapses and illegal states become unrepresentable.

Corollary — **don't handle cases that cannot happen.** If every `GateKeeperMode` maps to a valid URL by design, a `null` URL is not a branch to handle, it's a bug to surface (throw/assert). Defensive handling of impossible states hides the real defect and bloats the code. Where the type offers a sentinel (`Weak.Null`), prefer it over a nullable (`Weak<T>?`).

```csharp
// WRONG — the "mode" is a string prefix, re-tested in every method
if (currentVideoIdentity != null && currentVideoIdentity.StartsWith(PRESENTATION_BOT_PREFIX)) ...

// RIGHT — the mode is a case of the address type; matching is exhaustive
[REnumField(typeof(UserStream))]
[REnumField(typeof(PresentationBotStream))]
[REnumFieldEmpty("CurrentStream")]
public readonly partial struct LivekitAddress { }
```

### 16. Leaking or bypassing an existing abstraction

Use the abstraction the codebase already provides instead of reaching past it. Route protobuf messages through `IMessagePipe`, not `IDataPipe` directly — direct use leaks the abstraction and forces obscure `try/catch` hacks downstream to compensate. Before writing new resolution/parsing logic, find the owner and reuse it: URL building lives in `DecentralandUrlsSource` (it already handles ORG/ZONE/TODAY); participant display-name resolution already exists in the chat module.

### 17. Thread-safety left implicit on shared mutable state

If you add a mutable collection to a class touched from more than one thread, make the safety model explicit: either confine it (document `// Not thread-safe — main-thread only.` and keep all access on that thread) or use a `ConcurrentDictionary` / explicit lock — and ensure access to any nested list is thread-safe too. A non-thread-safe collection reachable from multiple threads with no stated constraint is a latent race.

### 18. Linear scans and redundant buffers in hot paths

Watch algorithmic complexity where a query runs per-frame or per-item:

- Replace O(n) membership checks (`List.Exists`, linear `foreach` search) with a `HashSet` / dictionary keyed by identity. A linear lookup *inside* a loop over all items is O(n²) — e.g. `HasAudioSourceForKey` scanning `audioSources` for every key.
- Drop intermediate buffers that exist only to be re-iterated immediately (process the source directly instead of filling `streamKeysBuffer` first).
- If you only need the first element, index `[0]`; don't `foreach`-and-break.

### 19. Dead code, redundant flags, dead stores, and duplicated logic

- Remove methods/fields that are never called (`IsVideoTrackMuted`) and booleans that merely cache a direct expression (`hasLiveAudio` when `IsAudioOpened` computes it inline).
- Remove assignments overwritten on every subsequent path (resetting `currentVideoIdentity` before both branches set it) and `if/else` arms that do the same thing.
- Extract a duplicated condition into one named helper, and **resolve/derive a value in exactly one place** — ideally one pure function marked `[Pure]`. Scattering URL resolution across `DecentralandUrlsSource` and `MainSceneLoader` means a change to one is silently missed by the other (a latent "two different sub-URLs depending on context" bug).

### 20. Hiding failures instead of surfacing them

- **Band-aid recovery:** a `try/catch` or "repair the corrupt X" branch added to make a crash disappear without understanding *what* corrupts the data. Find and fix the cause.
- **Stacked catches:** if a later handler already covers a case ("invalid data from participant"), an earlier duplicate catch is excessive — remove it.
- **Silent lookups:** `Assert.IsNotNull` on reflection / serialized-property-name lookups so a rename fails loudly at startup instead of silently doing nothing.

## PR Standards

- **Branches:** Based on `dev` branch
  - `feat/...` — new features
  - `fix/...` — bugfixes
  - `chore/...` — cleanup
  - `opti/...` — optimizations
- **PR naming:** Lowercase prefix (`feat:`, `fix:`, `chore:`, `opti:`)
- **PR description:** Generic description, technical description, QA test steps
- **PR approval:** QA review + developer review + passing builds/tests
- **Merge method:** Squash and merge
- **Commits:** Commit often as save points; PRs are squashed on merge
