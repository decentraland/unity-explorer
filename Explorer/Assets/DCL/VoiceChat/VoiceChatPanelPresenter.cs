using DCL.ChatArea;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.VoiceChat.CommunityVoiceChat;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelPresenter : IDisposable
    {
        private readonly VoiceChatPanelView view;
        private readonly VoiceChatOrchestrator voiceChatOrchestrator;

        private readonly PrivateVoiceChatController? privateVoiceChatController;
        private readonly CommunityVoiceChatController? communitiesVoiceChatController;
        private readonly VoiceChatPanelResizeController? voiceChatPanelResizeController;
        private readonly SceneVoiceChatController? sceneVoiceChatController;
        private readonly IReadonlyReactiveProperty<VoiceChatPanelState> voiceChatPanelState;
        private readonly List<IDisposable> eventSubscriptions = new();

        public VoiceChatPanelPresenter(VoiceChatPanelView view,
            ProfileRepositoryWrapper profileDataProvider,
            CommunitiesDataProvider communityDataProvider,
            IWebRequestController webRequestController,
            VoiceChatOrchestrator voiceChatOrchestrator,
            VoiceChatMicrophoneHandler voiceChatHandler,
            VoiceChatRoomManager roomManager,
            IRoomHub roomHub,
            PlayerEntryView playerEntry,
            ChatCoordinationEventBus coordinationEventBus)
        {
            this.view = view;
            this.voiceChatOrchestrator = voiceChatOrchestrator;

            voiceChatPanelResizeController = new VoiceChatPanelResizeController(view.VoiceChatPanelResizeView, voiceChatOrchestrator);
            privateVoiceChatController = new PrivateVoiceChatController(view.VoiceChatView, voiceChatOrchestrator, voiceChatHandler, profileDataProvider, roomHub.VoiceChatRoom().Room());
            communitiesVoiceChatController = new CommunityVoiceChatController(view.CommunityVoiceChatView, playerEntry, profileDataProvider, voiceChatOrchestrator, voiceChatHandler, roomManager, communityDataProvider, webRequestController);
            sceneVoiceChatController = new SceneVoiceChatController(view.SceneVoiceChatTitlebarView, voiceChatOrchestrator);
            voiceChatPanelState = voiceChatOrchestrator.CurrentVoiceChatPanelState;

            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelPointerEnterEvent>(_ => OnPointerEnterChatArea()));
            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelPointerExitEvent>(_ => OnPointerExitChatArea()));
            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelClickInsideEvent>(HandleClickInside));
            eventSubscriptions.Add(coordinationEventBus.Subscribe<ChatCoordinationEvents.ChatPanelClickOutsideEvent>(HandleClickOutside));
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

        private void HandleClickInside(ChatCoordinationEvents.ChatPanelClickInsideEvent evt)
        {
            if (voiceChatPanelState.Value == VoiceChatPanelState.SELECTED) return;

            // Check if the click is specifically inside the voice chat panel
            foreach (RaycastResult result in evt.RaycastResults)
            {
                if (result.gameObject.transform.IsChildOf(view.transform))
                {
                    voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.SELECTED);
                    return;
                }
            }
        }

        private void HandleClickOutside(ChatCoordinationEvents.ChatPanelClickOutsideEvent evt)
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

            // Dispose event subscriptions
            foreach (var subscription in eventSubscriptions)
                subscription.Dispose();
            eventSubscriptions.Clear();
        }
    }
}
