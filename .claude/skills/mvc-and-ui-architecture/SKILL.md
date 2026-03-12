---
name: mvc-and-ui-architecture
description: "MVC UI architecture — controllers, views, window stacking, and shared space. Use when building UI controllers (ControllerBase), showing views via MVCManager, connecting UI to ECS via bridge systems, implementing context menus, settings panels, or coordinating panel visibility."
user-invocable: false
---

# MVC & UI Architecture

## Sources

- `docs/mvc.md` — MVC UI architecture pattern
- `docs/shared-space-manager.md` — Centralized UI panel visibility coordination
- `docs/generic-context-menu.md` — Dynamic context menu controller
- `docs/settings-panel.md` — Modular settings panel architecture

---

## Controller Pattern

### ControllerBase

All controllers inherit `ControllerBase<TView, TInputData>` or `ControllerBase<TView>`. Each controller is bound to a single view instance and created by plugins.

### View Instantiation

- **Lazy:** `CreateLazily` — View created on first show
- **Pre-warmed:** `Preallocate` — View created at startup

### Controller Lifecycle

1. `OnViewInstantiated()` — Subscribe to view events, set initial state
2. `OnFocus()` — Controller becomes active (resume rendering, refresh data)
3. `OnBlur()` — Controller loses focus (suspend rendering)
4. `OnBeforeViewShow()` — Right before view becomes visible
5. `OnViewShow()` — View is now visible
6. `OnViewClose()` — View is closing

### Showing Views

```csharp
// Show a controller with input data
await mvcManager.ShowAsync(MyController.IssueCommand(new MyInputData { Id = "123" }));

// Show and forget (fire-and-forget)
mvcManager.ShowAndForget(MyController.IssueCommand(MyInputData.Empty));
```

### Code Example — Controller

From `MinimapController.cs`:

```csharp
public partial class MinimapController : ControllerBase<MinimapView>, IMapActivityOwner
{
    private const MapLayer RENDER_LAYERS = MapLayer.SatelliteAtlas | MapLayer.PlayerMarker;

    private readonly IMapRenderer mapRenderer;
    private readonly IMVCManager mvcManager;
    // ... other dependencies ...

    public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

    public MinimapController(MinimapView minimapView, IMapRenderer mapRenderer,
        IMVCManager mvcManager, /* ... */)
        : base(() => minimapView)
    {
        this.mapRenderer = mapRenderer;
        this.mvcManager = mvcManager;
        // ...
    }

    protected override void OnViewInstantiated()
    {
        viewInstance!.expandMinimapButton.onClick.AddListener(ExpandMinimap);
        viewInstance.minimapRendererButton.Button.onClick.AddListener(() =>
            sharedSpaceManager.ShowAsync(PanelsSharingSpace.Explore,
                new ExplorePanelParameter(ExploreSections.Navmap)));
        // ...
    }

    protected override void OnBlur()
    {
        mapCameraController?.SuspendRendering();
    }

    protected override void OnFocus()
    {
        mapCameraController?.ResumeRendering();
        // Refresh data
        placesApiCts.SafeCancelAndDispose();
        placesApiCts = new CancellationTokenSource();
        RefreshPlaceInfoUIAsync(previousParcelPosition, placesApiCts.Token).Forget();
    }

    public override void Dispose()
    {
        placesApiCts.SafeCancelAndDispose();
        // Unsubscribe from all events
        viewInstance.favoriteButton.OnButtonClicked -= OnFavoriteButtonClicked;
        // ...
    }
}
```

## Window Stack Manager

Sorting layers control view ordering and behavior:

| Layer | Behavior |
|-------|----------|
| `Persistent` | Always behind all other windows. Blurred when fullscreen shows. |
| `Fullscreen` | Hides popups when shown. Blurs persistent layer. |
| `Popup` | Stackable. Focus/blur support. |
| `Overlay` | Above everything. |

## ECS-Controller Bridge

`ControllerECSBridgeSystem` connects ECS data to MVC controllers.

```csharp
// In controller — define query and hook system
[Query]
[All(typeof(PlayerComponent))]
[None(typeof(PBAvatarShape))]
private void QueryPlayerPosition(in CharacterTransform transformComponent)
{
    // Access ECS data from controller
    Vector3 position = transformComponent.Position;
    // ...
}

public void HookPlayerPositionTrackingSystem(TrackPlayerPositionSystem system) =>
    AddModule(new BridgeSystemBinding<TrackPlayerPositionSystem>(
        this, QueryPlayerPositionQuery, system));

// Separate system class
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class TrackPlayerPositionSystem : ControllerECSBridgeSystem
{
    internal TrackPlayerPositionSystem(World world) : base(world) { }
}
```

## Shared Space Manager

Coordinates visibility of competing UI panels (Friends, Chat, Notifications) without panels knowing about each other.

- Panels implement `IPanelInSharedSpace` or `IControllerInSharedSpace`
- `ISharedSpaceManager.ShowAsync` hides competing panels, waits for animation, then shows the requested panel
- Panels should default to PopUp layer
- Overlay panels are not registered with SharedSpaceManager

**Panel requirements:**
- Raise `ViewShowingComplete` event
- Implement `OnHiddenInSharedSpaceAsync`, `OnShownInSharedSpaceAsync`, `IsVisibleInSharedSpace`

## Generic Context Menu

Dynamic, component-based context menu shown via MVC:

```csharp
// Build context menu
var contextMenu = new GenericContextMenu()
    .AddControl(new ToggleContextMenuControlSettings("Set as Home", SetAsHomeToggled))
    .AddControl(new SeparatorContextMenuControlSettings())
    .AddControl(new ButtonContextMenuControlSettings("Copy Link", copyLinkSprite, CopyJumpInLink))
    .AddControl(new ButtonContextMenuControlSettings("Reload Scene", reloadSprite, ReloadScene));

// Show it
mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
    new GenericContextMenuParameter(contextMenu, anchorPosition)))
    .Forget();
```

**Available components:**
- `ButtonContextMenuControlSettings` — Button with text + icon
- `ToggleContextMenuControlSettings` — Toggle with text
- `SeparatorContextMenuControlSettings` — Visual separator
- User profile info, toggle with checkmark, sub-menu button, scrollable button list

**Adding new components:**
1. Create `IContextMenuControlSettings` class
2. Create view inheriting `GenericContextMenuComponentBase`
3. Handle pool manager (`CreateObjectPool`, `GetContextMenuComponent`)
4. Create prefab, extend `GenericContextMenuPlugin`

## Settings Panel

Modular architecture for settings UI:

**Module types:**
- Toggle: `ToggleModuleBinding`
- Slider: `SliderModuleBinding` (Numeric / Percentage / Time / Custom)
- Dropdown: `DropdownModuleBinding` (single / multi-select)

**4 sections:** Graphics, Sound, Controls, Chat

**Adding new settings:**
1. Create module view (inherit appropriate base)
2. Create controller (inherit `SettingsFeatureController`)
3. Add to module binding enum (at end to not break ScriptableObject serialization)
4. Configure in `SettingsMenuConfiguration` ScriptableObject asset

**Feature flag integration:** `FeatureFlagName` on `SettingsGroup` auto-checks `FeatureFlagsConfiguration.Instance.IsEnabled()`.

## Subordinate Controllers

No strict architecture. Nested views are referenced by the main view. Sub-controllers are created by main controllers. Keep subordinate logic simple — complex subordinates should become standalone controllers.
