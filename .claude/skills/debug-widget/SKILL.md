---
name: debug-widget
description: "DebugContainer debug widget implementation and updates. Use when adding a new debug widget to the debug panel, creating or modifying debug categories, wiring ElementBinding or DebugWidgetVisibilityBinding, using IDebugContainerBuilder, calling TryAddWidget or AddCustomMarker, displaying live runtime data in the debug panel, or adding colored status indicators to the profiling/debug UI. Also applies when passing IDebugContainerBuilder through StaticContainer to a plugin or system."
user-invocable: false
---

# Debug Container Widget Implementation

## Sources

- `docs/debug-container-and-widgets.md` — Full architecture reference (file structure, lifecycle, all element types, builder API)

Read the doc above for detailed type inventories and UXML structure. This skill focuses on the implementation workflow and patterns you need to get a widget working.

---

## Quick Reference: Key Files

| Purpose | Path |
|---------|------|
| Categories + interface | `Assets/DCL/PerformanceAndDiagnostics/DebugUtilities/Builders/IDebugContainerBuilder.cs` |
| Fluent builder API | `Assets/DCL/PerformanceAndDiagnostics/DebugUtilities/Builders/DebugWidgetBuilder.cs` |
| Convenience shortcuts | `Assets/DCL/PerformanceAndDiagnostics/DebugUtilities/Builders/BuilderExtensions.cs` |
| ElementBinding\<T\> | `Assets/DCL/PerformanceAndDiagnostics/DebugUtilities/UIBindings/ElementBinding.cs` |
| Visibility binding | `Assets/DCL/PerformanceAndDiagnostics/DebugUtilities/UIBindings/DebugWidgetVisibilityBinding.cs` |
| Null builder (no-op) | `Assets/DCL/PerformanceAndDiagnostics/DebugUtilities/Builders/NullDebugContainerBuilder.cs` |
| StaticContainer (DI root) | `Assets/DCL/Infrastructure/Global/StaticContainer.cs` |

---

## Implementation Checklist

### Step 1: Add a Category (if needed)

Check if an existing category in `IDebugContainerBuilder.Categories` fits. If not, add one to the `Categories` static class inside `IDebugContainerBuilder`:

```csharp
// IDebugContainerBuilder.cs → public static class Categories { ... }
public static readonly WidgetName MY_WIDGET = "My Widget".AsWidgetName();
```

The type **must** be `WidgetName`, not `string` or `const string`. Every existing category uses this exact pattern — `public static readonly WidgetName`. The `AsWidgetName()` extension method (defined in the same file) wraps the string in the type-safe `WidgetName` struct. Using `const string` or plain `string` will not match the `GetOrAddWidget(WidgetName)` overload and breaks the convention used by all other categories in the class.

### Step 2: Choose an Integration Pattern

There are three patterns. Pick the one that matches your situation:

**Pattern A — System creates widget directly.**
The system receives `IDebugContainerBuilder` as a constructor parameter (source-generated into `InjectToWorld`). Widget creation happens in the constructor. Best when the system owns both the data and the display.

**Pattern B — Plugin creates bindings, system consumes.**
The plugin creates `ElementBinding<T>` + `DebugWidgetVisibilityBinding` in `InitializeAsync`, builds the widget, then passes the bindings to the system via `InjectToWorld`. Best when the plugin already manages the lifecycle and the system just writes values.

**Pattern C — Non-system helper class.**
A standalone class builds and updates the widget. Best for complex multi-widget scenarios (e.g. multiple room widgets).

### Step 3: Create Bindings and Build the Widget

Regardless of pattern, the widget construction looks the same:

```csharp
// Create bindings as fields
private readonly ElementBinding<string> myBinding = new (string.Empty);
private readonly DebugWidgetVisibilityBinding myVisibility = new (true);

// Build widget (in constructor or InitializeAsync)
debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.MY_WIDGET)
    ?.SetVisibilityBinding(myVisibility)
    .AddCustomMarker("Label:", myBinding);
```

`TryAddWidget` returns `null` when debug is disabled (`NullDebugContainerBuilder`), so the `?.` chain is essential — never remove it.

#### Available Builder Methods

Read `BuilderExtensions.cs` for the full list. The most common:

