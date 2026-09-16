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
    ///     It only reports the close intent (Jump in); what happens next is up to the caller.
    /// </summary>
    public class LobbyController : ControllerBase<LobbyView>
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
            viewInstance?.JumpInButton.onClick.RemoveListener(OnJumpIn);
            closeIntent?.TrySetCanceled();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            viewInstance!.JumpInButton.onClick.AddListener(OnJumpIn);
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

        private void OnJumpIn()
        {
            closeIntent?.TrySetResult();
            closeIntent = null;
        }
    }
}
