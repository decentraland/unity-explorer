using Cysharp.Threading.Tasks;
using DCL.Prefs;
using DCL.RealmNavigation;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat.Nearby
{
    public class NearbyVoiceTipController : IDisposable
    {
        private readonly NearbyVoiceTipView view;
        private readonly Action? onTryItNow;
        private CancellationTokenSource? cts;
        private bool subscribed;

        public NearbyVoiceTipController(NearbyVoiceTipView view, Action? onTryItNow, ILoadingStatus loadingStatus)
        {
            this.view = view;
            this.onTryItNow = onTryItNow;

            if (DCLPlayerPrefs.GetBool(DCLPrefKeys.NEARBY_VOICE_TIP_DISMISSED))
            {
                view.Hide();
                return;
            }

            view.Hide();
            cts = new CancellationTokenSource();
            WaitAndShowAsync(loadingStatus, cts.Token).Forget();
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();

            if (!subscribed)
                return;

            view.CloseButton.onClick.RemoveListener(OnClose);
            view.TryItNowButton.onClick.RemoveListener(OnTryItNow);
            subscribed = false;
        }

        private async UniTaskVoid WaitAndShowAsync(ILoadingStatus loadingStatus, CancellationToken ct)
        {
            await UniTask.WaitUntil(
                () => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed,
                cancellationToken: ct);

            if (ct.IsCancellationRequested)
                return;

            view.Show();
            view.CloseButton.onClick.AddListener(OnClose);
            view.TryItNowButton.onClick.AddListener(OnTryItNow);
            subscribed = true;
        }

        private void Dismiss()
        {
            DCLPlayerPrefs.SetBool(DCLPrefKeys.NEARBY_VOICE_TIP_DISMISSED, true, save: true);
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
