﻿using Cysharp.Threading.Tasks;
using MVC.PopupsController.PopupCloser;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MVC
{
    public class MVCManager : IMVCManager
    {
        public IReadOnlyDictionary<Type, IController> Controllers => controllers;

        private readonly Dictionary<Type, IController> controllers;
        private readonly IWindowsStackManager windowsStackManager;
        private readonly CancellationTokenSource destructionCancellationTokenSource;
        private readonly IPopupCloserView popupCloser;

        public MVCManager(
            IWindowsStackManager windowsStackManager,
            CancellationTokenSource destructionCancellationTokenSource,
            IPopupCloserView popupCloserView)
        {
            this.windowsStackManager = windowsStackManager;
            this.destructionCancellationTokenSource = destructionCancellationTokenSource;

            controllers = new Dictionary<Type, IController>(200);
            popupCloser = popupCloserView;
        }

        /// <summary>
        ///     Instead of a builder just expose a method
        ///     to add controller gradually (from different plug-ins).
        ///     It should not be exposed to the interface as the interface is for consumers only
        /// </summary>
        public void RegisterController<TView, TInputData>(IController<TView, TInputData> controller) where TView: IView
        {
            // throw an exception if the same combination of <TView, TInputData> was already added
            controllers.Add(typeof(IController<TView, TInputData>), controller);
        }

        public async UniTask ShowAsync<TView, TInputData>(ShowCommand<TView, TInputData> command) where TView: IView
        {
            // Find the controller
            IController controller = controllers[typeof(IController<TView, TInputData>)];
            CancellationToken ct = destructionCancellationTokenSource.Token;

            switch (controller.Layer)
            {
                case CanvasOrdering.SortingLayer.Popup:
                    await ShowPopupAsync(command, controller, ct);
                    break;
                case CanvasOrdering.SortingLayer.Fullscreen:
                    await ShowFullScreenAsync(command, controller, ct);
                    break;
                case CanvasOrdering.SortingLayer.Persistent:
                    await ShowPersistentAsync(command, controller, ct);
                    break;
                case CanvasOrdering.SortingLayer.Overlay:
                    await ShowTopAsync(command, controller, ct);
                    break;
            }
        }

        private async UniTask ShowTopAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, IController controller, CancellationToken ct)
            where TView: IView
        {
            // Push new fullscreen controller
            OverlayPushInfo overlayPushInfo = windowsStackManager.PushOverlay(controller);

            // Hide all popups in the stack and clear it
            if (overlayPushInfo.PopupControllers != null)
            {
                foreach (IController popupController in overlayPushInfo.PopupControllers)
                    popupController.HideViewAsync(ct).Forget();

                overlayPushInfo.PopupControllers.Clear();
            }

            // Hide fullscreen UI if any
            if (overlayPushInfo.FullscreenController != null)
            {
                overlayPushInfo.FullscreenController.HideViewAsync(ct).Forget();
                windowsStackManager.PopFullscreen(overlayPushInfo.FullscreenController);
            }

            // Hide the popup closer
            popupCloser.HideAsync(ct).Forget();

            await command.Execute(controller, overlayPushInfo.ControllerOrdering, ct);

            await controller.HideViewAsync(ct);
        }

        private async UniTask ShowPersistentAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, IController controller, CancellationToken ct)
            where TView: IView
        {
            // Push new fullscreen controller
            PersistentPushInfo persistentPushInfo = windowsStackManager.PushPersistent(controller);

            await command.Execute(controller, persistentPushInfo.ControllerOrdering, ct);
        }

        private async UniTask ShowFullScreenAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, IController controller, CancellationToken ct)
            where TView: IView
        {
            // Push new fullscreen controller
            FullscreenPushInfo fullscreenPushInfo = windowsStackManager.PushFullscreen(controller);

            // Hide all popups in the stack and clear it
            if (fullscreenPushInfo.PopupControllers != null)
            {
                foreach (IController popupController in fullscreenPushInfo.PopupControllers)
                    popupController.HideViewAsync(ct).Forget();

                fullscreenPushInfo.PopupControllers.Clear();
            }

            // Hide the popup closer
            popupCloser.HideAsync(ct).Forget();

            await command.Execute(controller, fullscreenPushInfo.ControllerOrdering, ct);

            await controller.HideViewAsync(ct);
            windowsStackManager.PopFullscreen(controller);
        }

        private async UniTask ShowPopupAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, IController controller, CancellationToken ct)
            where TView: IView
        {
            PopupPushInfo pushPopupPush = windowsStackManager.PushPopup(controller);

            popupCloser.SetDrawOrder(pushPopupPush.PopupCloserOrdering);

            pushPopupPush.PreviousController?.Blur();

            await UniTask.WhenAny(
                UniTask.WhenAll(command.Execute(controller, pushPopupPush.ControllerOrdering, ct), popupCloser.ShowAsync(ct)),
                WaitForPopupCloserClickAsync(controller, ct));

            // "Close" command has been received
            await controller.HideViewAsync(ct);

            // Pop the stack
            PopupPopInfo popupPopInfo = windowsStackManager.PopPopup(controller);

            if (popupPopInfo.NewTopMostController != null)
            {
                popupCloser.SetDrawOrder(popupPopInfo.PopupCloserOrdering);
                popupPopInfo.NewTopMostController.Focus();
            }
            else { popupCloser.HideAsync(ct).Forget(); }
        }

        private async UniTask WaitForPopupCloserClickAsync(IController currentController, CancellationToken ct)
        {
            do { await popupCloser.CloseButton.OnClickAsync(ct); }
            while (currentController != windowsStackManager.TopMostPopup);
        }
    }
}
