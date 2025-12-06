using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using MVC.PopupsController.PopupCloser;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MVC
{
    public class MVCManager : IMVCManager
    {
        private readonly Dictionary<Type, IController> controllers;
        private readonly IWindowsStackManager windowsStackManager;
        private readonly CancellationTokenSource destructionCancellationTokenSource;
        private readonly IPopupCloserView popupCloser;
        public IReadOnlyDictionary<Type, IController> Controllers => controllers;

        public event Action<IController>? OnViewShowed;
        public event Action<IController>? OnViewClosed;

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

        public void Dispose()
        {
            foreach (IController controllersValue in controllers.Values)
                controllersValue.Dispose();

            destructionCancellationTokenSource?.Dispose();
            windowsStackManager.Dispose();
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

        public void SetAllViewsCanvasActive(bool isActive)
        {
            foreach (IController controllersValue in controllers.Values)
                controllersValue.SetViewCanvasActive(isActive);
        }

        public void SetAllViewsCanvasActive(IController except, bool isActive)
        {
            foreach (IController controller in controllers.Values)
                if (controller != except)
                    controller.SetViewCanvasActive(isActive);
                else
                    controller.SetViewCanvasActive(!isActive);
        }

        public async UniTask ShowAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, CancellationToken ct = default) where TView: IView
        {
            // Find the controller
            IController controller = controllers[typeof(IController<TView, TInputData>)];

            if (controller.State != ControllerState.ViewHidden)
                return;

            ct = ct.Equals(default(CancellationToken))
                ? destructionCancellationTokenSource.Token
                : CancellationTokenSource.CreateLinkedTokenSource(ct, destructionCancellationTokenSource.Token).Token;

            try
            {
                OnViewShowed?.Invoke(controller);

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

                OnViewClosed?.Invoke(controller);
            }
            catch (OperationCanceledException _)
            {
                // TODO (Vit) : handle revert of command. Proposal - extend WizardCommands interface with Revert method and call it in case of cancellation.
                ReportHub.LogWarning(ReportCategory.MVC, $"ShowAsync was cancelled for {controller.GetType()}");
            }
        }

        private async UniTask ShowTopAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, IController controller, CancellationToken ct)
            where TView: IView
        {
            // Push new fullscreen controller
            OverlayPushInfo overlayPushInfo = windowsStackManager.PushOverlay(controller);

            // Hide all popups in the stack and clear it
            if (overlayPushInfo.PopupControllers != null)
                CloseAllPopups(overlayPushInfo.PopupControllers);

            // Hide fullscreen UI if any
            if (overlayPushInfo.FullscreenController != null)
                windowsStackManager.PopFullscreen(overlayPushInfo.FullscreenController);

            // Hide the popup closer
            popupCloser.HideAsync(ct).Forget();

            await UniTask.WhenAny(command.Execute(controller, overlayPushInfo.ControllerOrdering, ct), windowsStackManager.GetControllerClosure(controller)?.Task ?? UniTask.Never(ct));

            await controller.HideViewAsync(ct);
        }

        private async UniTask ShowPersistentAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, IController controller, CancellationToken ct)
            where TView: IView
        {
            // Push a new fullscreen controller
            PersistentPushInfo persistentPushInfo = windowsStackManager.PushPersistent(controller);

            try { await UniTask.WhenAny(command.Execute(controller, persistentPushInfo.ControllerOrdering, ct), windowsStackManager.GetControllerClosure(controller)?.Task ?? UniTask.Never(ct)); }
            finally
            {
                // If an exception occured in the life cycle of the persistent view, we must remove it from the stack
                // Otherwise, it will be in an indefinite state upon calling Focus/Blur

                ReportHub.LogWarning(ReportCategory.MVC, $"Emergency occured in the persistent controller {controller.GetType().Name}\n"
                                                         + "It will be removed from the stack");

                windowsStackManager.RemovePersistent(controller);
            }
        }

        private void CloseAllPopups(List<(IController, int)> popupControllers)
        {
            // Hide all popups in the stack and clear it
            for (int i = popupControllers.Count - 1; i >= 0; i--)
                windowsStackManager.PopPopup(popupControllers[i].Item1);

            popupControllers.Clear();
        }

        private async UniTask ShowFullScreenAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, IController controller, CancellationToken ct)
            where TView: IView
        {
            if (windowsStackManager.CurrentFullscreenController == controller)
                return;

            //Hide current fullscreen if any so it's safe to push a new one
            if (windowsStackManager.CurrentFullscreenController != null)
                windowsStackManager.PopFullscreen(windowsStackManager.CurrentFullscreenController);

            // Push new fullscreen controller
            FullscreenPushInfo fullscreenPushInfo = windowsStackManager.PushFullscreen(controller);

            try
            {
                CloseAllPopups(fullscreenPushInfo.PopupControllers);

                // Hide the popup closer
                popupCloser.HideAsync(ct).Forget();

                await UniTask.WhenAny(command.Execute(controller, fullscreenPushInfo.ControllerOrdering, ct),
                    fullscreenPushInfo.OnClose?.Task ?? UniTask.Never(ct),
                    windowsStackManager.GetControllerClosure(controller)?.Task ?? UniTask.Never(ct));

                await controller.HideViewAsync(ct);
            }
            finally { windowsStackManager.PopFullscreen(controller); }
        }

        private async UniTask ShowPopupAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, IController controller, CancellationToken ct)
            where TView: IView
        {
            PopupPushInfo pushPopupPush = windowsStackManager.PushPopup(controller);

            popupCloser.SetDrawOrder(pushPopupPush.PopupCloserOrdering);

            try
            {
                pushPopupPush.PreviousController?.Blur();

                await UniTask.WhenAny(
                    UniTask.WhenAll(command.Execute(controller, pushPopupPush.ControllerOrdering, ct), popupCloser.ShowAsync(ct)),
                    WaitForPopupCloserClickAsync(controller, ct),
                    pushPopupPush.OnClose?.Task ?? UniTask.Never(ct),
                    windowsStackManager.GetControllerClosure(controller)?.Task ?? UniTask.Never(ct));

                // "Close" command has been received
                await controller.HideViewAsync(ct);
            }
            finally
            {
                // Pop the stack
                PopupPopInfo popupPopInfo = windowsStackManager.PopPopup(controller, windowsStackManager.GetControllerClosure(controller)?.Task.Status != UniTaskStatus.Succeeded);

                if (popupPopInfo.NewTopMostController != null)
                {
                    popupCloser.SetDrawOrder(popupPopInfo.PopupCloserOrdering);
                    popupPopInfo.NewTopMostController.Focus();
                }
                else { popupCloser.HideAsync(ct).Forget(); }
            }
        }

        private async UniTask WaitForPopupCloserClickAsync(IController currentController, CancellationToken ct)
        {
            do
            {
                await UniTask.WhenAll(popupCloser.CloseButton.OnClickAsync(ct),
                    UniTask.WaitUntil(() => currentController.State == ControllerState.ViewFocused));
            }
            while (currentController != windowsStackManager.TopMostPopup.controller);
        }
    }
}
