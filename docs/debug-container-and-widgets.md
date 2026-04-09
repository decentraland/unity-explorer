# Debug Container and Widgets Architecture

> Runtime debug panel system built on Unity UIElements. Plugins and systems declare widgets at startup; bindings push live data to the UI each frame.

---

## Overview

The **DebugContainer** is a collapsible panel (toggled via a small button or hotkey) that hosts **Debug Widgets** — foldable sections of labeled controls. Each widget belongs to a named **category** (e.g. `PERFORMANCE`, `MEMORY`, `PARTICLES`) and contains rows of **elements** (labels, markers, buttons, toggles, sliders, fields, lists).

Data flows one way at runtime: systems write to **`ElementBinding<T>`** objects and the UI reflects the change. Interactive elements (buttons, toggles, fields) fire callbacks back into the system.

---

## File Structure

```
Assets/DCL/PerformanceAndDiagnostics/DebugUtilities/
├── Builders/
│   ├── IDebugContainerBuilder.cs      # Interface + Categories + WidgetName struct
│   ├── DebugContainerBuilder.cs       # Concrete implementation
│   ├── DebugWidgetBuilder.cs          # Fluent widget composition API
│   ├── BuilderExtensions.cs           # Convenience shortcuts (AddCustomMarker, AddToggleField, etc.)
│   ├── NullDebugContainerBuilder.cs   # No-op for builds without debug
│   └── FactoryMethod.cs              # IDebugElementFactory interfaces
├── UIBindings/
│   ├── IElementBinding.cs            # Binding interface
│   ├── ElementBinding.cs             # Two-way data binding
│   ├── PersistentElementBinding.cs   # Auto-persisted wrapper
│   ├── IndexedElementBinding.cs      # Dropdown index tracking
│   ├── EnumElementBinding.cs         # Enum dropdown helper
│   └── DebugWidgetVisibilityBinding.cs  # Widget expand/collapse + visibility
├── Views/
│   ├── DebugContainer.cs             # Root panel (UxmlElement)
│   ├── DebugWidget.cs                # Foldable widget section
│   ├── DebugControl.cs               # Two-column row layout
│   ├── DebugList.cs                  # Dynamic key-value list
│   ├── DebugElementBase.cs           # Abstract element + generic Factory
│   ├── <ElementType>Element.cs       # One per element kind (Button, Toggle, etc.)
│   └── Assets/
│       ├── DebugContainer.uxml       # Panel UXML layout
│       ├── DebugWidget.uxml          # Widget foldout UXML
│       ├── DebugControl.uxml         # Row UXML
│       └── DebugUtilitiesStyle.uss   # Shared styles
├── Declarations/
│   ├── IDebugElementDef.cs           # Marker interface for definitions
│   └── Debug<Type>Def.cs             # One per element kind
└── Formatter/
    └── BytesFormatter.cs             # Human-readable data-size formatting
```

---

## Core Concepts

### Categories

Every widget is filed under a **category** — a `WidgetName` constant declared in `IDebugContainerBuilder.Categories`. This enforces naming discipline and prevents typo-based widget duplication.

```csharp
// IDebugContainerBuilder.cs
public static class Categories
{
    public static readonly WidgetName PERFORMANCE = "Performance".AsWidgetName();
    public static readonly WidgetName MEMORY      = "Memory".AsWidgetName();
    public static readonly WidgetName PARTICLES   = "Particles".AsWidgetName();
    // ... ~30 categories total
}
```

Add new categories here when introducing a new debug section.

### WidgetName

A thin `readonly struct` wrapper around `string` that prevents accidental raw-string mismatches. Created via `"Name".AsWidgetName()` (internal extension).

### ElementBinding\<T\>

The core data-flow primitive. A binding holds a cached `tempValue` and a `tempValueIsDirty` flag. Setting `.Value` marks the cache dirty; the next `Update()` call propagates to the connected UI element. The connection is established automatically during `BuildWithFlex`.

Key members:

| Member | Purpose |
|--------|---------|
| `.Value` (get/set) | Read cached value or stage a new one |
| `.SetAndUpdate(T)` | Set + immediately flush to UI (convenience) |
| `.Connect(INotifyValueChanged<T>)` | Wired internally during Build |
| `.Release()` | Unregister callbacks (cleanup) |
| `event OnValueChanged` | Fires when the UI element changes (user interaction) |

