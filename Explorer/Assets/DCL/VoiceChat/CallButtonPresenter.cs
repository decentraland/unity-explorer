using Cysharp.Threading.Tasks;
using DCL.Chat.EventBus;
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
            USER_OFFLINE,
            USER_REJECTS_CALLS,
            USER_AVAILABLE,
            OWN_USER_IN_CALL,
            OWN_USER_REJECTS_CALLS,
        }

        private const string USER_OFFLINE_TOOLTIP_TEXT = "[User] is offline.";
        private const string USER_REJECTS_CALLS_TOOLTIP_TEXT = "[User] only accepts calls from friends.";
        private const string OWN_USER_REJECTS_CALLS_TOOLTIP_TEXT = "Add [User] as a friend, or update your \n <u><b>DM & Call settings</u></b> to connect with everyone.";
        private const string USER_ALREADY_IN_CALL_TOOLTIP_TEXT = "[User] is in another call.";
        private const string OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT = "End your current call to start a new one.";
        private const string COMMUNITY_CALL_ACTIVE_TOOLTIP_TEXT = "You are in a community call. End it to start a private call.";
        private const float ANIMATION_DURATION = 0.5f;
        private const int WAIT_TIME_BEFORE_TOOLTIP_CLOSES_MS = 4000;

        private readonly IDisposable? statusSubscription;
        private readonly IDisposable? orchestratorTypeSubscription;
        private readonly IDisposable? privateVoiceChatAvailableSubscription;
        private readonly IDisposable? currentChannelSubscription;

        private readonly CallButtonView view;
        private readonly IPrivateCallOrchestrator privateCallOrchestrator;
        private readonly IChatEventBus chatEventBus;

        private bool isClickedOnce;
        private OtherUserCallStatus otherUserStatus;
        private CancellationTokenSource cts;
        private string currentUserId = string.Empty;
        private string currentUserName = string.Empty;


        public CallButtonPresenter(
            CallButtonView view,
            IPrivateCallOrchestrator privateCallOrchestrator,
            IChatEventBus chatEventBus,
            IReadonlyReactiveProperty<ChatChannel> currentChannel)
        {
            this.view = view;
            this.privateCallOrchestrator = privateCallOrchestrator;
            this.chatEventBus = chatEventBus;
            this.view.CallButton.onClick.AddListener(OnCallButtonClicked);
            cts = new CancellationTokenSource();

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT))
            {
                statusSubscription = privateCallOrchestrator.CurrentCallStatus.Subscribe(OnVoiceChatStatusChanged);
                currentChannelSubscription = currentChannel.Subscribe(OnCurrentChannelChanged);
                chatEventBus.StartCall += OnChatEventBusStartCall;
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
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT)) return;

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
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT)) return;

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
            if (privateCallOrchestrator.CurrentVoiceChatType.Value == VoiceChatType.COMMUNITY)
            {
                await ShowTooltipWithAutoCloseAsync(COMMUNITY_CALL_ACTIVE_TOOLTIP_TEXT, ct);
                return;
            }

            // Check if we're already in a call
            if (privateCallOrchestrator.CurrentCallStatus.Value is
                VoiceChatStatus.VOICE_CHAT_IN_CALL or
                VoiceChatStatus.VOICE_CHAT_STARTED_CALL or
                VoiceChatStatus.VOICE_CHAT_STARTING_CALL)
            {
                await ShowTooltipWithAutoCloseAsync(OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT, ct);
                return;
            }

            switch (otherUserStatus)
            {
                case OtherUserCallStatus.USER_OFFLINE:
                    await ShowTooltipWithAutoCloseAsync(USER_OFFLINE_TOOLTIP_TEXT, ct);
                    break;
                case OtherUserCallStatus.USER_AVAILABLE:
                    // For available users, immediately start call without showing tooltip
                    view.TooltipParent.gameObject.SetActive(false);
                    isClickedOnce = false;
                    privateCallOrchestrator.StartCall(new Web3Address(currentUserId), VoiceChatType.PRIVATE);
                    break;
                case OtherUserCallStatus.OWN_USER_IN_CALL:
                    await ShowTooltipWithAutoCloseAsync(OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT, ct);
                    break;
                case OtherUserCallStatus.USER_REJECTS_CALLS:
                    await ShowTooltipWithAutoCloseAsync(USER_REJECTS_CALLS_TOOLTIP_TEXT, ct);
                    break;
                case OtherUserCallStatus.OWN_USER_REJECTS_CALLS:
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

            tooltipText = tooltipText.Replace("User", currentUserName);
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
            if (newStatus == VoiceChatStatus.VOICE_CHAT_BUSY)
                ShowTooltipWithAutoCloseAsync(USER_ALREADY_IN_CALL_TOOLTIP_TEXT, cts.Token).Forget();
        }

        public void Dispose()
        {
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT)) return;

            statusSubscription?.Dispose();
            orchestratorTypeSubscription?.Dispose();
            privateVoiceChatAvailableSubscription?.Dispose();
            currentChannelSubscription?.Dispose();
            chatEventBus.StartCall -= OnChatEventBusStartCall;
            view.CallButton.onClick.RemoveListener(OnCallButtonClicked);
        }
    }
}
