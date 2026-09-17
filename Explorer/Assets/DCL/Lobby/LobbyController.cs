using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Input.Component;
using DCL.RealmNavigation;
using MVC;
using System.Threading;

namespace DCL.Lobby
{
    /// <summary>
    ///     Fullscreen panel shown before the world starts loading and, later, on demand during gameplay.
    ///     It only reports the close intent (Jump in or Close); what happens next is up to the caller.
    /// </summary>
    public class LobbyController : ControllerBase<LobbyView, LobbyParameter>
    {
        private readonly IInputBlock inputBlock;
        private readonly IReadOnlyLoadingStatus loadingStatus;

        private UniTaskCompletionSource? closeIntent;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        // Before the world is loaded Jump in is the only way out; once in-world Escape behaves like any other fullscreen panel.
        public override bool CanBeClosedByEscape => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed;

        public LobbyController(ViewFactoryMethod viewFactory, IInputBlock inputBlock, IReadOnlyLoadingStatus loadingStatus) : base(viewFactory)
        {
            this.inputBlock = inputBlock;
            this.loadingStatus = loadingStatus;
        }

        public override void Dispose()
        {
            base.Dispose();
            viewInstance?.JumpInButton.onClick.RemoveListener(RequestClose);
            viewInstance?.CloseButton.onClick.RemoveListener(RequestClose);
            closeIntent?.TrySetCanceled();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            viewInstance!.JumpInButton.onClick.AddListener(RequestClose);
            viewInstance.CloseButton.onClick.AddListener(RequestClose);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            viewInstance!.CloseButton.gameObject.SetActive(!inputData.IsStartup);
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeIntent?.TrySetCanceled(ct);
            closeIntent = new UniTaskCompletionSource();
            await closeIntent.Task.AttachExternalCancellation(ct);
        }

        private void RequestClose()
        {
            closeIntent?.TrySetResult();
            closeIntent = null;
        }
    }

    public readonly struct LobbyParameter
    {
        /// <summary>
        ///     True when the lobby gates the in-app initialization flow (first show of the session), false when the user opened it on demand in-world.
        ///     At startup Jump in is the only way out, so no close button is offered.
        /// </summary>
        public readonly bool IsStartup;

        public LobbyParameter(bool isStartup)
        {
            IsStartup = isStartup;
        }
    }
}