### DebugWidgetVisibilityBinding

Controls whether a widget's foldout is expanded and whether the widget is visible at all. Provides `IsExpanded` and `IsConnectedAndExpanded` for **performance guards** — systems skip expensive string formatting when the widget is collapsed or the debug panel is closed.

```csharp
if (visibilityBinding.IsExpanded)
{
    // Only format strings and update bindings when the user can actually see them
    binding.Value = $"<color=red>{value}</color>";
}
```

---

## Lifecycle

### 1. Registration (startup)

Plugins and systems receive `IDebugContainerBuilder` via constructor injection. During initialization, they call `TryAddWidget` to declare widgets and compose their content:

```csharp
var binding = new ElementBinding<string>(string.Empty);
var visibility = new DebugWidgetVisibilityBinding(true);

debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.MY_CATEGORY)
    ?.SetVisibilityBinding(visibility)
    .AddCustomMarker("Label:", binding);
```

At this point no UI exists — `DebugWidgetBuilder` accumulates **placement records** (element definitions + layout metadata).

### 2. Build (UI creation)

An external system calls `BuildWithFlex(UIDocument)`. The builder:

1. Finds the `DebugContainer` element in the UIDocument.
2. For each queued widget, calls `DebugWidgetBuilder.Build(...)`.
3. Build instantiates `DebugWidget` UXML, creates `DebugControl` rows, instantiates element views via `IDebugElementFactory`, and calls `ConnectBindings()` on each element.
4. Adds all widgets to `DebugContainer.containerRoot`.

After this point, bindings are "live" — setting `.Value` updates the UI on the next `Update()`.

### 3. Runtime (frame loop)

Systems update bindings in their `Update()` method:

```csharp
protected override void Update(float t)
{
    // ... compute values ...

    if (visibilityBinding.IsExpanded)
    {
        string color = value >= threshold ? "red" : "green";
        binding.Value = $"<color={color}>{value} / {threshold}</color>";
    }
}
```

### 4. Teardown

Bindings are lightweight and do not require explicit disposal in most cases. Systems that hold references to bindings will be garbage-collected with the world. If explicit cleanup is needed, call `binding.Release()`.

---

## Widget Builder API

`DebugWidgetBuilder` provides a **fluent API** for composing widget contents. All methods return `this` for chaining.

### Structural Methods

| Method | Description |
|--------|-------------|
| `SetVisibilityBinding(DebugWidgetVisibilityBinding)` | Attach visibility control |
| `AddControl(IDebugElementDef? left, IDebugElementDef? right, DebugHintDef? hint)` | Raw two-column row |
| `AddControlWithLabel(string label, IDebugElementDef? right, DebugHintDef? hint)` | Label on left, element on right |
| `AddGroup(string name, params (left, right)[] elements)` | Nested sub-widget |
| `AddList(string name, IElementBinding<IReadOnlyList<(string, string)>>)` | Dynamic key-value list |

### Extension Shortcuts (BuilderExtensions.cs)

| Method | Left Column | Right Column |
|--------|-------------|--------------|
| `AddCustomMarker(string label, ElementBinding<string>)` | Static label | Dynamic text (supports rich text) |
| `AddCustomMarker(ElementBinding<string>)` | Dynamic text (full width) | — |
| `AddMarker(string label, ElementBinding<ulong>, Unit)` | Static label | Formatted numeric (time/bytes/bits) |
| `AddSingleButton(string text, Action)` | Button (full width) | — |
| `AddSingleButton(ElementBinding<string>, Action)` | Button with dynamic text | — |
| `AddToggleField(string label, EventCallback, bool)` | Static label | Toggle checkbox |
| `AddIntFieldWithConfirmation(int, string, Action<int>)` | Int field | Confirm button |
| `AddStringFieldWithConfirmation(string, string, Action<string>)` | Text field | Confirm button |
| `AddStringFieldsWithConfirmation(int, string, Action<string[]>)` | Multiple text fields | Confirm button |
| `AddFloatField(string label, ElementBinding<float>)` | Static label | Float field |
| `AddIntSliderField(string, ElementBinding<int>, min, max)` | Static label | Int slider |
| `AddFloatSliderField(string, ElementBinding<float>, min, max)` | Static label | Float slider |

