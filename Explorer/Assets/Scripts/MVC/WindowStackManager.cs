using System;

namespace MVC
{
    public class WindowStackManager : IWindowsStackManager
    {
        public (IController previousController, CanvasOrdering newControllerOrdering) Push(IController controller) =>
            throw new NotImplementedException();

        public IController Pop(IController controller) =>
            throw new NotImplementedException();
    }
}
