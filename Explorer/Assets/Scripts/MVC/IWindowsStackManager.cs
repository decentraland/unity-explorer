namespace MVC
{
    public interface IWindowsStackManager
    {
        /// <summary>
        ///     For each sortingLayer there is a separate stack with a begin value for <see cref="CanvasOrdering.OrderInLayer" />
        /// </summary>
        (IController previousController, CanvasOrdering newControllerOrdering) Push(IController controller);

        /// <summary>
        ///     Recycle a previously rented ordering setup.
        ///     Returns the previous controller in the stack.
        ///     Controller can be popped from the middle?
        /// </summary>
        IController Pop(IController controller);
    }
}