### Rich Text in Custom Markers

`AddCustomMarker` with `ElementBinding<string>` supports Unity rich text tags. The established pattern for colored status indicators:

```csharp
string color = isOverBudget ? "red" : "green";
binding.Value = $"<color={color}>{current} / {max}</color>";
```

---

## Element Definitions

Each UI element type has a corresponding **definition class** (`IDebugElementDef` implementation) that describes its configuration. The factory system maps definitions to view elements:

| Definition | View Element | Binding Type |
|-----------|-------------|-------------|
| `DebugConstLabelDef` | Static label | None |
| `DebugSetOnlyLabelDef` | Dynamic label | `ElementBinding<string>` |
| `DebugButtonDef` | Clickable button | `ElementBinding<string>` + `Action` |
| `DebugToggleDef` | Checkbox | `IElementBinding<bool>` |
| `DebugIntFieldDef` | Integer input | `ElementBinding<int>` |
| `DebugFloatFieldDef` | Float input | `ElementBinding<float>` |
| `DebugIntSliderDef` | Integer slider | `ElementBinding<int>` + min/max |
| `DebugFloatSliderDef` | Float slider | `ElementBinding<float>` + min/max |
| `DebugTextFieldDef` | Text input | `ElementBinding<string>` |
| `DebugVector2IntFieldDef` | Vector2Int input | `ElementBinding<Vector2Int>` |
| `DebugDropdownDef` | Selection dropdown | `IndexedElementBinding` |
| `DebugLongMarkerDef` | Formatted numeric | `ElementBinding<ulong>` + Unit |
| `DebugHintDef` | Info/warning/error hint | `string` or `ElementBinding<string>` |
| `AverageFpsBannerDef` | Color-coded FPS bar | `ElementBinding<AverageFpsBannerData>` |

---

## Integration Patterns

### Pattern A: System Creates Widget Directly

The system receives `IDebugContainerBuilder` via its constructor (source-generated `InjectToWorld`). Widget creation happens in the constructor.

**Used by:** `DebugViewProfilingSystem`, `DebugAnalyticsSystem`, `DebugGPUInstancingSystem`, `DebugRoomsSystem`

```csharp
public partial class MyDebugSystem : BaseUnityLoopSystem
{
    private readonly ElementBinding<string> statusBinding;
    private readonly DebugWidgetVisibilityBinding visibilityBinding;

    internal MyDebugSystem(World world, IDebugContainerBuilder debugBuilder) : base(world)
    {
        statusBinding = new ElementBinding<string>(string.Empty);
        visibilityBinding = new DebugWidgetVisibilityBinding(true);

        debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.MY_WIDGET)
            ?.SetVisibilityBinding(visibilityBinding)
            .AddCustomMarker("Status:", statusBinding);
    }

    protected override void Update(float t)
    {
        if (visibilityBinding.IsExpanded)
            statusBinding.Value = "running";
    }
}
```

### Pattern B: Plugin Creates Bindings, System Consumes

The plugin creates bindings + widget during `InitializeAsync`, then passes the bindings to the system via `InjectToWorld`. This is used when the system doesn't need direct access to `IDebugContainerBuilder`.

**Used by:** `ParticleSystemPlugin` + `ParticleSystemBudgetSystem`

```csharp
// Plugin
public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
{
    countBinding = new ElementBinding<string>(string.Empty);
    visibility = new DebugWidgetVisibilityBinding(true);

    debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.MY_WIDGET)
        ?.SetVisibilityBinding(visibility)
        .AddCustomMarker("Count:", countBinding);
}

public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, ...)
{
    MySystem.InjectToWorld(ref builder, countBinding!, visibility!);
}

// System
internal MySystem(World world, ElementBinding<string> countBinding,
    DebugWidgetVisibilityBinding visibilityBinding) : base(world)
{
    this.countBinding = countBinding;
    this.visibilityBinding = visibilityBinding;
}
```

### Pattern C: Non-System Helper Class

