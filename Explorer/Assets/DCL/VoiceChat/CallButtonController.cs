using System;

namespace DCL.VoiceChat
{
    public class CallButtonController
    {
        private const string USER_OFFLINE_TOOLTIP_TEXT = "The user you are trying to call is offline.";
        private const string USER_REJECTS_CALLS_TOOLTIP_TEXT = "This user accepts calls from friends only.";
        private const string USER_ALREADY_IN_CALL_TOOLTIP_TEXT = "The user is in another call. Please try again later.";
        private const string OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT = "You're already on a call. Starting a new one will end it. Click the button again to confirm. Click anywhere else to cancel.";

        public event Action<string> StartCall;
        public string CurrentUserId { get; private set; }

        private readonly CallButtonView view;
        private bool isClickedOnce = false;
        private OtherUserCallStatus otherUserStatus;

        public CallButtonController(CallButtonView view)
        {
            this.view = view;
            this.view.CallButton.onClick.AddListener(OnCallButtonClicked);
        }

        public void SetCallButtonVisibility(bool isVisible)
        {
            view.gameObject.SetActive(isVisible);
            view.TooltipParent.gameObject.SetActive(false);
            isClickedOnce = false;
        }

        public void Reset()
        {
            view.TooltipParent.gameObject.SetActive(false);
            isClickedOnce = false;
        }

        public void SetCallStatusForUser(OtherUserCallStatus status, string userId)
        {
            CurrentUserId = userId;
            otherUserStatus = status;
            Reset();
        }

        private void OnCallButtonClicked()
        {
            if (isClickedOnce)
            {
                view.TooltipParent.gameObject.SetActive(false);
                isClickedOnce = false;
                switch (otherUserStatus)
                {
                    case OtherUserCallStatus.OWN_USER_IN_CALL:
                        StartCall?.Invoke(CurrentUserId);
                        break;
                }
            }
            else
            {
                isClickedOnce = true;

                switch (otherUserStatus)
                {
                    case OtherUserCallStatus.USER_OFFLINE:
                        view.TooltipParent.gameObject.SetActive(true);
                        view.TooltipText.text = USER_OFFLINE_TOOLTIP_TEXT;
                        break;
                    case OtherUserCallStatus.USER_AVAILABLE:
                        view.TooltipParent.gameObject.SetActive(false);
                        isClickedOnce = false;
                        StartCall?.Invoke(CurrentUserId);
                        break;
                    case OtherUserCallStatus.USER_IN_CALL:
                        view.TooltipParent.gameObject.SetActive(true);
                        view.TooltipText.text = USER_ALREADY_IN_CALL_TOOLTIP_TEXT;
                        break;
                    case OtherUserCallStatus.OWN_USER_IN_CALL:
                        view.TooltipParent.gameObject.SetActive(true);
                        view.TooltipText.text = OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT;
                        break;
                    case OtherUserCallStatus.USER_REJECTS_CALLS:
                        view.TooltipParent.gameObject.SetActive(true);
                        view.TooltipText.text = USER_REJECTS_CALLS_TOOLTIP_TEXT;
                        break;
                }
            }
        }

        public enum OtherUserCallStatus
        {
            USER_OFFLINE,
            USER_REJECTS_CALLS,
            USER_AVAILABLE,
            USER_IN_CALL,
            OWN_USER_IN_CALL
        }
    }
}
