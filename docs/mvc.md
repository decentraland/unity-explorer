# MVC

We use a pretty traditional approach for handling views:

## Controller

All `Controllers` should inherit `ControllerBase<TView, TInputData>` (if they accept input parameters) or `ControllerBase<TView>` (if they don't require them).

### View Instantiation

`Controller` is bound to the view instance: it's not shared between multiple views.

```csharp
        public delegate TView ViewFactoryMethod();

        protected ControllerBase(ViewFactoryMethod viewFactory)
        {
            this.viewFactory = viewFactory;
            State = ControllerState.ViewHidden;
        }
```

By default, there are two shortcuts to produce `TView` from the prefab:
- Lazily

```csharp
        public static ViewFactoryMethod CreateLazily<TViewMono>(TViewMono prefab, Transform root) where TViewMono: MonoBehaviour, TView =>
            () => Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, root);
```

- Pre-warmed

```csharp
        public static ViewFactoryMethod Preallocate<TViewMono>(TViewMono prefab, Transform root, out TViewMono instance) where TViewMono: MonoBehaviour, TView
        {
            TViewMono instance2 = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, root);
            instance = instance2;
            instance2.HideAsync(CancellationToken.None, true).Forget();
            return () => instance2;
        }
```

### Responsibilities of the Controller

Controllers should hold all the logic related to their own view and create any underlying/nested controllers (more in following chapters).
If a controller needs to access properties of its view it should be done with an override of `OnViewInstantiated`, this ensures that the view is correctly instantiated to avoid null refs.
The main controllers should be created by Plugins in order to ensure a correct flow of data and accessibility to functionalities.


### Blur, Focus, ...

Based on the implementation need a variety of default methods can be used in the controllers, the methods call order might vary based on the UI type:
* OnFocus is called when the UI is shown after being blurred
* OnBlur is called when a different UI is hiding the one handled by the controller
* OnBeforeViewShow is called right before the async Show of a UI
* OnViewShow is called after finishing the async Show (so after show animation is completed if any exist)
* OnViewClose is called after finishing the async Hide (so after hide animation is completed if any exist)

### Connection with ECS

Generally speaking the UI architecture is agnostic from the ECS world, but there exist some scenarios in which we need to access ECS data or even write something in the ECS world.

Accessing ECS data in Main Controllers:

Plugins that create main controllers can also inject Systems to the ECS World, we'll now see a practical example.

One example of ECS data need is the minimap, there we need to move the camera based on the player position in the world.
In order to do this we will need to create a system in the `MinimapController`, add it to the SystemBinding structure and create the needed query.
System definition:
```csharp
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrackPlayerPositionSystem : ControllerECSBridgeSystem
    {
        internal TrackPlayerPositionSystem(World world) : base(world) { }
    }
```
In the constructor of the controller:
```csharp
SystemBinding = AddModule(new BridgeSystemBinding<TrackPlayerPositionSystem>(this, QueryPlayerPositionQuery));
```
And the query:
```csharp
[All(typeof(PlayerComponent))]
        [Query]
        private void QueryPlayerPosition(in TransformComponent transformComponent)
        {
                //Needed logic
        }
```

In order to inject this system in the world from the MinimapPlugin we will also need to create the inject function (called for every plugin)
```csharp
        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var system = TrackPlayerPositionSystem.InjectToWorld(ref builder);
            minimapController.SystemBinding.InjectSystem(system);
        }
```

Accessing ECS data in Subordinate Controllers:

Having a more flexible approach for subordinate controllers, we cannot have the same structure for them to ECS Data.
As an example `PlayerMarkerController` needs as well the position and rotation of the player in order to show it in the Navmap, to do so we will need again to define the system in the controller, to inject it into the world and to manually pass it to the lowest controller a ref to the world builder.

## View

### Responsibilities of the View

Views should implement ViewBase and IView.
Views should not contain any business logic, views should hold only reference to the various UI elements in the form of serialised properties like:
```csharp
        [field: SerializeField]
        internal Button exampleButton { get; private set; }
```
and eventually some minor functions to handle visual stuff.
Views can override the ViewBase ShowAsync and HideAsync methods if there is a need for custom show/hide animations.

## Model

Controllers can freely access data from all present caches and introduce local data storage needed for their own needs in an encapsulated way.

In general, there are no strict concepts of how and what data can be accessed and transformed, but the following guidelines should be respected:
- `InputData` contains a small tip of information (such as `Id`) from which the required `Model` can be retrieved
- If the whole model needed is already retrieved on the caller side it's valid to pass it as `InputData`. But if such a model is needed for the view only it should be retrieved in the `Controller`
- The `Model` needed for a corresponding view can be retrieved asynchronously, e.g. from an end-point. `OnBeforeViewShow()` and `OnViewShow()` can be changed to `async` methods or an independent async process can be started (by `UniTaskVoid...Forget()`)
- We should avoid data permutation from one type to another: it's confusing and leads to extra boiler-plate and CPU cycles
- Ensure that data shared between multiple domains is propagated to consumers in the form of `read-only` contracts or `immutable` structures
- It's allowed to access data from `ECS` as it's described in [Connection with ECS](#connection-with-ecs)

For example, when a player passport is integrated the show call will request a model holding the needed player data to be displayed.

## MVC Manager

`MVCManager` is a storage of controllers and an entry for showing a particular view.

### Windows Stack Manager

`WindowStackManager` provides capabilities for maintaining stacks for different view types. It's subordinate to `MVCManager` and should not be used outside of it.

### Sorting Layer

The sorting layer defines how the corresponding view behaves in the Windows Stack. For each type, there is a different unique logic under the hood:
- `Persistent` views are always rendered behind everything else and once shown can't be hidden. They are rendered in the same order and should not overlap.
  - `Minimap` is a good example of a `Persistent` View.
- `Fullscreen` views are rendered in front of the `Persistent` layer.
  - When a `Fullscreen` view is pushed into the stack all `Popups` get hidden and all `Persistent` views get `Blurred`.
  - When a `Fullscreen` view closes all `Persistent` views receive `Focus`
- `Popup`
  - There could be several `Popups` in the stack. The next popup pushed is drawn above the previous one
  - `Popup` receives a `Blur` signal when it gets obscured by a new `Popup`; and `Focus` when it becomes the highest in the hierarchy
  - There is a special `IPopupCloserView` that exists in a single copy and provides the capability of closing the top-most pop-up by clicking on the background
- `Overlay` views are always drawn above every other view.
  - When the `Overlay` is pushed it hides `Fullscreen` and `Popup` views
  - When the `Overlay` is popped no automatic recovery for hidden views is performed

### How to Show a View

To show a view a respective `ShowCommand<TView, TInputData>` should be passed into the `UniTask ShowAsync<TView, TInputData>(ShowCommand<TView, TInputData> command)` method.

`Controller` is bound with a view type and input arguments' type one to one: thus, to prevent argument mismatch `IssueCommand(TInputData inputData)` from the typed `Controller` should be used, e.g.:

```csharp
mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Navmap))).Forget()
```

## Subordinate Controllers

The structure previously explained handles the generic UIs, like the minimap, the explore panel, any popup, etc.
In situations like nested controllers there isn't a strict architecture to follow, but it is recommended to follow some guidelines.

* Views in nested objects of the main view hierarchy should be referenced by the main view
* Controllers related to those sub views should be created by the main controllers
* Views should be logic-less
