namespace MVC
{
    public interface IWindowsStackManager
    {
        IController TopMostPopup { get; }

        /// <summary>
        ///     For each sortingLayer there is a separate stack with a begin value for <see cref="CanvasOrdering.OrderInLayer" />
        /// </summary>
        PopupPushInfo PushPopup(IController controller);

        FullscreenPushInfo PushFullscreen(IController controller);

        FullscreenPopInfo PopFullscreen(IController controller);

        PersistentPushInfo PushPersistent(IController controller);

        TopPushInfo PushTop(IController controller);

        void PopTop(IController controller);

        /// <summary>
        ///     Recycle a previously rented ordering setup.
        ///     Returns the previous controller in the stack.
        ///     Controller can be popped from the middle?
        /// </summary>
        PopupPopInfo PopPopup(IController controller);
    }
}
