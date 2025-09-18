using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using Decentraland.SocialService.V2;
using System;
using System.Threading;
using Utility;
using Notifications = DCL.NotificationsBusController.NotificationsBus;

namespace DCL.VoiceChat
{
    public class VoiceChatOrchestrator : IDisposable, IVoiceChatOrchestrator
    {
        private const string TAG = nameof(VoiceChatOrchestrator);

        private readonly IPrivateVoiceChatCallStatusService privateVoiceChatCallStatusService;
        private readonly ICommunityVoiceChatCallStatusService communityVoiceChatCallStatusService;
        private readonly SceneVoiceChatTrackerService sceneVoiceChatTrackerService;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IChatEventBus chatEventBus;

        private readonly IDisposable? privateStatusSubscription;
        private readonly IDisposable? communityStatusSubscription;

        private readonly ReactiveProperty<VoiceChatType> currentVoiceChatType = new (VoiceChatType.NONE);
        private readonly ReactiveProperty<VoiceChatStatus> currentCallStatus = new (VoiceChatStatus.DISCONNECTED);
        private readonly ReactiveProperty<VoiceChatPanelSize> currentVoiceChatPanelSize = new (VoiceChatPanelSize.DEFAULT);
        private readonly ReactiveProperty<ActiveCommunityVoiceChat?> currentActiveCommunityData = new (null);

        private IVoiceChatCallStatusServiceBase? activeCallStatusService;
        private IVoiceChatOrchestrator? voiceChatOrchestratorImplementation;
        private CancellationTokenSource joinCallCts = new ();

        public IReadonlyReactiveProperty<VoiceChatType> CurrentVoiceChatType => currentVoiceChatType;
        public IReadonlyReactiveProperty<VoiceChatStatus> CurrentCallStatus => currentCallStatus;
        public IReadonlyReactiveProperty<VoiceChatPanelSize> CurrentVoiceChatPanelSize => currentVoiceChatPanelSize;
        public IReadonlyReactiveProperty<ActiveCommunityVoiceChat?> CurrentSceneActiveCommunityVoiceChatData => currentActiveCommunityData;
        public IReadonlyReactiveProperty<string> CurrentCommunityId => communityVoiceChatCallStatusService.CallId;
        public string CurrentConnectionUrl => activeCallStatusService?.ConnectionUrl ?? string.Empty;
        public VoiceChatParticipantsStateService ParticipantsStateService { get; }

        public IReadonlyReactiveProperty<VoiceChatStatus> CommunityCallStatus => communityVoiceChatCallStatusService.Status;

        public IReadonlyReactiveProperty<VoiceChatStatus> PrivateCallStatus => privateVoiceChatCallStatusService.Status;

        public string CurrentTargetWallet => privateVoiceChatCallStatusService.CurrentTargetWallet;

        public VoiceChatOrchestrator(
            IPrivateVoiceChatCallStatusService privateVoiceChatCallStatusService,
            ICommunityVoiceChatCallStatusService communityVoiceChatCallStatusService,
            VoiceChatParticipantsStateService participantsStateService,
            SceneVoiceChatTrackerService sceneVoiceChatTrackerService, ISharedSpaceManager sharedSpaceManager, IChatEventBus chatEventBus)
        {
            this.privateVoiceChatCallStatusService = privateVoiceChatCallStatusService;
            this.communityVoiceChatCallStatusService = communityVoiceChatCallStatusService;
            this.sceneVoiceChatTrackerService = sceneVoiceChatTrackerService;
            this.sharedSpaceManager = sharedSpaceManager;
            this.chatEventBus = chatEventBus;
            ParticipantsStateService = participantsStateService;

            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT)) return;

