using DCL.Chat.ChatServices;
using DCL.ChatArea;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.VoiceChat.CommunityVoiceChat;
using DCL.WebRequests;
using System;
using Utility;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelPresenter : IDisposable
    {
        private readonly VoiceChatPanelView view;
        private readonly VoiceChatOrchestrator voiceChatOrchestrator;
        private readonly ChatClickDetectionService chatClickDetectionService;

        private readonly PrivateVoiceChatController? privateVoiceChatController;
        private readonly CommunityVoiceChatPresenter? communitiesVoiceChatController;
        private readonly VoiceChatPanelResizeController? voiceChatPanelResizeController;
        private readonly SceneVoiceChatController? sceneVoiceChatController;
        private readonly IReadonlyReactiveProperty<VoiceChatPanelState> voiceChatPanelState;
        private readonly EventSubscriptionScope eventSubscriptions = new();

        public VoiceChatPanelPresenter(VoiceChatPanelView view,
            ProfileRepositoryWrapper profileDataProvider,
            CommunitiesDataProvider communityDataProvider,
            IWebRequestController webRequestController,
            VoiceChatOrchestrator voiceChatOrchestrator,
            VoiceChatMicrophoneHandler voiceChatHandler,
            VoiceChatRoomManager roomManager,
            IRoomHub roomHub,
            VoiceChatParticipantEntryView participantEntryView,
            ChatSharedAreaEventBus chatSharedAreaEventBus,
            ChatClickDetectionService chatClickDetectionService,
            IDecentralandUrlsSource urlsSource)
        {
            this.view = view;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.chatClickDetectionService = chatClickDetectionService;

            voiceChatPanelResizeController = new VoiceChatPanelResizeController(view.VoiceChatPanelResizeView, voiceChatOrchestrator);
            privateVoiceChatController = new PrivateVoiceChatController(view.PrivateVoiceChatView, voiceChatOrchestrator, voiceChatHandler, profileDataProvider, roomHub.VoiceChatRoom().Room());
            communitiesVoiceChatController = new CommunityVoiceChatPresenter(view.CommunityVoiceChatView, participantEntryView, profileDataProvider, voiceChatOrchestrator, voiceChatHandler, roomManager, communityDataProvider, webRequestController);
            sceneVoiceChatController = new SceneVoiceChatController(view.SceneVoiceChatPanelView, voiceChatOrchestrator);
            voiceChatPanelState = voiceChatOrchestrator.CurrentVoiceChatPanelState;

            eventSubscriptions.Add(chatClickDetectionService);

            eventSubscriptions.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelPointerEnterEvent>(OnPointerEnterChatArea));
            eventSubscriptions.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelPointerExitEvent>(OnPointerExitChatArea));
            eventSubscriptions.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelGlobalClickEvent>(HandleGlobalClick));
            eventSubscriptions.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelShownInSharedSpaceEvent>(HandleChatPanelShownInSharedSpace));
            eventSubscriptions.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelHiddenInSharedSpaceEvent>(HandleChatPanelHiddenInSharedSpace));
            eventSubscriptions.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelToggleEvent>(HandleChatPanelToggle));
            eventSubscriptions.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelVisibilityEvent>(HandleChatPanelVisibility));

            chatClickDetectionService.OnClickInside += HandleClickInside;
            chatClickDetectionService.OnClickOutside += HandleClickOutside;
        }

        private void HandleChatPanelVisibility(ChatSharedAreaEvents.ChatPanelVisibilityEvent evt)
        {
            voiceChatOrchestrator.ChangePanelState(evt.IsVisible ? VoiceChatPanelState.UNFOCUSED : VoiceChatPanelState.HIDDEN, force: true);
        }

        private void HandleChatPanelToggle(ChatSharedAreaEvents.ChatPanelToggleEvent evt)
        {
            if (voiceChatOrchestrator.CurrentVoiceChatPanelState.Value is VoiceChatPanelState.HIDDEN)
                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.FOCUSED, force: true);
        }

        private void HandleChatPanelHiddenInSharedSpace(ChatSharedAreaEvents.ChatPanelHiddenInSharedSpaceEvent _)
        {
            voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.HIDDEN, force: true);
        }

        private void HandleChatPanelShownInSharedSpace(ChatSharedAreaEvents.ChatPanelShownInSharedSpaceEvent evt)
        {
            voiceChatOrchestrator.ChangePanelState(evt.Focus? VoiceChatPanelState.FOCUSED : VoiceChatPanelState.UNFOCUSED, force: true);
        }

        private void OnPointerExitChatArea(ChatSharedAreaEvents.ChatPanelPointerExitEvent _)
        {
            if (voiceChatPanelState.Value == VoiceChatPanelState.FOCUSED)
                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.UNFOCUSED);
        }

        private void OnPointerEnterChatArea(ChatSharedAreaEvents.ChatPanelPointerEnterEvent _)
        {
            if (voiceChatPanelState.Value == VoiceChatPanelState.UNFOCUSED)
                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.FOCUSED);
        }

        private void HandleGlobalClick(ChatSharedAreaEvents.ChatPanelGlobalClickEvent evt) =>
            chatClickDetectionService.ProcessRaycastResults(evt.RaycastResults);

        private void HandleClickInside()
        {
            if (voiceChatPanelState.Value == VoiceChatPanelState.SELECTED) return;

            voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.SELECTED);
        }

        private void HandleClickOutside()
        {
            if (voiceChatPanelState.Value == VoiceChatPanelState.UNFOCUSED) return;

            voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.UNFOCUSED);
        }

        public void Dispose()
        {
            privateVoiceChatController?.Dispose();
            communitiesVoiceChatController?.Dispose();
            sceneVoiceChatController?.Dispose();
            voiceChatPanelResizeController?.Dispose();

            chatClickDetectionService.OnClickInside -= HandleClickInside;
            chatClickDetectionService.OnClickOutside -= HandleClickOutside;

            eventSubscriptions.Dispose();
        }
    }
}
