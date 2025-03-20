
using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.UI.SharedSpaceManager
{
    public interface IPanelInSharedSpace<in TParams> : IPanelInSharedSpace
    {
        /// <summary>
        ///     Called by the manager. It should be implemented by Persistent controllers or panels that are not controllers, and should wait for any animation or preparation to finish before returning.
        ///     Otherwise, it should return immediately.
        /// </summary>
        /// <param name="ct">A cancellation token.</param>
        /// <param name="parameters">Optional parameters used to vary the way the panel is shown.</param>
        /// <returns>The async task.</returns>
        UniTask OnShownInSharedSpaceAsync(CancellationToken ct, TParams parameters = default!);
    }

    /// <summary>
    /// A UI panel whose visibility is managed by a Shared Space Manager. A panel may be a controller, a view or none of them. Panels use this interface as a homogenous way to communicate with the manager after
    /// they have been registered in it.
    /// Panels will tell the manager when did the showing process finish, and will be told when they must show or hide as a result of the activity of other panels.
    /// </summary>
    public interface IPanelInSharedSpace
    {
        delegate void ViewShowingCompleteDelegate(IPanelInSharedSpace controller);

        /// <summary>
        /// Raised once the view has finished any preparation / animation and is ready, when the controller is shown.
        /// It should be placed right before the condition for the WaitForCloseIntentAsync is evaluated, in case it is a controller, or before leaving the OnShownInSharedSpaceAsync method.
        /// </summary>
        event ViewShowingCompleteDelegate ViewShowingComplete;

        /// <summary>
        /// Gets whether the panel can be considered as visible by the manager.
        /// It should check if the view is not hidden, it the case it is a controller, or any other condition that means that the panel can be hidden.
        /// </summary>
        bool IsVisibleInSharedSpace { get; }

        /// <summary>
        /// Called by the manager. When the panel is a controller, the implementation should make the WaitForCloseIntentAsync finish; otherwise, it should hide the view / visual elements, waiting for any animation
        /// or cleaning process to finish before returning.
        /// </summary>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>The async task.</returns>
        UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct);
    }
}
