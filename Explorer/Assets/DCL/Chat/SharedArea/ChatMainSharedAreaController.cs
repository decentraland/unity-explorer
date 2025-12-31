using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatServices;
using DCL.Chat.ControllerShowParams;
using MVC;
using System.Threading;
using Utility;
using InputAction = UnityEngine.InputSystem.InputAction;

namespace DCL.ChatArea
{
    public class ChatMainSharedAreaController : ControllerBase<ChatMainSharedAreaView, ChatMainSharedAreaControllerShowParams>
    {
        private readonly IMVCManager mvcManager;
        private readonly ChatSharedAreaEventBus chatSharedAreaEventBus;
        private readonly IEventBus chatEventBus;
        private readonly EventSubscriptionScope eventScope = new ();
        private readonly DCLInput dclInput;

        public readonly CommandRegistry CommandRegistry;

        public bool IsVisibleInSharedSpace { get; private set; }

        private int fullscreenViewsOpenCount;

        public ChatMainSharedAreaController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            ChatSharedAreaEventBus chatSharedAreaEventBus,
            CommandRegistry commandRegistry,
            IEventBus chatEventBus,
            DCLInput dclInput) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.chatSharedAreaEventBus = chatSharedAreaEventBus;
            this.CommandRegistry = commandRegistry;
            this.chatEventBus = chatEventBus;
            this.dclInput = dclInput;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            mvcManager.OnViewShowed += OnMvcViewShowed;
            mvcManager.OnViewClosed += OnMvcViewClosed;
            viewInstance!.OnPointerEnterEvent += HandlePointerEnter;
            viewInstance.OnPointerExitEvent += HandlePointerExit;
            dclInput.UI.Submit.performed += OnUISubmitPerformed;

            eventScope.Add(chatEventBus.Subscribe<ChatEvents.ToggleChatEvent>(ToggleChatState));
            // Subscribe to visibility state changes
            eventScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelVisibilityStateChangedEvent>(HandleVisibilityStateChanged));
            CentralizedChatClickDetectionService.Instance.Resume();
        }

        protected override void OnViewClose()
        {

        }

        protected override void OnViewShow()
        {
            CentralizedChatClickDetectionService.Instance.Resume();
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

        private void ToggleChatState(ChatEvents.ToggleChatEvent _)
        {
            chatSharedAreaEventBus.RaiseToggleEvent();
        }

        private void OnUISubmitPerformed(InputAction.CallbackContext callbackContext)
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
                CentralizedChatClickDetectionService.Instance.Pause();
                // If the entire controller view is not even active, we can't proceed.
                await UniTask.CompletedTask;
                return;
            }

            // If the chat was fully hidden (e.g., by the Friends panel), transition to Default.
            // If it was minimized, transition to Default or Focused based on the input.
            // The `showParams.Focus` will be true when toggling with Enter/shortcut, and false when returning from another panel.
            chatSharedAreaEventBus.RaiseShownInSharedSpaceEvent(showParams.Focus);
            CentralizedChatClickDetectionService.Instance.Resume();

            await UniTask.CompletedTask;
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            CentralizedChatClickDetectionService.Instance.Pause();
            chatSharedAreaEventBus.RaiseHiddenInSharedSpaceEvent();
            await UniTask.CompletedTask;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
            CentralizedChatClickDetectionService.Instance.Pause();
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
        }

        private void OnMvcViewShowed(IController controller)
        {
            //We only need to notify the chat if a fullscreen view is shown (to hide it)
            if (controller.Layer is not CanvasOrdering.SortingLayer.Fullscreen) return;

            fullscreenViewsOpenCount++;
            if (fullscreenViewsOpenCount == 1)
                chatSharedAreaEventBus.RaiseFullscreenOpenEvent();
        }

        private void OnMvcViewClosed(IController controller)
        {
            if (controller.Layer is not CanvasOrdering.SortingLayer.Fullscreen) return;
            fullscreenViewsOpenCount--;

            if (fullscreenViewsOpenCount == 0)
                chatSharedAreaEventBus.RaiseFullscreenClosedEvent();
        }

        private void HandleVisibilityStateChanged(ChatSharedAreaEvents.ChatPanelVisibilityStateChangedEvent evt)
        {
            IsVisibleInSharedSpace = evt.IsVisibleInSharedSpace;
            if (IsVisibleInSharedSpace)
                CentralizedChatClickDetectionService.Instance.Resume();
            else
                CentralizedChatClickDetectionService.Instance.Pause();
        }
    }
}
