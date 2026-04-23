using DCL.Chat.ChatServices;
using DCL.ChatArea;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.VoiceChat.CommunityVoiceChat;
using DCL.WebRequests;
using MVC;
using System;
using DCL.UI;
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

        private VoiceChatPanelState? stateBeforeFullscreen;

        public VoiceChatPanelPresenter(VoiceChatPanelView view,
            ProfileRepositoryWrapper profileDataProvider,
            CommunitiesDataProvider communityDataProvider,
            ImageControllerProvider imageControllerProvider,
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

            var communitiesVoiceChatController = new CommunityVoiceChatPresenter(view.CommunityVoiceChatView, participantEntryView, profileDataProvider, voiceChatOrchestrator, voiceChatHandler, roomManager, communityDataProvider, imageControllerProvider);
            presenterScope.Add(communitiesVoiceChatController);

            var sceneVoiceChatController = new SceneVoiceChatPresenter(view.SceneVoiceChatPanelView, voiceChatOrchestrator, imageControllerProvider);
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
            presenterScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.MVCViewOpenEvent>(OnMVCViewOpened));
            presenterScope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.MVCViewClosedEvent>(OnMVCViewClosed));
        }

        private void OnMVCViewOpened(ChatSharedAreaEvents.MVCViewOpenEvent evt)
        {
            if (evt.ViewSortingLayer is not CanvasOrdering.SortingLayer.FULLSCREEN) return;

            stateBeforeFullscreen ??= voiceChatPanelState.Value;

            voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.HIDDEN, force: true);
            clickDetectionHandler.Pause();
        }

        private void OnMVCViewClosed(ChatSharedAreaEvents.MVCViewClosedEvent evt)
        {
            if (evt.ViewSortingLayer is not CanvasOrdering.SortingLayer.FULLSCREEN) return;
            if (stateBeforeFullscreen is null) return;

            VoiceChatPanelState previous = stateBeforeFullscreen.Value;
            stateBeforeFullscreen = null;

            // Always leave HIDDEN — NONE re-activates the panel container without focus side effects
            VoiceChatPanelState restoreTo = previous is VoiceChatPanelState.NONE or VoiceChatPanelState.HIDDEN
                ? VoiceChatPanelState.NONE
                : VoiceChatPanelState.UNFOCUSED;

            voiceChatOrchestrator.ChangePanelState(restoreTo, force: true);
            clickDetectionHandler.Resume();
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
