using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;

namespace DCL.Chat
{
    public class ChatMainController : ControllerBase<ChatMainView, ChatControllerShowParams>,
                                  IControllerInSharedSpace<ChatMainView, ChatControllerShowParams>
    {
        private readonly ChatMainPresenter chatMainPresenter;

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public bool IsVisibleInSharedSpace => State != ControllerState.ViewHidden && !chatMainPresenter.IsHidden();

        public ChatMainController(ViewFactoryMethod viewFactory, ChatMainPresenter chatMainPresenter) : base(viewFactory)
        {
            this.chatMainPresenter = chatMainPresenter;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            chatMainPresenter.SetView(viewInstance);
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            chatMainPresenter.OnViewShow();
        }

        protected override void OnViewClose()
        {
            chatMainPresenter.OnViewClose();
        }

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ChatControllerShowParams showParams)
        {
            if (State != ControllerState.ViewHidden)
            {
                chatMainPresenter.OnShown(showParams);
                ViewShowingComplete?.Invoke(this);
            }
            await UniTask.CompletedTask;
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            chatMainPresenter.OnHidden();
            await UniTask.CompletedTask;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }

        public override void Dispose()
        {
            base.Dispose();
            chatMainPresenter.Dispose();
        }
    }
}