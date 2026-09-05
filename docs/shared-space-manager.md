# Shared Space Manager

## Origin

When several UI panels compete among them for an area of the screen (Friends, Chat, Notifications...), there must be an easy way to tell all panels to hide when one is going to be shown without any of them knowing about the existence of the others. In the past it was done using buses and other ad-hoc solutions that made the panel controllers and assemblies interdependent, incurring in cyclic referencing very often and convoluted designs. Besides, several Controller implementations were done in very different ways and are shown / hidden using different mechanisms, there was no consistency, which led to a kind of "Frankenstein" UI, as they did not behave the same way even visually.

This was not the definitive solution for all those problems but a **necessary intermediate step** that will help us find the best solution in the future.
It comes from [this PR](https://github.com/decentraland/unity-explorer/pull/3503).

## Main Design Characteristics

A centralized system was implemented that knows about the existence of all the features and orchestrates who shows and who hides and how (using the MVCManager internally), applying specific logics if necessary depending on the panel. Many controllers and UI-related mechanisms had to be adapted (fixed, sometimes). Now all UI panels behave in a similar way, both in their implementations and visually.

Note that we are calling them "panels" because they may not be either controllers or views; panels are required to implement `IPanelInSharedSpace` interface, whose implementation may vary notably depending on whether they are controllers or not, and depending on the type of controller (Persistent, Popup or Fullscreen); Overlay controllers do not need to be included in the manager.
Internally, the manager will use the MVC Manager when possible, so the rules of that system apply (a Fullscreen controller will hide all Popups automatically, but not Persistent controllers or other panels).

* UI panels interdependency reduced. Now only those panels that open other panels (like Emotes Wheel, which opens the Backpack) know about the existence of such panel. If showing the other panel does not require parameters, the caller does not even need to know its type / import its assembly. An enumeration is used to know which panel to show (previously registered). The `IssueCommand` method is called by the manager, internally.
* Now every panel waits for visible panels to hide before it starts showing.
* Any kind of UI implementation can be added to the manager as long as it implements the `IPanelInSharedSpace` interface (even if they do not inherit from `IController` or `IView`, there are some cases like this like `NotificationsMenuController`).
* All participating UI panels use the same workflow so it is easier to catch who is affecting the visibility of other panels.
* Adding some logic to the showing / hiding process is straightforward, full flexibility.
* All shortcuts that affect the panels that share the space are managed at the same place. Previously, they were spread through the code, even in systems (ECS).
* Popups now use the `WaitForCloseIntentAsync` method according to the design of the MVCManager.

Note: Overlay panels are not considered as competing among other panels so they were not registered in the manager.

Note 2: The manager allows showing panels without parameters, even if the implementation inherits from `IController` and requires a parameters structure. If no instance is provided, a default one will be passed internally. This helps reducing dependencies when no specific data has to be passed to some controllers.

## How to Use It

How it works:

* To open a panel that is registered in the `ISharedSpaceManager`, you must call `ISharedSpaceManager.ShowAsync`.
* To hide a panel, it should leave the `WaitForCloseIntentAsync` method internally.
* When `ShowAsync` is called, it will hide any registered panel (depending on the established rules), wait for it to finish its animation or cleaning process, and then show the panel. `ShowAsync` may be called from anywhere.
* When `HideAsync` is called, it will wait for the panel to finish its animation or cleaning process and, depending on the established rules, another panel may be shown or not.
* If something has to happen when a controller is hidden, that logic should be added right after the `await mvcManager.ShowAsync` line in `SharedSpaceManager.ShowAsync` (remember that, in our MVC manager, a Fullscreen or Popup controller is "showing" until its `WaitForCloseIntentAsync` finishes).
* The `ToggleVisibilityAsync` method is used when there is no way to know whether the panel is hidden or not, and it has to change its state.
* Regarding the parameters sent to these methods, they can be of any type although in case of showing a controller the type should match the one expected by it.

How to add new panels:

* Make the panel implement `IPanelInSharedSpace` or `IControllerInSharedSpace` (if it implements `ControllerBase`).
  * The panel must raise the `ViewShowingComplete` event right before the condition for the `WaitForCloseIntentAsync` is evaluated, in case it is a controller, or before leaving the `OnShownInSharedSpaceAsync` method.
  * The `OnHiddenInSharedSpaceAsync` method must make the `WaitForCloseIntentAsync` finish, in case it is a controller, or hide the view / visual elements waiting for any animation or cleaning process to finish. It will be called by the system when hiding the panel.
  * The `OnShownInSharedSpaceAsync` method should be used by Persistent controllers or panels that are not controllers, and should wait for any animation or preparation to finish.
  * The `IsVisibleInSharedSpace` will normally check if the view is not hidden, in the case it is a controller, or any other condition that means that the panel can be hidden.
* The panel should be a **PopUp** (see the Layer property), unless there is a good reason for it to be a different thing.
* Add the panel to the `PanelsInSharedSpace` enumeration.
* Add a section to the `ShowAsync` method to handle how the panel should be shown, depending on whether it is a controller or not, and whether it needs some specific logic (like hiding another specific panel).
* Add a section to the `HideAsync` method if it needs specific logic (like opening another panel when it is hidden, for example).
* Register the panel in the `ISharedSpaceManager` after you register it in the MVCManager, when instantiating the controller.
