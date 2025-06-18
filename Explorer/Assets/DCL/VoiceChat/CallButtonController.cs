using Cysharp.Threading.Tasks;
using DG.Tweening;
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
        private bool isClickedOnce = false;
        private OtherUserCallStatus otherUserStatus;
        private CancellationTokenSource cts;

        public CallButtonController(CallButtonView view, IVoiceChatCallStatusService voiceChatCallStatusService)
        {
            this.view = view;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.view.CallButton.onClick.AddListener(OnCallButtonClicked);
            cts = new CancellationTokenSource();
            voiceChatCallStatusService.StatusChanged += OnVoiceChatStatusChanged;
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
            WaitAndClosePopup(cts!.Token).Forget();

            if (isClickedOnce)
            {
                view.TooltipParent.gameObject.SetActive(false);
                isClickedOnce = false;
            }
            else
            {
                isClickedOnce = true;

                if (voiceChatCallStatusService.Status is VoiceChatStatus.VOICE_CHAT_IN_CALL or VoiceChatStatus.VOICE_CHAT_STARTED_CALL or VoiceChatStatus.VOICE_CHAT_STARTING_CALL)
                {
                    EnableTooltipParent(cts.Token);
                    view.TooltipText.text = OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT;
                    return;
                }

                switch (otherUserStatus)
                {
                    case OtherUserCallStatus.USER_OFFLINE:
                        EnableTooltipParent(cts.Token);
                        view.TooltipText.text = USER_OFFLINE_TOOLTIP_TEXT;
                        break;
                    case OtherUserCallStatus.USER_AVAILABLE:
                        view.TooltipParent.gameObject.SetActive(false);
                        isClickedOnce = false;
                        StartCall?.Invoke(CurrentUserId);
                        break;
                    case OtherUserCallStatus.OWN_USER_IN_CALL:
                        EnableTooltipParent(cts.Token);
                        view.TooltipText.text = OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT;
                        break;
                    case OtherUserCallStatus.USER_REJECTS_CALLS:
                        EnableTooltipParent(cts.Token);
                        view.TooltipText.text = USER_REJECTS_CALLS_TOOLTIP_TEXT;
                        break;
                    case OtherUserCallStatus.OWN_USER_REJECTS_CALLS:
                        EnableTooltipParent(cts.Token);
                        view.TooltipText.text = OWN_USER_REJECTS_CALLS_TOOLTIP_TEXT;
                        break;
                }
            }
        }

        private async UniTaskVoid WaitAndClosePopup(CancellationToken ct)
        {
            await UniTask.Delay(WAIT_TIME_BEFORE_TOOLTIP_CLOSES_MS, cancellationToken: ct);
            view.TooltipParentCanvas.DOFade(0, ANIMATION_DURATION).OnComplete(() =>
            {
                view.TooltipParent.gameObject.SetActive(false);
                view.TooltipParentCanvas.interactable = false;
                view.TooltipParentCanvas.blocksRaycasts = false;
                isClickedOnce = false;
            });
        }

        private void EnableTooltipParent(CancellationToken ct)
        {
            view.TooltipParentCanvas.alpha = 0;
            view.TooltipParent.gameObject.SetActive(true);
            view.TooltipParentCanvas.interactable = true;
            view.TooltipParentCanvas.blocksRaycasts = true;
            view.TooltipParentCanvas.DOFade(1, ANIMATION_DURATION).ToUniTask(cancellationToken: ct);
        }

        private void OnVoiceChatStatusChanged(VoiceChatStatus newStatus)
        {
            if (isClickedOnce && newStatus == VoiceChatStatus.VOICE_CHAT_USER_BUSY)
            {
                EnableTooltipParent(cts.Token);
                view.TooltipText.text = USER_ALREADY_IN_CALL_TOOLTIP_TEXT;
            }
        }

        public void Dispose()
        {
            voiceChatCallStatusService.StatusChanged -= OnVoiceChatStatusChanged;
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
