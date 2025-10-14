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
        private readonly ChatClickDetectionService clickDetectionService;

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
            clickDetectionService = new ChatClickDetectionService(view.transform);
            presenterScope.Add(clickDetectionService);

            view.PointerEnter += OnPointerEnter;
            view.PointerExit += OnPointerExit;

            clickDetectionService.OnClickInside += HandleClickInside;
            clickDetectionService.OnClickOutside += HandleClickOutside;

            presenterScope.Add(voiceChatOrchestrator.CommunityCallStatus.Subscribe(OnCallStatusChanged));
            presenterScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelShownInSharedSpaceEvent>(HandleChatPanelShownInSharedSpace));
            presenterScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelHiddenInSharedSpaceEvent>(HandleChatPanelHiddenInSharedSpace));
            presenterScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelToggleEvent>(HandleChatPanelToggle));
            presenterScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.ChatPanelVisibilityEvent>(HandleChatPanelVisibility));
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status.IsNotConnected())
                clickDetectionService.Pause();
            else
                clickDetectionService.Resume();
        }

        private void HandleChatPanelVisibility(ChatSharedAreaEvents.ChatPanelVisibilityEvent evt)
        {
            voiceChatOrchestrator.ChangePanelState(evt.IsVisible ? VoiceChatPanelState.UNFOCUSED : VoiceChatPanelState.HIDDEN, force: true);

            if (evt.IsVisible)
            {
                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.UNFOCUSED, force: true);
                clickDetectionService.Resume();
            }
            else
            {
                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.HIDDEN, force: true);
                clickDetectionService.Pause();
            }
        }

        private void HandleChatPanelToggle(ChatSharedAreaEvents.ChatPanelToggleEvent evt)
        {
            if (voiceChatOrchestrator.CurrentVoiceChatPanelState.Value is VoiceChatPanelState.HIDDEN)
            {
                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.FOCUSED, force: true);
                clickDetectionService.Resume();
            }
        }

        private void HandleChatPanelHiddenInSharedSpace(ChatSharedAreaEvents.ChatPanelHiddenInSharedSpaceEvent _)
        {
            voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.HIDDEN, force: true);
            clickDetectionService.Pause();
        }

        private void HandleChatPanelShownInSharedSpace(ChatSharedAreaEvents.ChatPanelShownInSharedSpaceEvent evt)
        {
            voiceChatOrchestrator.ChangePanelState(evt.Focus? VoiceChatPanelState.FOCUSED : VoiceChatPanelState.UNFOCUSED, force: true);
            clickDetectionService.Resume();
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

            clickDetectionService.OnClickInside -= HandleClickInside;
            clickDetectionService.OnClickOutside -= HandleClickOutside;
            clickDetectionService.Dispose();
        }
    }
}
