using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace MVC
{
    public class WindowStackManager : IWindowsStackManager
    {
        private List<IController> popupStack = new List<IController>();
        private List<IController> persistentStack = new List<IController>();
        private IController fullscreenController;
        private IController topController;

        public IController TopMostPopup => popupStack.LastOrDefault();

        public PopupPushInfo PushPopup(IController controller)
        {
            int orderInLayer = popupStack.Count * 2;
            popupStack.Add(controller);

            return new PopupPushInfo(
                    new CanvasOrdering(CanvasOrdering.SortingLayer.Popup, orderInLayer),
                    new CanvasOrdering(CanvasOrdering.SortingLayer.Popup, orderInLayer - 1),
                    popupStack.Count >= 2 ? popupStack[^2] : null);
        }

        public FullscreenPushInfo PushFullscreen(IController controller)
        {
            fullscreenController = controller;
            return new FullscreenPushInfo(popupStack, new CanvasOrdering(CanvasOrdering.SortingLayer.Fullscreen, 0));
        }

        public void PopFullscreen(IController controller) =>
            fullscreenController = null;

        public PersistentPushInfo PushPersistent(IController controller)
        {
            persistentStack.Add(controller);
            return new PersistentPushInfo(new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, -20));
        }

        public TopPushInfo PushTop(IController controller)
        {
            topController = controller;
            return new TopPushInfo(popupStack, fullscreenController, new CanvasOrdering(CanvasOrdering.SortingLayer.Top, 1));
        }

        public void PopTop(IController controller) =>
            topController = null;

        public PopupPopInfo PopPopup(IController controller)
        {
            popupStack.Remove(controller);
            return new PopupPopInfo(
                new CanvasOrdering(CanvasOrdering.SortingLayer.Popup, ((popupStack.Count - 1) * 2) - 1),
                TopMostPopup);
        }
    }

    public readonly struct PopupPushInfo
    {
        public readonly CanvasOrdering ControllerOrdering;
        public readonly CanvasOrdering PopupCloserOrdering;
        public readonly IController PreviousController;

        public PopupPushInfo(CanvasOrdering controllerOrdering, CanvasOrdering popupCloserOrdering, IController previousController)
        {
            this.ControllerOrdering = controllerOrdering;
            this.PopupCloserOrdering = popupCloserOrdering;
            this.PreviousController = previousController;
        }
    }

    public readonly struct PopupPopInfo
    {
        public readonly CanvasOrdering PopupCloserOrdering;
        public readonly IController NewTopMostController;

        public PopupPopInfo(CanvasOrdering popupCloserOrdering, IController newTopMostController)
        {
            this.PopupCloserOrdering = popupCloserOrdering;
            this.NewTopMostController = newTopMostController;
        }
    }

    public readonly struct FullscreenPushInfo
    {
        public readonly List<IController> PopupControllers;
        public readonly CanvasOrdering ControllerOrdering;

        public FullscreenPushInfo(List<IController> popupControllers, CanvasOrdering controllerOrdering)
        {
            this.PopupControllers = popupControllers;
            ControllerOrdering = controllerOrdering;
        }
    }

    public readonly struct TopPushInfo
    {
        public readonly List<IController> PopupControllers;
        public readonly IController FullscreenController;
        public readonly CanvasOrdering ControllerOrdering;

        public TopPushInfo(List<IController> popupControllers, IController fullscreenController, CanvasOrdering controllerOrdering)
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
