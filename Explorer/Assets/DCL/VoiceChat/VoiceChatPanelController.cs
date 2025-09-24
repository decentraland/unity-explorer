using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.VoiceChat.CommunityVoiceChat;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Pool;

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
        private readonly IReadonlyReactiveProperty<VoiceChatPanelState> voiceChatPanelState;

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
            voiceChatPanelState = voiceChatOrchestrator.CurrentVoiceChatPanelState;

            DCLInput.Instance.UI.Click.performed += HandleGlobalClick;

            view.PointerEnterChatArea += OnPointerEnterChatArea;
            view.PointerExitChatArea += OnPointerExitChatArea;
            view.PointerClick += OnPointerClick;
            view.PointerClickChatArea += OnPointerClickChatArea;
        }

        private void OnPointerClickChatArea()
        {
            if (voiceChatPanelState.Value is VoiceChatPanelState.UNFOCUSED or VoiceChatPanelState.FOCUSED)
                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.SELECTED);
        }

        private void OnPointerClick()
        {
            voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.SELECTED);
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

        private void HandleGlobalClick(InputAction.CallbackContext context)
        {
            if (EventSystem.current == null) return;
            if (voiceChatPanelState.Value != VoiceChatPanelState.SELECTED) return;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = GetPointerPosition(context)
            };

            using PooledObject<List<RaycastResult>> _ = ListPool<RaycastResult>.Get(out List<RaycastResult>? results);

            EventSystem.current.RaycastAll(eventData, results);

            foreach (RaycastResult result in results)
            {
                if (result.gameObject.transform.IsChildOf(view.transform))
                {
                    return;
                }
            }

            WaitAndChange().Forget();
            return;

            async UniTaskVoid WaitAndChange()
            {
                //We wait in case we clicked on the chat, so it has time to update the state before trying to unfocus
                await UniTask.Delay(5);
                if (voiceChatPanelState.Value == VoiceChatPanelState.FOCUSED) return;

                voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.UNFOCUSED);
            }
        }

        private static Vector2 GetPointerPosition(InputAction.CallbackContext ctx)
        {
            if (ctx.control is Pointer pointer) return pointer.position.ReadValue();
            if (Pointer.current != null) return Pointer.current.position.ReadValue();
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            if (Touchscreen.current?.primaryTouch != null) return Touchscreen.current.primaryTouch.position.ReadValue();

            return Vector2.zero;
        }
        public void Dispose()
        {
            privateVoiceChatController?.Dispose();
            communitiesVoiceChatController?.Dispose();
            sceneVoiceChatController?.Dispose();
            voiceChatPanelResizeController?.Dispose();
            DCLInput.Instance.UI.Click.performed -= HandleGlobalClick;
            view.PointerEnterChatArea -= OnPointerEnterChatArea;
            view.PointerExitChatArea -= OnPointerExitChatArea;
        }
    }
}
