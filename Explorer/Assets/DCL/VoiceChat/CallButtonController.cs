using Cysharp.Threading.Tasks;
using DG.Tweening;
using DCL.Chat.EventBus;
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
        private const float ANIMATION_DURATION = 0.5f;
        private const int WAIT_TIME_BEFORE_TOOLTIP_CLOSES_MS = 4000;

        public event Action<string> StartCall;
        public string CurrentUserId { get; private set; }

        private readonly CallButtonView view;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;
        private readonly IChatEventBus chatEventBus;
        private bool isClickedOnce = false;
        private OtherUserCallStatus otherUserStatus;
        private CancellationTokenSource cts;

        public CallButtonController(CallButtonView view, IVoiceChatCallStatusService voiceChatCallStatusService, IChatEventBus chatEventBus)
        {
            this.view = view;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.chatEventBus = chatEventBus;
            this.view.CallButton.onClick.AddListener(OnCallButtonClicked);
            cts = new CancellationTokenSource();
            voiceChatCallStatusService.StatusChanged += OnVoiceChatStatusChanged;
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
            HandleCallButtonClick(cts!.Token).Forget();
        }

        private async UniTaskVoid HandleCallButtonClick(CancellationToken ct)
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

            if (voiceChatCallStatusService.Status is VoiceChatStatus.VOICE_CHAT_IN_CALL or VoiceChatStatus.VOICE_CHAT_STARTED_CALL or VoiceChatStatus.VOICE_CHAT_STARTING_CALL)
            {
                await ShowTooltipWithAutoClose(OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT, ct);
                return;
            }

            switch (otherUserStatus)
            {
                case OtherUserCallStatus.USER_OFFLINE:
                    await ShowTooltipWithAutoClose(USER_OFFLINE_TOOLTIP_TEXT, ct);
                    break;
                case OtherUserCallStatus.USER_AVAILABLE:
                    // For available users, immediately start call without showing tooltip
                    view.TooltipParent.gameObject.SetActive(false);
                    isClickedOnce = false;
                    StartCall?.Invoke(CurrentUserId);
                    break;
                case OtherUserCallStatus.OWN_USER_IN_CALL:
                    await ShowTooltipWithAutoClose(OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT, ct);
                    break;
                case OtherUserCallStatus.USER_REJECTS_CALLS:
                    await ShowTooltipWithAutoClose(USER_REJECTS_CALLS_TOOLTIP_TEXT, ct);
                    break;
                case OtherUserCallStatus.OWN_USER_REJECTS_CALLS:
                    await ShowTooltipWithAutoClose(OWN_USER_REJECTS_CALLS_TOOLTIP_TEXT, ct);
                    break;
            }
        }

        private async UniTask ShowTooltipWithAutoClose(string tooltipText, CancellationToken ct)
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
            if (newStatus == VoiceChatStatus.VOICE_CHAT_USER_BUSY)
            {
                ShowTooltipWithAutoClose(USER_ALREADY_IN_CALL_TOOLTIP_TEXT, cts.Token).Forget();
            }
        }

        public void Dispose()
        {
            voiceChatCallStatusService.StatusChanged -= OnVoiceChatStatusChanged;
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