A helper class (not a system) builds and updates a widget. Used for complex multi-widget scenarios like rooms.

**Used by:** `DebugWidgetRoomDisplay`, `DebugWidgetGateKeeperRoomDisplay`

---

## How to Pass IDebugContainerBuilder

`IDebugContainerBuilder` is a **global resource** created in `StaticContainer`. It does not flow through `InjectToWorld`'s shared dependencies.

### To a System (Pattern A)

Add `IDebugContainerBuilder` as a constructor parameter. The source generator includes it in the generated `InjectToWorld` signature. The plugin must receive and forward it.

### To a Plugin

Pass `container.DebugContainerBuilder` in `StaticContainer.ECSWorldPlugins` when instantiating the plugin:

```csharp
// StaticContainer.cs, in ECSWorldPlugins array
new MyPlugin(..., container.DebugContainerBuilder),
```

---

## Performance Guidelines

1. **Always use visibility guards.** Wrap binding updates in `if (visibilityBinding.IsExpanded)` to skip string formatting when the widget is collapsed.
2. **Prefer `IsExpanded` over `IsConnectedAndExpanded`** unless the binding may not be connected (e.g. widget creation was conditional).
3. **Avoid allocations.** Use `$"<color=...>` interpolation sparingly — it allocates. For high-frequency updates, consider caching the formatted string when the underlying value hasn't changed.
4. **`TryAddWidget` returns null** when debug is disabled (`NullDebugContainerBuilder`). Always use `?.` null-conditional chaining.

---

## Adding a New Debug Widget (Checklist)

1. **Add category** in `IDebugContainerBuilder.Categories` if none fits.
2. **Choose integration pattern** (A: system-direct, B: plugin-creates-bindings, C: helper class).
3. **Create bindings** as fields: `ElementBinding<T>` for data, `DebugWidgetVisibilityBinding` for visibility.
4. **Build widget** using the fluent API with `TryAddWidget(category)?.SetVisibilityBinding(...).Add*(...)`.
5. **Wire DI** — ensure `IDebugContainerBuilder` reaches your plugin/system via `StaticContainer`.
6. **Update bindings** in `Update()` behind a `visibilityBinding.IsExpanded` guard.
7. **No cleanup needed** in most cases — bindings are GC'd with the world.

---

## Assembly References

The debug utilities live in the `DebugUtilities` assembly (`GUID:4725c02394ab4ce19f889e4e8001f989`). The `DCL.Plugins` assembly already references it, so any system included via `.asmref` pointing to `DCL.Plugins` can use `DCL.DebugUtilities` and `DCL.DebugUtilities.UIBindings` namespaces without additional asmdef changes.

---

## Reference Files

| Purpose | Path |
|---------|------|
| Interface + Categories | `Assets/DCL/PerformanceAndDiagnostics/DebugUtilities/Builders/IDebugContainerBuilder.cs` |
| Builder implementation | `Assets/DCL/PerformanceAndDiagnostics/DebugUtilities/Builders/DebugContainerBuilder.cs` |
| Widget builder (fluent API) | `Assets/DCL/PerformanceAndDiagnostics/DebugUtilities/Builders/DebugWidgetBuilder.cs` |
| Extension shortcuts | `Assets/DCL/PerformanceAndDiagnostics/DebugUtilities/Builders/BuilderExtensions.cs` |
| ElementBinding | `Assets/DCL/PerformanceAndDiagnostics/DebugUtilities/UIBindings/ElementBinding.cs` |
| Visibility binding | `Assets/DCL/PerformanceAndDiagnostics/DebugUtilities/UIBindings/DebugWidgetVisibilityBinding.cs` |
| Profiling system (Pattern A) | `Assets/DCL/PerformanceAndDiagnostics/Profiling/ECS/DebugViewProfilingSystem.cs` |
| ParticleSystem plugin (Pattern B) | `Assets/DCL/PluginSystem/World/ParticleSystemPlugin.cs` |
| Room display (Pattern C) | `Assets/DCL/Multiplayer/Connections/Systems/Debug/DebugWidgetRoomDisplay.cs` |
| StaticContainer (DI root) | `Assets/DCL/Infrastructure/Global/StaticContainer.cs` |
