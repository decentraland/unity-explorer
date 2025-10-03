using DCL.Chat.ChatServices;
using DCL.ChatArea;
using DCL.Communities.CommunitiesDataProvider;
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
        private readonly CommunityVoiceChatController? communitiesVoiceChatController;
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
            PlayerEntryView playerEntry,
            ChatSharedAreaEventBus chatSharedAreaEventBus,
            ChatClickDetectionService chatClickDetectionService)
        {
            this.view = view;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.chatClickDetectionService = chatClickDetectionService;

            voiceChatPanelResizeController = new VoiceChatPanelResizeController(view.VoiceChatPanelResizeView, voiceChatOrchestrator);
            privateVoiceChatController = new PrivateVoiceChatController(view.VoiceChatView, voiceChatOrchestrator, voiceChatHandler, profileDataProvider, roomHub.VoiceChatRoom().Room());
            communitiesVoiceChatController = new CommunityVoiceChatController(view.CommunityVoiceChatView, playerEntry, profileDataProvider, voiceChatOrchestrator, voiceChatHandler, roomManager, communityDataProvider, webRequestController);
            sceneVoiceChatController = new SceneVoiceChatController(view.SceneVoiceChatTitlebarView, voiceChatOrchestrator);
            voiceChatPanelState = voiceChatOrchestrator.CurrentVoiceChatPanelState;

            eventSubscriptions.Add(chatClickDetectionService);

            eventSubscriptions.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelPointerEnterEvent>(_ => OnPointerEnterChatArea()));
            eventSubscriptions.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelPointerExitEvent>(_ => OnPointerExitChatArea()));
            eventSubscriptions.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelGlobalClickEvent>(HandleGlobalClick));

            chatClickDetectionService.OnClickInside += HandleClickInside;
            chatClickDetectionService.OnClickOutside += HandleClickOutside;
        }

        private void OnPointerExitChatArea()
        {
            if (voiceChatPanelState.Value == VoiceChatPanelState.FOCUSED)
                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.UNFOCUSED);
        }

        private void OnPointerEnterChatArea()
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
