using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Input;
using DCL.ParcelsService;
using DCL.SceneLoadingScreens;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.TeleportPrompt
{
    public partial class TeleportPromptController : ControllerBase<TeleportPromptView, TeleportPromptController.Params>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly ICursor cursor;
        private readonly ITeleportController teleportController;
        private readonly IMVCManager mvcManager;
        private Action<TeleportPromptResultType> resultCallback;

        public TeleportPromptController(
            ViewFactoryMethod viewFactory,
            ICursor cursor,
            ITeleportController teleportController,
            IMVCManager mvcManager) : base(viewFactory)
        {
            this.cursor = cursor;
            this.teleportController = teleportController;
            this.mvcManager = mvcManager;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.CloseButton.onClick.AddListener(Dismiss);
            viewInstance.CancelButton.onClick.AddListener(Dismiss);
            viewInstance.ContinueButton.onClick.AddListener(Approve);
        }

        protected override void OnViewShow()
        {
            cursor.Unlock();
            RequestTeleport(inputData.Coords, result =>
            {
                if (result != TeleportPromptResultType.Approved)
                    return;

                TeleportToInputCoords().Forget();
            });
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.CancelButton.OnClickAsync(ct),
                viewInstance.ContinueButton.OnClickAsync(ct));

        private void RequestTeleport(Vector2Int coords, Action<TeleportPromptResultType> result)
        {
            resultCallback = result;
            viewInstance.CoordsText.text = $"Teleport to {coords.x},{coords.y}?";
        }

        private void Dismiss() =>
            resultCallback?.Invoke(TeleportPromptResultType.Canceled);

        private void Approve() =>
            resultCallback?.Invoke(TeleportPromptResultType.Approved);

        private async UniTaskVoid TeleportToInputCoords()
        {
            var loadReport = AsyncLoadProcessReport.Create();
            var timeout = TimeSpan.FromSeconds(30);

            await UniTask.WhenAll(
                mvcManager.ShowAsync(SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport, timeout))),
                teleportController.TeleportToSceneSpawnPointAsync(inputData.Coords, loadReport, CancellationToken.None));
        }
    }
}
