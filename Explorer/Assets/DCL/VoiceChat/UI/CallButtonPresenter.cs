using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.History;
using DCL.FeatureFlags;
using DCL.Utilities;
using DCL.Web3;
using DG.Tweening;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    public class CallButtonPresenter
    {
        public enum OtherUserCallStatus
        {
            UserOffline,
            UserRejectsCalls,
            UserAvailable,
            OwnUserInCall,
            OwnUserRejectsCalls,
        }

        private const string USER_OFFLINE_TOOLTIP_TEXT = "[{0}] is offline.";
        private const string USER_REJECTS_CALLS_TOOLTIP_TEXT = "[{0} only accepts calls from friends.";
        private const string OWN_USER_REJECTS_CALLS_TOOLTIP_TEXT = "Add [{0}] as a friend, or update your \n <u><b>DM & Call settings</u></b> to connect with everyone.";
        private const string USER_ALREADY_IN_CALL_TOOLTIP_TEXT = "[{0}] is in another call.";
        private const string OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT = "End your current call to start a new one.";
        private const string COMMUNITY_CALL_ACTIVE_TOOLTIP_TEXT = "You are in a community call. End it to start a private call.";
        private const float ANIMATION_DURATION = 0.5f;
        private const int WAIT_TIME_BEFORE_TOOLTIP_CLOSES_MS = 4000;

        private readonly IDisposable? statusSubscription;
        private readonly IDisposable? orchestratorTypeSubscription;
        private readonly IDisposable? privateVoiceChatAvailableSubscription;
        private readonly IDisposable? currentChannelSubscription;
        private readonly IDisposable? startCallSubscription;

        private readonly CallButtonView view;
        private readonly IPrivateCallOrchestrator privateCallOrchestrator;
        private readonly ChatEventBus chatEventBus;

        private bool isClickedOnce;
        private OtherUserCallStatus otherUserStatus;
        private CancellationTokenSource cts;
        private string currentUserId = string.Empty;
        private string currentUserName = string.Empty;


        public CallButtonPresenter(
            CallButtonView view,
            IPrivateCallOrchestrator privateCallOrchestrator,
            ChatEventBus chatEventBus,
            IReadonlyReactiveProperty<ChatChannel> currentChannel)
        {
            this.view = view;
            this.privateCallOrchestrator = privateCallOrchestrator;
            this.chatEventBus = chatEventBus;
            this.view.CallButton.onClick.AddListener(OnCallButtonClicked);
            cts = new CancellationTokenSource();

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.VoiceChat))
            {
                statusSubscription = privateCallOrchestrator.CurrentCallStatus.Subscribe(OnVoiceChatStatusChanged);
                currentChannelSubscription = currentChannel.Subscribe(OnCurrentChannelChanged);
                startCallSubscription = chatEventBus.Subscribe<ChatEvents.StartCallEvent>(_ => OnChatEventBusStartCall());
            }

            view.gameObject.SetActive(false);
        }

        private void OnCurrentChannelChanged(ChatChannel newChannel)
        {
            bool shouldShowButton = newChannel.ChannelType == ChatChannel.ChatChannelType.USER;
            view.gameObject.SetActive(shouldShowButton);
        }

        private void OnChatEventBusStartCall()
        {
            OnCallButtonClicked();
        }

        private void Reset()
        {
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.VoiceChat)) return;

            if (!PlayerLoopHelper.IsMainThread)
                ResetAsync().Forget();
            else
                view.TooltipParent.gameObject.SetActive(false);

            isClickedOnce = false;
            return;

            async UniTaskVoid ResetAsync()
            {
                await UniTask.SwitchToMainThread();
                view.TooltipParent.gameObject.SetActive(false);
            }
        }


        public void SetCallStatusForUser(OtherUserCallStatus status, string userId, string userName)
        {
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.VoiceChat)) return;

            currentUserName = userName;
            currentUserId = userId;
            otherUserStatus = status;
            Reset();
        }

        private void OnCallButtonClicked()
        {
            cts = cts.SafeRestart();
            HandleCallButtonClickAsync(cts.Token).Forget();
        }

        private async UniTaskVoid HandleCallButtonClickAsync(CancellationToken ct)
        {
            if (isClickedOnce)
            {
                // If already clicked once, immediately hide tooltip and reset state
                view.TooltipParent.gameObject.SetActive(false);
                isClickedOnce = false;
                return;
            }

            // First click - set the flag and handle the logic
            isClickedOnce = true;

            // Check if we're in a community call first
            if (privateCallOrchestrator.CurrentVoiceChatType.Value == VoiceChatType.Community)
            {
                await ShowTooltipWithAutoCloseAsync(COMMUNITY_CALL_ACTIVE_TOOLTIP_TEXT, ct);
                return;
            }

            // Check if we're already in a call
            if (privateCallOrchestrator.CurrentCallStatus.Value is
                VoiceChatStatus.VoiceChatInCall or
                VoiceChatStatus.VoiceChatStartedCall or
                VoiceChatStatus.VoiceChatStartingCall)
            {
                await ShowTooltipWithAutoCloseAsync(OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT, ct);
                return;
            }

            switch (otherUserStatus)
            {
                case OtherUserCallStatus.UserOffline:
                    await ShowTooltipWithAutoCloseAsync(USER_OFFLINE_TOOLTIP_TEXT, ct);
                    break;
                case OtherUserCallStatus.UserAvailable:
                    // For available users, immediately start call without showing tooltip
                    view.TooltipParent.gameObject.SetActive(false);
                    isClickedOnce = false;
                    privateCallOrchestrator.StartCall(new Web3Address(currentUserId), VoiceChatType.Private);
                    break;
                case OtherUserCallStatus.OwnUserInCall:
                    await ShowTooltipWithAutoCloseAsync(OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT, ct);
                    break;
                case OtherUserCallStatus.UserRejectsCalls:
                    await ShowTooltipWithAutoCloseAsync(USER_REJECTS_CALLS_TOOLTIP_TEXT, ct);
                    break;
                case OtherUserCallStatus.OwnUserRejectsCalls:
                    await ShowTooltipWithAutoCloseAsync(OWN_USER_REJECTS_CALLS_TOOLTIP_TEXT, ct);
                    break;
            }
        }

        private async UniTask ShowTooltipWithAutoCloseAsync(string tooltipText, CancellationToken ct)
        {
            view.TooltipParentCanvas.alpha = 0;
            view.TooltipParent.gameObject.SetActive(true);
            view.TooltipParentCanvas.interactable = true;
            view.TooltipParentCanvas.blocksRaycasts = true;

            tooltipText = string.Format(tooltipText, currentUserName);
            view.TooltipText.text = tooltipText;

            await view.TooltipParentCanvas.DOFade(1, ANIMATION_DURATION).ToUniTask(cancellationToken: ct);
            await UniTask.Delay(WAIT_TIME_BEFORE_TOOLTIP_CLOSES_MS, cancellationToken: ct);
            await view.TooltipParentCanvas.DOFade(0, ANIMATION_DURATION).ToUniTask(cancellationToken: ct);
            view.TooltipParent.gameObject.SetActive(false);
            view.TooltipParentCanvas.interactable = false;
            view.TooltipParentCanvas.blocksRaycasts = false;
            isClickedOnce = false;
        }

        private void OnVoiceChatStatusChanged(VoiceChatStatus newStatus)
        {
            if (newStatus == VoiceChatStatus.VoiceChatBusy)
                ShowTooltipWithAutoCloseAsync(USER_ALREADY_IN_CALL_TOOLTIP_TEXT, cts.Token).Forget();
        }

        public void Dispose()
        {
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.VoiceChat)) return;

            statusSubscription?.Dispose();
            orchestratorTypeSubscription?.Dispose();
            privateVoiceChatAvailableSubscription?.Dispose();
            currentChannelSubscription?.Dispose();
            startCallSubscription?.Dispose();
            view.CallButton.onClick.RemoveListener(OnCallButtonClicked);
        }
    }
}
