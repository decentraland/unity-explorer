using DCL.Prefs;
using System;

namespace DCL.VoiceChat.Proximity
{
    public class ProximityVoiceTipController : IDisposable
    {
        private readonly ProximityVoiceTipView view;
        private readonly Action? onTryItNow;
        private bool subscribed;

        public ProximityVoiceTipController(ProximityVoiceTipView view, Action? onTryItNow)
        {
            this.view = view;
            this.onTryItNow = onTryItNow;

            if (DCLPlayerPrefs.GetBool(DCLPrefKeys.PROXIMITY_VOICE_TIP_DISMISSED))
            {
                view.Hide();
                return;
            }

            view.Show();
            view.CloseButton.onClick.AddListener(OnClose);
            view.TryItNowButton.onClick.AddListener(OnTryItNow);
            subscribed = true;
        }

        public void Dispose()
        {
            if (!subscribed)
                return;

            view.CloseButton.onClick.RemoveListener(OnClose);
            view.TryItNowButton.onClick.RemoveListener(OnTryItNow);
            subscribed = false;
        }

        private void Dismiss()
        {
            DCLPlayerPrefs.SetBool(DCLPrefKeys.PROXIMITY_VOICE_TIP_DISMISSED, true, save: true);
            Dispose();
            view.Hide();
        }

        private void OnClose()
        {
            Dismiss();
        }

        private void OnTryItNow()
        {
            Dismiss();
            onTryItNow?.Invoke();
        }
    }
}
