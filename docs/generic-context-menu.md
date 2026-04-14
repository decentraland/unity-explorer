# Generic Context Menu

The Generic Context Menu is an MVC controller designed to manage all instances of context menus. Its primary objective is to provide a unified mechanism for rendering context menus across Decentraland. This ensures visual consistency and allows the menus to respond dynamically to real-time values, such as feature flags, without requiring the creation of new prefabs for each instance.

## Invocation

Like other MVC controllers, it is invoked by issuing a command through the `mvcManager`:

```csharp
mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter()))
```

### Minimal Parameters

The minimal parameters required are:

1. A reference to the configuration class (`GenericContextMenu`).
1. A `Vector2` specifying the anchor position of the context menu.

### Optional Parameters

Additional optional parameters include:

1. A `Rect` that defines the usable space for the context menu (default is the full viewport). This parameter ensures that the menu's position avoids overlapping with the defined rectangle.
1. An action triggered when the context menu is shown.
1. An action triggered when the context menu is closed.
1. A task that, upon completion, closes the context menu (clicking outside the menu also closes it).

## Available Components

Currently, the following basic components are available:

1. Button with text and icon (`ButtonContextMenuControlSettings`)
1. Button with text (`SimpleButtonContextMenuControlSettings`)
1. Toggle with text (`ToggleContextMenuControlSettings`)
1. Graphical separator (`SeparatorContextMenuControlSettings`) -- A simple grey line used as a divider.
1. User profile info (`UserProfileContextMenuControlSettings`) - Presents name, claimed name badge and user address similar to the user Passport
1. Toggle with text and check mark (`ToggleWithCheckContextMenuControlSettings`)
1. Button for a sub-menu (`SubMenuContextMenuButtonSettings`)
1. Scrollable list of buttons with text (`ScrollableButtonListControlSettings`)

Each component comes with its own set of configuration parameters. Default graphical settings are provided but can be overridden as needed. The minimum required parameters are those specific to the component (e.g. for a button are: text, sprite and an action).

Some components, like the toggle, support initial values. These can be set in their respective settings class before the context menu is invoked.

## Usage Example

Here is an example of how the context menu can be configured and used.

### Defining the Context Menu

```csharp
GenericContextMenu contextMenu = new GenericContextMenu()
    .AddControl(publicToggleSettings = new ToggleContextMenuControlSettings(
        view.publicToggleText,
        toggleValue => SetPublicRequested?.Invoke(currentReelData, toggleValue)))
    .AddControl(new SeparatorContextMenuControlSettings())
    .AddControl(new ButtonContextMenuControlSettings(
        view.shareButtonText,
        view.shareButtonSprite,
        () => ShareToXRequested?.Invoke(currentReelData)))
    .AddControl(new ButtonContextMenuControlSettings(
        view.copyButtonText,
        view.copyButtonSprite,
        () => CopyPictureLinkRequested?.Invoke(currentReelData)))
    .AddControl(new ButtonContextMenuControlSettings(
        view.downloadButtonText,
        view.downloadButtonSprite,
        () => DownloadRequested?.Invoke(currentReelData)))
    .AddControl(new ButtonContextMenuControlSettings(
        view.deleteButtonText,
        view.deleteButtonSprite,
        () => DeletePictureRequested?.Invoke(currentReelData)));
```

Note that `AddControl()` has an overload that lets you define a `GenericContextMenuElement`. Using that object reference you can set the control visibility using its `Enabled` property at any given time.

### Setting an Initial Value

```csharp
publicToggleSettings.SetInitialValue(cameraReelResponse.isPublic);
```

### Invoking the Context Menu

```csharp
mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
    new GenericContextMenuParameter(
        contextMenu,
        buttonRectTransform.position,
        actionOnShow: () => isContextMenuOpen = true,
        actionOnHide: () =>
        {
            isContextMenuOpen = false;
            if (view.gameObject.activeSelf)
                HideControl();
        },
        closeTask: closeContextMenuTask?.Task)));
```

## Creation of a new component

### Create a control settings class

We need to create a class that implements `IContextMenuControlSettings` where all the available configs are stored, such as:
1. UI tweaks
1. Action to execute on interaction
1. Configuration data (e.g. the initial boolean value in toggle component)

This is also a good place to create a method that lets you set the initial data, since this instance can be stored upon creation of the context menu itself.

### Create a component view

We need to create a class that behaves as the View of our component. This class must inherit `GenericContextMenuComponentBase` and will hold the references of all the `GameObjects` we need to use in our component.
The superclass will force us to implement two methods:
1. `UnregisterListeners`: will be called upon disable, therefore it's where we can remove all listeners registered to our components
1. `RegisterCloseListener`: it is called on setup and provides an `Action` that closes the context menu itself, therefore we will register it on every interactable component that upon interaction, we want them to trigger the menu close.

This class is the perfect place to create a configuration method that has our control settings as parameter so that we can apply our configurations.

### Handle the control pool manager

We also need to manage our new component inside the pool manager. We therefore need to call `CreateObjectPool` for our new View type.
We finally need to manage the `Get` action of the new component. This is done inside the `GetContextMenuComponent` function where we have access to its settings.

### Prefab creation and management

We finally need only to create our new prefab for our control that will have the new View component attached.
Once it is completed we need to extend the `GenericContextMenuPlugin.cs` plugin to reference and load the new prefab. Remember to also assign it in the `Global Plugins Settings` asset file.
