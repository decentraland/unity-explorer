using Cysharp.Threading.Tasks;
using MVC;

namespace DCL.UI.SharedSpaceManager
{
    /// <summary>
    ///     The main purpose of this manager is to regulate how UI panels that share a space in screen (and cannot overlap each other) are shown and hidden, so every new panel added to this manager
    ///     behaves in a homogeneous way. For example, when there are 2 mutually exclusive panels, showing one of them will make the other hide, and the panel being shown will not be visible until the other is
    ///     completely hidden.
    /// </summary>
    /// <remarks>
    ///     Note that we are calling them "panels" because they may not be either controllers or views; panels are required to implement IPanelInSharedSpace interface, whose implementation may
    ///     vary notably depending on whether they are controllers or not, and depending on the type of controller (Persistent, Popup or Fullscreen); Overlay controllers do not need to be included in the manager.
    ///     Internally, the manager will use the MVC Manager when possible, so the rules of that system apply (a Fullscreen controller will hide all Popups automatically, but not Persistent controllers or other panels).
    ///
    ///     How it works:
    ///
    ///     - When ShowAsync is called, it will hide any registered panel (depending on the established rules), wait for it to finish its animation or cleaning process, and then showing the panel. ShowAsync
    ///     may be called from anywhere. If something has to happen when a controller is hidden, that logic should be added right after the line where the manager awaits the panel to stop showing (remember that,
    ///     in our MVC manager, a Fullscreen or Popup controller is "showing" until its WaitForCloseIntentAsync finishes).
    ///     - The ToggleVisibilityAsync method is used when there is no way to know whether the panel is hidden or not, and it has to change its state.
    ///     - Regarding the parameters sent to these methods, they can be of any type although in case of showing a controller the type should match the one expected by it.
    ///
    ///     How to add new panels:
    ///
    ///     - Register the panel.
    ///     - Make the panel implement either IPanelInSharedSpace (if it does not inherit from IController) or IControllerInSharedSpace (otherwise).
    ///     -> The panel must raise the ViewShowingComplete event right before the condition for the WaitForCloseIntentAsync is evaluated, in case it is a controller, or before leaving the OnShownInSharedSpaceAsync method.
    ///     -> The OnHiddenInSharedSpaceAsync method must make the WaitForCloseIntentAsync finish, in case it is a controller, or hide the view / visual elements waiting for any animation or cleaning process to finish.
    ///     -> The OnShownInSharedSpaceAsync method should be implemented by Persistent controllers or panels that are not controllers, and should wait for any animation or preparation to finish.
    ///     -> The IsVisibleInSharedSpace will normally check if the view is not hidden, it the case it is a controller, or any other condition that means that the panel can be hidden.
    ///     - Add the panel to the PanelsInSharedSpace enumeration.
    ///     - Add a section to the ShowAsync method to handle how the panel should be shown, depending on whether it is a controller or not, and whether it needs some specific logic (like hiding other specific panel).
    ///     - Add a section to the HideAsync method if it needs specific logic (like opening other panel when it is hidden, for example).
    /// </remarks>
    public interface ISharedSpaceManager
    {
        /// <summary>
        ///     Hides any registered panel (depending on the established rules), waits for it to finish its animation or cleaning process, and then shows the desired panel. It may be called from anywhere.
        /// </summary>
        /// <param name="panel">Which panel to show.</param>
        /// <param name="parameters">Optionally, the parameters the panel will use when shown.</param>
        /// <returns>The async task.</returns>
        UniTask ShowAsync<TParams>(PanelsSharingSpace panel, TParams parameters = default!);

        /// <summary>
        ///     <inheritdoc cref="ShowAsync{TParams}" /> <br />
        ///     A shortcut to <see cref="ShowAsync{TParams}" /> to pass a default value of <see cref="ControllerNoData" /> without specifying it, it will not work if the panel actually requires a non-<see cref="ControllerNoData" /> parameter
        /// </summary>
        UniTask ShowAsync(PanelsSharingSpace panel) =>
            ShowAsync<ControllerNoData>(panel);

        /// <summary>
        ///     It shows the panel if it is hidden, or hides it if it is not. Should be used when there is no way to know whether the panel is hidden or not, and it has to change its state
        /// </summary>
        /// <param name="panel">Which panel's state to change.</param>
        /// <param name="parameters">Optionally, the parameters the panel will use when shown / hidden.</param>
        /// <returns>The async task.</returns>
        UniTask ToggleVisibilityAsync<TParams>(PanelsSharingSpace panel, TParams parameters = default!);

        /// <summary>
        ///     A shortcut to <see cref="ToggleVisibilityAsync{TParams}" /> to pass a default value of <see cref="ControllerNoData" /> without specifying it, it will not work if the panel actually requires a non-<see cref="ControllerNoData" /> parameter
        /// </summary>
        UniTask ToggleVisibilityAsync(PanelsSharingSpace panel) =>
            ToggleVisibilityAsync<ControllerNoData>(panel);

        /// <summary>
        ///     When a panel is registered, it means that its visibility will be affected by the visibility of the other registered panels, and vice versa.
        /// </summary>
        /// <param name="panel">The identifier of the panel.</param>
        /// <param name="panelImplementation">The actual implementation of the panel.</param>
        void RegisterPanel<TParams>(PanelsSharingSpace panel, IPanelInSharedSpace<TParams> panelImplementation);

        /// <summary>
        ///     <inheritdoc cref="RegisterPanel{T}" /> <br />
        ///     Registers controller as a panel
        /// </summary>
        void RegisterPanel<TView, TInputData>(PanelsSharingSpace panel, IControllerInSharedSpace<TView, TInputData> controller) where TView: IView;
    }
}
