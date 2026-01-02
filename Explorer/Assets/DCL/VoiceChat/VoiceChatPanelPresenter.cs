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
        private readonly IReadonlyReactiveProperty<VoiceChatPanelState> voiceChatPanelState;
        private readonly EventSubscriptionScope presenterScope = new();
        private readonly ChatClickDetectionHandler clickDetectionHandler;

        public VoiceChatPanelPresenter(VoiceChatPanelView view,
            ProfileRepositoryWrapper profileDataProvider,
            CommunitiesDataProvider communityDataProvider,
            IWebRequestController webRequestController,
            VoiceChatOrchestrator voiceChatOrchestrator,
            VoiceChatMicrophoneHandler voiceChatHandler,
            VoiceChatRoomManager roomManager,
            IRoomHub roomHub,
            VoiceChatParticipantEntryView participantEntryView,
            ChatSharedAreaEventBus chatSharedAreaEventBus)
        {
            this.view = view;
            this.voiceChatOrchestrator = voiceChatOrchestrator;

            var voiceChatPanelResizePresenter = new VoiceChatPanelResizePresenter(view.VoiceChatPanelResizeView, voiceChatOrchestrator);
            presenterScope.Add(voiceChatPanelResizePresenter);

            var privateVoiceChatController = new PrivateVoiceChatPresenter(view.PrivateVoiceChatView, voiceChatOrchestrator, voiceChatHandler, profileDataProvider, roomHub.VoiceChatRoom().Room());
            presenterScope.Add(privateVoiceChatController);

            var communitiesVoiceChatController = new CommunityVoiceChatPresenter(view.CommunityVoiceChatView, participantEntryView, profileDataProvider, voiceChatOrchestrator, voiceChatHandler, roomManager, communityDataProvider, webRequestController);
            presenterScope.Add(communitiesVoiceChatController);

            var sceneVoiceChatController = new SceneVoiceChatPresenter(view.SceneVoiceChatPanelView, voiceChatOrchestrator);
            presenterScope.Add(sceneVoiceChatController);

            voiceChatPanelState = voiceChatOrchestrator.CurrentVoiceChatPanelState;
            clickDetectionHandler = new ChatClickDetectionHandler(view.transform);
            presenterScope.Add(clickDetectionHandler);

            view.PointerEnter += OnPointerEnter;
            view.PointerExit += OnPointerExit;

            clickDetectionHandler.OnClickInside += HandleClickInside;
            clickDetectionHandler.OnClickOutside += HandleClickOutside;

            presenterScope.Add(voiceChatOrchestrator.CommunityCallStatus.Subscribe(OnCallStatusChanged));
            presenterScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ToggleChatPanelEvent>(HandleChatPanelToggle));
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status.IsNotConnected())
                clickDetectionHandler.Pause();
            else
                clickDetectionHandler.Resume();
        }

        private void HandleChatPanelToggle(ChatSharedAreaEvents.ToggleChatPanelEvent evt)
        {
            if (voiceChatOrchestrator.CurrentVoiceChatPanelState.Value is VoiceChatPanelState.HIDDEN)
            {
                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.FOCUSED, force: true);
                clickDetectionHandler.Resume();
            }
        }

        private void OnPointerExit()
        {
            if (voiceChatPanelState.Value == VoiceChatPanelState.FOCUSED)
                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.UNFOCUSED);
        }

        private void OnPointerEnter()
        {
            if (voiceChatPanelState.Value == VoiceChatPanelState.UNFOCUSED)
                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.FOCUSED);
        }

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
            presenterScope.Dispose();

            view.PointerEnter -= OnPointerEnter;
            view.PointerExit -= OnPointerExit;

            clickDetectionHandler.Dispose();
        }
    }
}
