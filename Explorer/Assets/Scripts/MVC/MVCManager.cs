using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MVC
{
    public class MVCManager : IMVCManager
    {
        private readonly Dictionary<Type, IController> controllers;
        private readonly IWindowsStackManager windowsStackManager;
        private readonly CancellationTokenSource destructionCancellationTokenSource;
        private readonly PopupCloserView popupCloser;

        public MVCManager(IWindowsStackManager windowsStackManager, CancellationTokenSource destructionCancellationTokenSource, PopupCloserView popupCloser)
        {
            this.windowsStackManager = windowsStackManager;
            this.destructionCancellationTokenSource = destructionCancellationTokenSource;
            this.popupCloser = popupCloser;

            controllers = new Dictionary<Type, IController>(200);
        }

        /// <summary>
        ///     Instead of a builder just expose a method
        ///     to add controller gradually (from different plug-ins).
        ///     It should not be exposed to the interface as the interface is for consumers only
        /// </summary>
        public void RegisterController<TView, TInputData>(IController<TView, TInputData> controller) where TView: MonoBehaviour, IView
        {
            // throw an exception if the same combination of <TView, TInputData> was already added
            controllers.Add(typeof(IController<TView, TInputData>), controller);
        }

        public async UniTask Show<TView, TInputData>(ShowCommand<TView, TInputData> command) where TView: MonoBehaviour, IView
        {
            // Find the controller
            IController controller = controllers[typeof(IController<TView, TInputData>)];
            CancellationToken ct = destructionCancellationTokenSource.Token;

            switch (controller.SortingLayer)
            {
                case CanvasOrdering.SORTING_LAYER.Popup:
                    await ShowPopup(command, controller, ct);
                    break;
                case CanvasOrdering.SORTING_LAYER.Fullscreen:
                    await ShowFullScreen(command, controller, ct);
                    break;
                case CanvasOrdering.SORTING_LAYER.Persistent:
                    await ShowPersistent(command, controller, ct);
                    break;
                case CanvasOrdering.SORTING_LAYER.Top:
                    await ShowTop(command, controller, ct);
                    break;
            }
        }

        private async UniTask ShowTop<TView, TInputData>(ShowCommand<TView, TInputData> command, IController controller, CancellationToken ct)
            where TView : MonoBehaviour, IView
        {
            // Push new fullscreen controller
            TopPushInfo topPushInfo = windowsStackManager.PushTop(controller);

            // Hide all popups in the stack and clear it
            foreach (IController popupController in topPushInfo.PopupControllers)
            {
                popupController.HideView(ct).Forget();
            }
            topPushInfo.PopupControllers.Clear();

            if (topPushInfo.FullscreenController != null)
            {
                topPushInfo.FullscreenController.HideView(ct).Forget();
                windowsStackManager.PopFullscreen(topPushInfo.FullscreenController);
            }

            // Hide the popup closer
            popupCloser.Hide(ct).Forget();

            await UniTask.WhenAll(command.Execute(controller, topPushInfo.ControllerOrdering, ct));

            await controller.HideView(ct);
        }

        private async UniTask ShowPersistent<TView, TInputData>(ShowCommand<TView, TInputData> command, IController controller, CancellationToken ct)
            where TView : MonoBehaviour, IView
        {
            // Push new fullscreen controller
            PersistentPushInfo persistentPushInfo = windowsStackManager.PushPersistent(controller);

            await UniTask.WhenAll(command.Execute(controller, persistentPushInfo.ControllerOrdering, ct));

            await controller.HideView(ct);
        }

        private async UniTask ShowFullScreen<TView, TInputData>(ShowCommand<TView, TInputData> command, IController controller, CancellationToken ct)
            where TView : MonoBehaviour, IView
        {
            // Push new fullscreen controller
            FullscreenPushInfo fullscreenPushInfo = windowsStackManager.PushFullscreen(controller);

            // Hide all popups in the stack and clear it
            foreach (IController popupController in fullscreenPushInfo.PopupControllers)
            {
                popupController.HideView(ct).Forget();
            }
            fullscreenPushInfo.PopupControllers.Clear();

            // Hide the popup closer
            popupCloser.Hide(ct).Forget();

            await UniTask.WhenAll(command.Execute(controller, fullscreenPushInfo.ControllerOrdering, ct));

            await controller.HideView(ct);
            windowsStackManager.PopFullscreen(controller);
        }

        private async UniTask ShowPopup<TView, TInputData>(ShowCommand<TView, TInputData> command, IController controller, CancellationToken ct)
            where TView : MonoBehaviour, IView
        {
            PopupPushInfo pushPopupPush = windowsStackManager.PushPopup(controller);

            popupCloser.SetDrawOrder(pushPopupPush.PopupCloserOrdering);

            pushPopupPush.PreviousController?.OnBlur();

            await UniTask.WhenAny(
                UniTask.WhenAll(command.Execute(controller, pushPopupPush.ControllerOrdering, ct), popupCloser.Show(ct)),
                WaitForPopupCloserClick(controller, ct));

            // "Close" command has been received
            await controller.HideView(ct);

            // Pop the stack
            PopupPopInfo popupPopInfo = windowsStackManager.PopPopup(controller);

            if (popupPopInfo.NewTopMostController != null)
            {
                popupCloser.SetDrawOrder(popupPopInfo.PopupCloserOrdering);
                popupPopInfo.NewTopMostController.OnFocus();
            }
            else
            {
                popupCloser.Hide(ct).Forget();
            }
        }

        private async UniTask WaitForPopupCloserClick(IController currentController, CancellationToken ct)
        {
            do
            {
                await popupCloser.CloseButton.OnClickAsync(ct);
            }
            while (currentController != windowsStackManager.TopMostPopup);
        }
    }
}
