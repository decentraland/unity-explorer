using Cysharp.Threading.Tasks;
using DG.Tweening;
using DCL.Chat.EventBus;
using DCL.Utilities;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    public class CallButtonController
    {
        private const string USER_OFFLINE_TOOLTIP_TEXT = "User is offline.";
        private const string USER_REJECTS_CALLS_TOOLTIP_TEXT = "User only accepts calls from friends.";
        private const string OWN_USER_REJECTS_CALLS_TOOLTIP_TEXT = "Add User as a friend, or update your DM & Call settings to connect with everyone.";
        private const string USER_ALREADY_IN_CALL_TOOLTIP_TEXT = "User is in another call.";
        private const string OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT = "End your current call to start a new one.";
        private const string COMMUNITY_CALL_ACTIVE_TOOLTIP_TEXT = "You are in a community call. End it to start a private call.";
        private const float ANIMATION_DURATION = 0.5f;
        private const int WAIT_TIME_BEFORE_TOOLTIP_CLOSES_MS = 4000;

        private readonly IDisposable statusSubscription;
        private readonly IDisposable orchestratorTypeSubscription;
        private readonly IDisposable privateVoiceChatAvailableSubscription;

        public event Action<string> StartCall;
        public string CurrentUserId { get; private set; }

        private readonly CallButtonView view;
        private readonly IVoiceChatOrchestratorState voiceChatState;
        private readonly IChatEventBus chatEventBus;
        private bool isClickedOnce;
        private OtherUserCallStatus otherUserStatus;
        private CancellationTokenSource cts;

        public CallButtonController(CallButtonView view, IVoiceChatOrchestratorState voiceChatState, IChatEventBus chatEventBus)
        {
            this.view = view;
            this.voiceChatState = voiceChatState;
            this.chatEventBus = chatEventBus;
            this.view.CallButton.onClick.AddListener(OnCallButtonClicked);
            cts = new CancellationTokenSource();

            statusSubscription = voiceChatState.CurrentCallStatus.Subscribe(OnVoiceChatStatusChanged);

            // We might want to start the call directly here. And let the orchestrator handle the states.
            // But we will need to handle the parent view so it closes after the button is pressed and the call is successfully established (in case of Passport, etc.)
            chatEventBus.StartCall += OnChatEventBusStartCall;
        }

        private void OnChatEventBusStartCall()
        {
            OnCallButtonClicked();
        }

        public void Reset()
        {
            if (!PlayerLoopHelper.IsMainThread)
                ResetAsync().Forget();
            else
                view.TooltipParent.gameObject.SetActive(false);

            isClickedOnce = false;
        }

        private async UniTaskVoid ResetAsync()
        {
            await UniTask.SwitchToMainThread();
            view.TooltipParent.gameObject.SetActive(false);
        }

        public void SetCallStatusForUser(OtherUserCallStatus status, string userId)
        {
            CurrentUserId = userId;
            otherUserStatus = status;
            Reset();
        }

        private void OnCallButtonClicked()
        {
            cts = cts?.SafeRestart();
            HandleCallButtonClickAsync(cts!.Token).Forget();
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
            if (voiceChatState.CurrentVoiceChatType.Value == VoiceChatType.COMMUNITY)
            {
                await ShowTooltipWithAutoCloseAsync(COMMUNITY_CALL_ACTIVE_TOOLTIP_TEXT, ct);
                return;
            }

            // Check if we're already in a call
            if (voiceChatState.CurrentCallStatus.Value is VoiceChatStatus.VOICE_CHAT_IN_CALL or VoiceChatStatus.VOICE_CHAT_STARTED_CALL or VoiceChatStatus.VOICE_CHAT_STARTING_CALL)
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
                    StartCall?.Invoke(CurrentUserId);
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
            //This state comes after a call to BE, so we won't show any tooltip until the reply arrives.
            //DO we need to add some loading/calling animation here??
            if (newStatus == VoiceChatStatus.VOICE_CHAT_BUSY)
            {
                ShowTooltipWithAutoCloseAsync(USER_ALREADY_IN_CALL_TOOLTIP_TEXT, cts.Token).Forget();
            }
        }

        public void Dispose()
        {
            statusSubscription?.Dispose();
            orchestratorTypeSubscription?.Dispose();
            privateVoiceChatAvailableSubscription?.Dispose();
            chatEventBus.StartCall -= OnChatEventBusStartCall;
            view.CallButton.onClick.RemoveListener(OnCallButtonClicked);
        }

        public enum OtherUserCallStatus
        {
            USER_OFFLINE,
            USER_REJECTS_CALLS,
            USER_AVAILABLE,
            OWN_USER_IN_CALL,
            OWN_USER_REJECTS_CALLS
        }
    }
}