            Notifications.NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.COMMUNITY_VOICE_CHAT_STARTED, OnClickedNotification);

            privateVoiceChatCallStatusService.PrivateVoiceChatUpdateReceived += OnPrivateVoiceChatUpdateReceived;
            sceneVoiceChatTrackerService.ActiveVoiceChatDetectedInScene += OnActiveVoiceChatDetectedInScene;
            sceneVoiceChatTrackerService.ActiveVoiceChatStoppedInScene += OnActiveVoiceChatStoppedInScene;

            privateStatusSubscription = privateVoiceChatCallStatusService.Status.Subscribe(OnPrivateVoiceChatStatusChanged);
            communityStatusSubscription = communityVoiceChatCallStatusService.Status.Subscribe(OnCommunityVoiceChatStatusChanged);
        }

        public void Dispose()
        {
            privateVoiceChatCallStatusService.PrivateVoiceChatUpdateReceived -= OnPrivateVoiceChatUpdateReceived;
            sceneVoiceChatTrackerService.ActiveVoiceChatDetectedInScene -= OnActiveVoiceChatDetectedInScene;
            sceneVoiceChatTrackerService.ActiveVoiceChatStoppedInScene -= OnActiveVoiceChatStoppedInScene;

            privateStatusSubscription?.Dispose();
            communityStatusSubscription?.Dispose();

            currentVoiceChatType.ClearSubscriptionsList();
            currentCallStatus.ClearSubscriptionsList();
            currentVoiceChatPanelSize.ClearSubscriptionsList();
            currentActiveCommunityData.ClearSubscriptionsList();
            ParticipantsStateService.Dispose();
        }

        private void OnClickedNotification(object[] parameters)
        {
            if (parameters.Length == 0 || parameters[0] is not CommunityVoiceChatStartedNotification)
                return;

            var notification = (CommunityVoiceChatStartedNotification)parameters[0];

            ShowCommunityInChatAndJoinAsync().Forget();
            return;

            async UniTaskVoid ShowCommunityInChatAndJoinAsync()
            {
                await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true));
                chatEventBus.OpenCommunityConversationUsingCommunityId(notification.CommunityId);
                JoinCommunityVoiceChat(notification.CommunityId, true);
            }

        }

        public void StartCall(string callId, VoiceChatType callType)
        {
            if (VoiceChatCallTypeValidator.IsNoActiveCall(currentVoiceChatType.Value))
            {
                SetActiveCallService(callType);
                activeCallStatusService?.StartCall(callId);
            }
        }

        public void AcceptPrivateCall()
        {
            if (VoiceChatCallTypeValidator.IsPrivateCall(currentVoiceChatType.Value))
                privateVoiceChatCallStatusService.AcceptCall();
        }

        public void HangUp() =>
            activeCallStatusService?.HangUp();

        public void RejectCall()
        {
            if (VoiceChatCallTypeValidator.IsPrivateCall(currentVoiceChatType.Value))
                privateVoiceChatCallStatusService.RejectCall();
        }

        public void HandleConnectionEnded()
        {
            communityVoiceChatCallStatusService.HandleLivekitConnectionEnded();
            privateVoiceChatCallStatusService.HandleLivekitConnectionEnded();
        }

        public void HandleConnectionError()
        {
            activeCallStatusService?.HandleLivekitConnectionFailed();
        }

        private void OnPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update)
        {
            if (currentVoiceChatType.Value != VoiceChatType.COMMUNITY)
            {
                SetActiveCallService(VoiceChatType.PRIVATE);
                privateVoiceChatCallStatusService.OnPrivateVoiceChatUpdateReceived(update);
            }
        }

        private void OnPrivateVoiceChatStatusChanged(VoiceChatStatus status)
        {
            // Update call status if we're already in a private call
            if (currentVoiceChatType.Value == VoiceChatType.PRIVATE) { currentCallStatus.Value = status; }

            // Handle transitions to/from private call
            if (status.IsNotConnected())
            {
                if (currentVoiceChatType.Value == VoiceChatType.PRIVATE) SetActiveCallService(VoiceChatType.NONE);
            }
            else if (status is VoiceChatStatus.VOICE_CHAT_STARTING_CALL
                     or VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL
                     or VoiceChatStatus.VOICE_CHAT_STARTED_CALL
                     or VoiceChatStatus.VOICE_CHAT_IN_CALL)
            {
                SetActiveCallService(VoiceChatType.PRIVATE);
                currentCallStatus.Value = status;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Switched Orchestrator state to {currentVoiceChatType.Value}");
        }

        private void OnActiveVoiceChatDetectedInScene(ActiveCommunityVoiceChat activeCommunityData)
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Active voice chat detected in scene for community: {activeCommunityData.communityName} ({activeCommunityData.communityId})");
            currentActiveCommunityData.Value = activeCommunityData;
        }

        private void OnActiveVoiceChatStoppedInScene()
        {
            currentActiveCommunityData.Value = null;
        }

        private void OnCommunityVoiceChatStatusChanged(VoiceChatStatus status)
        {
            // Update call status if we're already in a community call
            if (currentVoiceChatType.Value == VoiceChatType.COMMUNITY) { currentCallStatus.Value = status; }

            // Handle transitions to/from community call
            if (status.IsNotConnected())
            {
                if (currentVoiceChatType.Value == VoiceChatType.COMMUNITY) SetActiveCallService(VoiceChatType.NONE);
            }
            else if (status is VoiceChatStatus.VOICE_CHAT_STARTING_CALL
                     or VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL
                     or VoiceChatStatus.VOICE_CHAT_STARTED_CALL
                     or VoiceChatStatus.VOICE_CHAT_IN_CALL)
            {
                SetActiveCallService(VoiceChatType.COMMUNITY);
                currentCallStatus.Value = status;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Switched Orchestrator state to {currentVoiceChatType.Value}");
        }

        private void SetActiveCallService(VoiceChatType newType)
        {
            currentVoiceChatType.UpdateValue(newType);

            switch (newType)
            {
                case VoiceChatType.NONE:
                    activeCallStatusService = null;
                    break;
                case VoiceChatType.PRIVATE:
                    activeCallStatusService = privateVoiceChatCallStatusService;
                    break;
                case VoiceChatType.COMMUNITY:
                    activeCallStatusService = communityVoiceChatCallStatusService;
                    break;
            }
        }

        public void ChangePanelSize(VoiceChatPanelSize panelSize)
        {
            currentVoiceChatPanelSize.Value = panelSize;
        }

        public void JoinCommunityVoiceChat(string communityId, bool force = false)
        {
            joinCallCts = joinCallCts.SafeRestart();

            if (!VoiceChatCallTypeValidator.IsNoActiveCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.JoinCommunityVoiceChatAsync(communityId, joinCallCts.Token).Forget();
            else if (force)
                HangUpAndStartCallAsync().Forget();

            return;

            async UniTaskVoid HangUpAndStartCallAsync()
            {
                HangUp();
                await UniTask.Delay(100, cancellationToken: joinCallCts.Token);

                if (!joinCallCts.IsCancellationRequested)
                    communityVoiceChatCallStatusService.JoinCommunityVoiceChatAsync(communityId, joinCallCts.Token).Forget();
            }
        }

        public void RequestToSpeakInCurrentCall()
        {
            if (VoiceChatCallTypeValidator.IsCommunityCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.RequestToSpeakInCurrentCall();
        }

        public void LowerHandInCurrentCall()
        {
            if (VoiceChatCallTypeValidator.IsCommunityCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.LowerHandInCurrentCall();
        }

        public void PromoteToSpeakerInCurrentCall(string walletId)
        {
            if (VoiceChatCallTypeValidator.IsCommunityCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.PromoteToSpeakerInCurrentCall(walletId);
        }

        public void DenySpeakerInCurrentCall(string walletId)
        {
            if (VoiceChatCallTypeValidator.IsCommunityCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.DenySpeakerInCurrentCall(walletId);
        }

        public void DemoteFromSpeakerInCurrentCall(string walletId)
        {
            if (VoiceChatCallTypeValidator.IsCommunityCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.DemoteFromSpeakerInCurrentCall(walletId);
        }

        public void KickPlayerFromCurrentCall(string walletId)
        {
            if (VoiceChatCallTypeValidator.IsCommunityCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.KickPlayerFromCurrentCall(walletId);
        }

        public void EndStreamInCurrentCall()
        {
            if (VoiceChatCallTypeValidator.IsCommunityCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.EndStreamInCurrentCall();
        }

        public bool HasActiveVoiceChatCall(string communityId) =>
            communityVoiceChatCallStatusService.HasActiveVoiceChatCall(communityId);

        public bool TryGetActiveCommunityData(string communityId, out ActiveCommunityVoiceChat activeCommunityData) =>
            communityVoiceChatCallStatusService.TryGetActiveCommunityVoiceChat(communityId, out activeCommunityData);

        public ReactiveProperty<bool>? SubscribeToCommunityUpdates(string communityId) =>
            communityVoiceChatCallStatusService.SubscribeToCommunityUpdates(communityId);
    }
}
