using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatServices.ChatContextService;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Communities.CommunitiesDataProvider;
using DCL.UI.InputFieldFormatting;
using DCL.UI.Profiles.Helpers;
using DCL.VoiceChat;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Chat
{
    public class ChatMainController : ControllerBase<ChatMainView, ChatControllerShowParams>,
                                  IControllerInSharedSpace<ChatMainView, ChatControllerShowParams>
    {
        private readonly IEventBus eventBus;
        private readonly ChatInputBlockingService chatInputBlockingService;
        private readonly CommandRegistry commandRegistry;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ChatMemberListService chatMemberListService;
        private readonly CommunityDataService communityDataService;
        private readonly CurrentChannelService currentChannelService;
        private readonly ChatConfig.ChatConfig chatConfig;
        private readonly IChatHistory chatHistory;
        private readonly IChatEventBus chatEventBus;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly IMVCManager mvcManager;
        private readonly ChatContextMenuService chatContextMenuService;
        private readonly ChatClickDetectionService chatClickDetectionService;
        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly ITextFormatter textFormatter;

        private ChatPanelPresenter? chatPanelPresenter;

        private readonly HashSet<IBlocksChat> chatBlockers = new ();

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public bool IsVisibleInSharedSpace => chatPanelPresenter?.IsVisibleInSharedSpace ?? false;

        public ChatMainController(ViewFactoryMethod viewFactory,
            ChatConfig.ChatConfig chatConfig,
            IEventBus eventBus,
            IMVCManager mvcManager,
            IChatEventBus chatEventBus,
            CurrentChannelService currentChannelService,
            ChatInputBlockingService chatInputBlockingService,
            CommandRegistry commandRegistry,
            IChatHistory chatHistory,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ChatMemberListService chatMemberListService,
            ChatContextMenuService chatContextMenuService,
            CommunityDataService communityDataService,
            ChatClickDetectionService chatClickDetectionService,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            CommunitiesDataProvider communityDataProvider,
            ITextFormatter textFormatter) : base(viewFactory)
        {
            this.chatConfig = chatConfig;
            this.eventBus = eventBus;
            this.mvcManager = mvcManager;
            this.chatEventBus = chatEventBus;
            this.currentChannelService = currentChannelService;
            this.chatInputBlockingService = chatInputBlockingService;
            this.commandRegistry = commandRegistry;
            this.chatHistory = chatHistory;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.chatMemberListService = chatMemberListService;
            this.chatContextMenuService = chatContextMenuService;
            this.communityDataService = communityDataService;
            this.chatClickDetectionService = chatClickDetectionService;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.communityDataProvider = communityDataProvider;
            this.textFormatter = textFormatter;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            mvcManager.OnViewShowed += OnMvcViewShowed;
            mvcManager.OnViewClosed += OnMvcViewClosed;
            viewInstance!.OnPointerEnterEvent += HandlePointerEnter;
            viewInstance.OnPointerExitEvent += HandlePointerExit;

            chatPanelPresenter = new ChatPanelPresenter(viewInstance!.ChatPanelView, textFormatter, voiceChatOrchestrator, currentChannelService, communityDataProvider, chatConfig, chatEventBus, chatHistory, communityDataService,
                chatMemberListService, profileRepositoryWrapper, commandRegistry, chatInputBlockingService, eventBus, chatContextMenuService, chatClickDetectionService);
        }

        private void OnUIClose(InputAction.CallbackContext obj)
        {
            if (chatBlockers.Count > 0) return;

            chatPanelPresenter?.OnUIClose();
        }

        protected override void OnViewShow()
        {
            chatPanelPresenter?.OnViewShow();
        }

        public void SetVisibility(bool isVisible)
        {
            chatPanelPresenter?.SetVisibility(isVisible);
        }

        public void SetFocusState()
        {
            chatPanelPresenter?.SetFocusState();
        }

        public void ToggleState()
        {
            chatPanelPresenter?.ToggleState();
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
            chatPanelPresenter?.OnShownInSharedSpaceAsync(showParams.Focus);


            ViewShowingComplete?.Invoke(this);
            await UniTask.CompletedTask;
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            chatPanelPresenter?.OnHiddenInSharedSpace();
            await UniTask.CompletedTask;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }

        private void HandlePointerEnter()
        {
            chatPanelPresenter?.HandlePointerEnter();

        }

        private void HandlePointerExit()
        {
            chatPanelPresenter?.HandlePointerExit();
        }

        public override void Dispose()
        {
            if (viewInstance != null)
            {
                mvcManager.OnViewShowed -= OnMvcViewShowed;
                mvcManager.OnViewClosed -= OnMvcViewClosed;
                viewInstance.OnPointerEnterEvent -= HandlePointerEnter;
                viewInstance.OnPointerExitEvent -= HandlePointerExit;
                DCLInput.Instance.UI.Close.performed -= OnUIClose;
            }

            base.Dispose();

            chatMemberListService.Dispose();
            chatBlockers.Clear();
        }

        private void OnMvcViewShowed(IController controller)
        {
            if (controller is not IBlocksChat blocker) return;

            chatPanelPresenter?.OnMvcViewShowed();

            chatBlockers.Add(blocker);
        }

        private void OnMvcViewClosed(IController controller)
        {
            if (controller is not IBlocksChat blocker) return;

            chatBlockers.Remove(blocker);

            if (chatBlockers.Count == 0)
                chatPanelPresenter?.OnMvcViewClosed();

        }
    }
}
