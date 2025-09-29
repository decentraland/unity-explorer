using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;
using System.Collections.Generic;

namespace DCL.ChatArea
{
    public class ChatMainController : ControllerBase<ChatMainView, ChatControllerShowParams>,
                                  IControllerInSharedSpace<ChatMainView, ChatControllerShowParams>
    {
        private readonly IMVCManager mvcManager;
        private readonly ChatCoordinationEventBus coordinationEventBus;

        private readonly HashSet<IBlocksChat> chatBlockers = new ();

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public bool IsVisibleInSharedSpace => false; // This will need to be tracked differently if needed

        public ChatMainController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            ChatCoordinationEventBus coordinationEventBus) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.coordinationEventBus = coordinationEventBus;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            mvcManager.OnViewShowed += OnMvcViewShowed;
            mvcManager.OnViewClosed += OnMvcViewClosed;
            viewInstance!.OnPointerEnterEvent += HandlePointerEnter;
            viewInstance.OnPointerExitEvent += HandlePointerExit;
        }

        protected override void OnViewShow()
        {
            coordinationEventBus.RaiseViewShowEvent();
        }

        public void SetVisibility(bool isVisible)
        {
            coordinationEventBus.RaiseVisibilityEvent(isVisible);
        }

        public void SetFocusState()
        {
            coordinationEventBus.RaiseFocusEvent();
        }

        public void ToggleState()
        {
            coordinationEventBus.RaiseToggleEvent();
        }

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ChatControllerShowParams showParams)
        {
            // This method is called when we want to "show" the chat.
            // This can happen when:
            // 1. Toggling from a Minimized state.
            // 2. Another panel (like Friends) that was obscuring the chat is closed.
            if (State == ControllerState.ViewHidden)
            {
                // If the entire controller view is not even active, we can't proceed.
                await UniTask.CompletedTask;
                return;
            }

            // If the chat was fully hidden (e.g., by the Friends panel), transition to Default.
            // If it was minimized, transition to Default or Focused based on the input.
            // The `showParams.Focus` will be true when toggling with Enter/shortcut, and false when returning from another panel.
            coordinationEventBus.RaiseShownInSharedSpaceEvent(showParams.Focus);


            ViewShowingComplete?.Invoke(this);
            await UniTask.CompletedTask;
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            coordinationEventBus.RaiseHiddenInSharedSpaceEvent();
            await UniTask.CompletedTask;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }

        private void HandlePointerEnter()
        {
            coordinationEventBus.RaisePointerEnter();
        }

        private void HandlePointerExit()
        {
            coordinationEventBus.RaisePointerExit();
        }

        public override void Dispose()
        {
            if (viewInstance != null)
            {
                mvcManager.OnViewShowed -= OnMvcViewShowed;
                mvcManager.OnViewClosed -= OnMvcViewClosed;
                viewInstance.OnPointerEnterEvent -= HandlePointerEnter;
                viewInstance.OnPointerExitEvent -= HandlePointerExit;
            }

            base.Dispose();

            chatBlockers.Clear();
        }

        private void OnMvcViewShowed(IController controller)
        {
            if (controller is not IBlocksChat blocker) return;

            chatBlockers.Add(blocker);
            coordinationEventBus.RaiseMvcViewShowedEvent();
        }

        private void OnMvcViewClosed(IController controller)
        {
            if (controller is not IBlocksChat blocker) return;

            chatBlockers.Remove(blocker);
             if (chatBlockers.Count == 0)
                coordinationEventBus.RaiseMvcViewClosedEvent();
        }
    }
}
