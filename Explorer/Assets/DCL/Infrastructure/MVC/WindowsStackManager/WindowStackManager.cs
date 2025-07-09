using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace MVC
{
    public class WindowStackManager : IWindowsStackManager
    {
        internal List<IController> popupStack { get; } = new ();
        internal List<IController> persistentStack { get; } = new ();
        internal IController? fullscreenController { get; private set; }
        internal IController? topController { get; private set; }

        public IController? TopMostPopup => popupStack.LastOrDefault();
        public IController CurrentFullscreenController => fullscreenController;

        private readonly List<(IController controller, UniTaskCompletionSource? onClose)> closeableStack = new ();

        public WindowStackManager()
        {
            DCLInput.Instance.UI.Close.performed += CloseNextUI;
        }

        public void Dispose() =>
            DCLInput.Instance.UI.Close.performed -= CloseNextUI;

        private void CloseNextUI(InputAction.CallbackContext obj)
        {
            closeableStack.LastOrDefault().onClose?.TrySetResult();
        }

        public PopupPushInfo PushPopup(IController controller)
        {
            int orderInLayer = popupStack.Count * 2;
            popupStack.Add(controller);

            UniTaskCompletionSource? onClose = null;

            if (controller.CanBeClosedByEscape)
                closeableStack.Add((controller, onClose = new UniTaskCompletionSource()));

            foreach (var persistant in persistentStack)
                if (persistant.State == ControllerState.ViewFocused)
                    persistant.Blur();

            return new PopupPushInfo(
                    new CanvasOrdering(CanvasOrdering.SortingLayer.Popup, orderInLayer),
                    new CanvasOrdering(CanvasOrdering.SortingLayer.Popup, orderInLayer - 1),
                    popupStack.Count >= 2 ? popupStack[^2] : null,
                    onClose);
        }

        public FullscreenPushInfo PushFullscreen(IController controller)
        {
            fullscreenController = controller;

            UniTaskCompletionSource? onClose = null;

            if (controller.CanBeClosedByEscape)
                closeableStack.Add((controller, onClose = new UniTaskCompletionSource()));

            foreach (IController persistentController in persistentStack)
                if(persistentController.State == ControllerState.ViewFocused)
                    persistentController.Blur();

            return new FullscreenPushInfo(popupStack, new CanvasOrdering(CanvasOrdering.SortingLayer.Fullscreen, 0), onClose);
        }

        public void PopFullscreen(IController controller)
        {
            foreach (IController persistentController in persistentStack)
                persistentController.Focus();

            fullscreenController = null;

            if (!controller.CanBeClosedByEscape) return;

            TryPopCloseable(controller);
        }

        public PersistentPushInfo PushPersistent(IController controller)
        {
            persistentStack.Add(controller);
            return new PersistentPushInfo(new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, -20));
        }

        public void RemovePersistent(IController controller)
        {
            persistentStack.Remove(controller);
        }

        public OverlayPushInfo PushOverlay(IController controller)
        {
            topController = controller;
            return new OverlayPushInfo(popupStack, fullscreenController, new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 1));
        }

        public void PopOverlay(IController controller) =>
            topController = null;

        public PopupPopInfo PopPopup(IController controller)
        {
            popupStack.Remove(controller);

            if (popupStack.Count == 0)
            {
                foreach (var persistant in persistentStack)
                    if (persistant.State == ControllerState.ViewBlurred)
                        persistant.Focus();
            }

            TryPopCloseable(controller);

            return new PopupPopInfo(
                new CanvasOrdering(CanvasOrdering.SortingLayer.Popup, ((popupStack.Count - 1) * 2) - 1),
                TopMostPopup);
        }

        private void TryPopCloseable(IController controller)
        {
            if (controller.CanBeClosedByEscape)
            {
                for (var i = 0; i < closeableStack.Count; i++)
                {
                    if (closeableStack[i].controller == controller)
                    {
                        closeableStack.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }

    public readonly struct PopupPushInfo
    {
        public readonly CanvasOrdering ControllerOrdering;
        public readonly CanvasOrdering PopupCloserOrdering;
        public readonly IController? PreviousController;
        public readonly UniTaskCompletionSource? OnClose;

        public PopupPushInfo(CanvasOrdering controllerOrdering, CanvasOrdering popupCloserOrdering, IController? previousController, UniTaskCompletionSource? onClose)
        {
            this.ControllerOrdering = controllerOrdering;
            this.PopupCloserOrdering = popupCloserOrdering;
            this.PreviousController = previousController;
            OnClose = onClose;
        }
    }

    public readonly struct PopupPopInfo
    {
        public readonly CanvasOrdering PopupCloserOrdering;
        public readonly IController? NewTopMostController;

        public PopupPopInfo(CanvasOrdering popupCloserOrdering, IController? newTopMostController)
        {
            this.PopupCloserOrdering = popupCloserOrdering;
            this.NewTopMostController = newTopMostController;
        }
    }

    public readonly struct FullscreenPushInfo
    {
        public readonly List<IController> PopupControllers;
        public readonly CanvasOrdering ControllerOrdering;
        public readonly UniTaskCompletionSource? OnClose;

        public FullscreenPushInfo(List<IController> popupControllers, CanvasOrdering controllerOrdering, UniTaskCompletionSource? onClose)
        {
            this.PopupControllers = popupControllers;
            ControllerOrdering = controllerOrdering;
            OnClose = onClose;
        }
    }

    public readonly struct OverlayPushInfo
    {
        public readonly List<IController> PopupControllers;
        public readonly IController FullscreenController;
        public readonly CanvasOrdering ControllerOrdering;

        public OverlayPushInfo(List<IController> popupControllers, IController fullscreenController, CanvasOrdering controllerOrdering)
        {
            this.PopupControllers = popupControllers;
            ControllerOrdering = controllerOrdering;
            FullscreenController = fullscreenController;
        }
    }

    public readonly struct PersistentPushInfo
    {
        public readonly CanvasOrdering ControllerOrdering;

        public PersistentPushInfo(CanvasOrdering controllerOrdering)
        {
            ControllerOrdering = controllerOrdering;
        }
    }
}
