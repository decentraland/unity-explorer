using DCL.Communities.CommunitiesDataProvider;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.UI.Profiles.Helpers;
using DCL.VoiceChat.CommunityVoiceChat;
using DCL.WebRequests;
using System;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelController : IDisposable
    {
        private readonly VoiceChatPanelView view;
        private readonly VoiceChatOrchestrator voiceChatOrchestrator;

        private readonly PrivateVoiceChatController? privateVoiceChatController;
        private readonly CommunityVoiceChatController? communitiesVoiceChatController;
        private readonly VoiceChatPanelResizeController? voiceChatPanelResizeController;
        private readonly SceneVoiceChatController? sceneVoiceChatController;

        public VoiceChatPanelController(VoiceChatPanelView view,
            ProfileRepositoryWrapper profileDataProvider,
            CommunitiesDataProvider communityDataProvider,
            IWebRequestController webRequestController,
            VoiceChatOrchestrator voiceChatOrchestrator,
            VoiceChatMicrophoneHandler voiceChatHandler,
            VoiceChatRoomManager roomManager,
            IRoomHub roomHub,
            PlayerEntryView playerEntry)
        {
            this.view = view;
            this.voiceChatOrchestrator = voiceChatOrchestrator;

            voiceChatPanelResizeController = new VoiceChatPanelResizeController(view.VoiceChatPanelResizeView, voiceChatOrchestrator);
            privateVoiceChatController = new PrivateVoiceChatController(view.VoiceChatView, voiceChatOrchestrator, voiceChatHandler, profileDataProvider, roomHub.VoiceChatRoom().Room());
            communitiesVoiceChatController = new CommunityVoiceChatController(view.CommunityVoiceChatView, playerEntry, profileDataProvider, voiceChatOrchestrator, voiceChatHandler, roomManager, communityDataProvider, webRequestController);
            sceneVoiceChatController = new SceneVoiceChatController(view.SceneVoiceChatTitlebarView, voiceChatOrchestrator);

            view.PointerClick += OnPointerClick;
        }

        private void OnPointerClick()
        {
            voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.SELECTED);
        }

        public void OnPointerExit()
        {
            if (voiceChatOrchestrator.CurrentVoiceChatPanelState.Value == VoiceChatPanelState.FOCUSED)
                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.UNFOCUSED);
        }

        public void OnPointerEnter()
        {
            if (voiceChatOrchestrator.CurrentVoiceChatPanelState.Value == VoiceChatPanelState.UNFOCUSED)
                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.FOCUSED);
        }

        public void Dispose()
        {
            privateVoiceChatController?.Dispose();
            communitiesVoiceChatController?.Dispose();
            sceneVoiceChatController?.Dispose();
            voiceChatPanelResizeController?.Dispose();
            view.PointerClick -= OnPointerClick;
        }
    }
}
