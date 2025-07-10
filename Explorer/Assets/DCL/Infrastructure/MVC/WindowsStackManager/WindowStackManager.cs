using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace MVC
{
    public class WindowStackManager : IWindowsStackManager
    {
        private const int POPUP_ORDER_IN_LAYER_INCREMENT = 2;
        // Calculated from the old equation when Count == 0: ((popupStack.Count - 1) * 2) - 1
        private const int MINIMUM_POPUP_CLOSER_ODER_IN_LAYER = -3;

        internal List<IController> popupStack { get; } = new ();
        internal List<IController> persistentStack { get; } = new ();
        internal IController? fullscreenController { get; private set; }
        internal IController? topController { get; private set; }

        public IController? TopMostPopup => popupStack.LastOrDefault();
        public IController CurrentFullscreenController => fullscreenController;

        private readonly List<(IController controller, UniTaskCompletionSource? onClose)> closeableStack = new ();
        private readonly Dictionary<IController, int> popupOrders = new ();

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
            int currentMaxOrderInLayer = POPUP_ORDER_IN_LAYER_INCREMENT;
            IController? topMostPopup = TopMostPopup;
            if (topMostPopup != null && popupOrders.TryGetValue(topMostPopup, out int topMostPopupOrder))
                // We increment the order in layer by 2 to keep the popup closer ordering odd
                currentMaxOrderInLayer = topMostPopupOrder + POPUP_ORDER_IN_LAYER_INCREMENT;

            popupStack.Add(controller);

            // Keep track of every popup canvass order
            popupOrders.Add(controller, currentMaxOrderInLayer);

            UniTaskCompletionSource? onClose = null;

            if (controller.CanBeClosedByEscape)
                closeableStack.Add((controller, onClose = new UniTaskCompletionSource()));

            foreach (var persistant in persistentStack)
                if (persistant.State == ControllerState.ViewFocused)
                    persistant.Blur();

            return new PopupPushInfo(
                    new CanvasOrdering(CanvasOrdering.SortingLayer.Popup, currentMaxOrderInLayer),
                    new CanvasOrdering(CanvasOrdering.SortingLayer.Popup, currentMaxOrderInLayer - 1),
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
            popupOrders.Remove(controller);

            if (popupStack.Count == 0)
            {
                foreach (var persistant in persistentStack)
                    if (persistant.State == ControllerState.ViewBlurred)
                        persistant.Focus();
            }

            TryPopCloseable(controller);

            // We get the topmost popup after removing the current one so that we can calculate the new popup closer ordering
            // and configure the new top most, if exists
            IController? topMostPopup = TopMostPopup;

            return new PopupPopInfo(
                new CanvasOrdering(CanvasOrdering.SortingLayer.Popup, topMostPopup == null ? MINIMUM_POPUP_CLOSER_ODER_IN_LAYER : popupOrders[topMostPopup] - 1),
                topMostPopup);
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
