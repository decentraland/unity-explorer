using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;
using System.Collections.Generic;
using Utility;

namespace DCL.ChatArea
{
    public class ChatMainSharedAreaController : ControllerBase<ChatMainSharedAreaView, ChatMainSharedAreaControllerShowParams>, IControllerInSharedSpace<ChatMainSharedAreaView, ChatMainSharedAreaControllerShowParams>
    {
        private readonly IMVCManager mvcManager;
        private readonly ChatSharedAreaEventBus chatSharedAreaEventBus;

        private readonly HashSet<IBlocksChat> chatBlockers = new ();
        private readonly EventSubscriptionScope eventScope = new ();

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public bool IsVisibleInSharedSpace { get; private set; }

        public ChatMainSharedAreaController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            ChatSharedAreaEventBus chatSharedAreaEventBus) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.chatSharedAreaEventBus = chatSharedAreaEventBus;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            mvcManager.OnViewShowed += OnMvcViewShowed;
            mvcManager.OnViewClosed += OnMvcViewClosed;
            viewInstance!.OnPointerEnterEvent += HandlePointerEnter;
            viewInstance.OnPointerExitEvent += HandlePointerExit;

            // Subscribe to visibility state changes
            eventScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelVisibilityStateChangedEvent>(HandleVisibilityStateChanged));
        }

        protected override void OnViewShow()
        {
            chatSharedAreaEventBus.RaiseViewShowEvent();
        }

        public void SetVisibility(bool isVisible)
        {
            chatSharedAreaEventBus.RaiseVisibilityEvent(isVisible);
        }

        public void SetFocusState()
        {
            chatSharedAreaEventBus.RaiseFocusEvent();
        }

        public void ToggleState()
        {
            chatSharedAreaEventBus.RaiseToggleEvent();
        }

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ChatMainSharedAreaControllerShowParams showParams)
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
            chatSharedAreaEventBus.RaiseShownInSharedSpaceEvent(showParams.Focus);


            ViewShowingComplete?.Invoke(this);
            await UniTask.CompletedTask;
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            chatSharedAreaEventBus.RaiseHiddenInSharedSpaceEvent();
            await UniTask.CompletedTask;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }

        private void HandlePointerEnter()
        {
            chatSharedAreaEventBus.RaisePointerEnter();
        }

        private void HandlePointerExit()
        {
            chatSharedAreaEventBus.RaisePointerExit();
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

            eventScope.Dispose();

            base.Dispose();

            chatBlockers.Clear();
        }

        private void OnMvcViewShowed(IController controller)
        {
            if (controller is not IBlocksChat blocker) return;

            chatBlockers.Add(blocker);
            chatSharedAreaEventBus.RaiseMvcViewShowedEvent();
        }

        private void OnMvcViewClosed(IController controller)
        {
            if (controller is not IBlocksChat blocker) return;

            chatBlockers.Remove(blocker);
             if (chatBlockers.Count == 0)
                chatSharedAreaEventBus.RaiseMvcViewClosedEvent();
        }

        private void HandleVisibilityStateChanged(ChatSharedAreaEvents.ChatPanelVisibilityStateChangedEvent evt)
        {
            IsVisibleInSharedSpace = evt.IsVisibleInSharedSpace;
        }
    }
}
