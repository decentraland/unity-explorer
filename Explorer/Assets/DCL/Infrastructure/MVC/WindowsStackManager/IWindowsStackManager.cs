using Cysharp.Threading.Tasks;

namespace MVC
{
    public interface IWindowsStackManager
    {
        UniTaskCompletionSource GetTopMostCloseTask(IController controller);
        IController TopMostPopup { get; }

        IController CurrentFullscreenController { get; }

        /// <summary>
        ///     For each sortingLayer there is a separate stack with a begin value for <see cref="CanvasOrdering.OrderInLayer" />
        /// </summary>
        PopupPushInfo PushPopup(IController controller);

        FullscreenPushInfo PushFullscreen(IController controller);

        void PopFullscreen(IController controller);

        PersistentPushInfo PushPersistent(IController controller);

        /// <summary>
        ///     Persistent view should be removed on emergency only
        /// </summary>
        void RemovePersistent(IController controller);

        OverlayPushInfo PushOverlay(IController controller);

        void PopOverlay(IController controller);

        /// <summary>
        ///     Recycle a previously rented ordering setup.
        ///     Returns the previous controller in the stack.
        ///     Controller can be popped from the middle?
        /// </summary>
        PopupPopInfo PopPopup(IController controller);
    }
}
