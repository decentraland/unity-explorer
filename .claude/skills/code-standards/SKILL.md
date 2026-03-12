---
name: code-standards
description: "C# naming conventions, member ordering, formatting rules, memory/GC rules, test patterns, and PR standards. Use when writing, reviewing, or modifying non-trivial C# changes in this Unity project — applies to ECS systems, controllers, tests, utilities, and plugins."
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
