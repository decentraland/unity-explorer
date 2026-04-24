---
name: code-standards
description: "C# naming conventions, member ordering, formatting rules, nullable reference types, memory/GC rules, test patterns, and PR standards. Use when writing, reviewing, or modifying non-trivial C# changes in this Unity project — applies to ECS systems, controllers, tests, utilities, and plugins."
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

- Minimize GC pressure — reuse objects, use object pooling (`Utility/Pool`, `Utility/ThreadSafePool`)
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