| Method | Use Case |
|--------|----------|
| `AddCustomMarker(string label, ElementBinding<string>)` | Live text with optional rich text color tags |
| `AddMarker(string label, ElementBinding<ulong>, Unit)` | Formatted numeric (time ns, bytes, bits) |
| `AddToggleField(string label, EventCallback, bool)` | Toggle a runtime setting |
| `AddSingleButton(string text, Action)` | Trigger an action |
| `AddFloatSliderField(string, ElementBinding<float>, min, max)` | Tune a float parameter |
| `AddIntSliderField(string, ElementBinding<int>, min, max)` | Tune an int parameter |
| `AddControlWithLabel(string, IDebugElementDef?, DebugHintDef?)` | Custom left-label + right-element row |

For the complete element definition types, check the `Declarations/` folder — one `Debug*Def.cs` per element kind.

### Step 4: Wire IDebugContainerBuilder Through DI

`IDebugContainerBuilder` is a global resource on `StaticContainer`. It does **not** flow through `InjectToWorld`'s shared dependencies. You must pass it explicitly:

1. **Add it to your plugin's constructor:**
   ```csharp
   public MyPlugin(..., IDebugContainerBuilder debugBuilder)
   {
       this.debugBuilder = debugBuilder;
   }
   ```

2. **Pass it in StaticContainer where the plugin is instantiated** (search for your plugin name in `StaticContainer.cs`, usually in the `ECSWorldPlugins` array):
   ```csharp
   new MyPlugin(..., container.DebugContainerBuilder),
   ```

If using Pattern A (system creates widget), add `IDebugContainerBuilder` as a constructor parameter on the system. The source generator will include it in the generated `InjectToWorld` method. Then pass it from the plugin's `InjectToWorld`:
```csharp
MyDebugSystem.InjectToWorld(ref builder, debugBuilder);
```

### Step 5: Update Bindings at Runtime

In your system's `Update()`, write values to bindings behind a visibility guard:

```csharp
protected override void Update(float t)
{
    // ... compute your values ...

    if (visibilityBinding.IsExpanded)
    {
        string color = currentValue >= threshold ? "red" : "green";
        myBinding.Value = $"<color={color}>{currentValue} / {threshold}</color>";
    }
}
```

The visibility guard skips string formatting when the widget is collapsed or the debug panel is closed. This matters because `Update()` runs every frame across multiple worlds.

### Step 6: Required Usings

```csharp
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
```

Both namespaces live in the `DebugUtilities` assembly (`GUID:4725c02394ab4ce19f889e4e8001f989`). The `DCL.Plugins` assembly already references it, so any code compiled into that assembly (including `.asmref` files pointing to `DCL.Plugins`) can use these without asmdef changes. If your system lives in a different assembly, add the GUID to that assembly's references.

---

## Performance Rules

1. **Always guard with `visibilityBinding.IsExpanded`** before updating bindings. String interpolation and `$"<color=...>"` formatting allocate — skip them when nobody is looking.
2. **Use `IsExpanded`** (not `IsConnectedAndExpanded`) unless the widget creation was conditional and the binding might not be connected.
3. **No LINQ** in Update — standard ECS performance constraint applies here too.
4. **Binding.Value set is lazy** — it marks a dirty flag and the UI picks it up on the next UIElements binding pass. No need to call `.SetAndUpdate()` from a system's `Update()` — that's for one-shot updates outside the frame loop.

---

## Colored Status Indicators

The established pattern for budget/threshold indicators uses Unity rich text:

```csharp
// Two-state: green/red
string color = value >= limit ? "red" : "green";
binding.Value = $"<color={color}>{value} / {limit}</color>";

// Three-state: green/yellow/red (see DebugViewProfilingSystem for FPS example)
string color = value >= high ? "green" : value >= low ? "yellow" : "red";
```

This works because `AddCustomMarker` renders `ElementBinding<string>` through a label that supports rich text tags.

---

## Reference Implementations

When in doubt, read these existing implementations:

| Pattern | File | What it shows |
|---------|------|---------------|
| A (system-direct) | `PerformanceAndDiagnostics/Profiling/ECS/DebugViewProfilingSystem.cs` | Multiple widgets, visibility guards, colored markers, sliders, toggles |
| A (system-direct) | `Rendering/GPUInstancing/Systems/DebugGPUInstancingSystem.cs` | Toggle + slider, settings sync, cleanup on dispose |
| B (plugin-creates) | `PluginSystem/World/ParticleSystemPlugin.cs` + `SDKComponents/ParticleSystem/Systems/ParticleSystemBudgetSystem.cs` | Plugin creates bindings in InitializeAsync, system writes them |
| C (helper class) | `Multiplayer/Connections/Systems/Debug/DebugWidgetRoomDisplay.cs` | Non-system class, decorator pattern, multiple widget instances |
