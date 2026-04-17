using Cysharp.Threading.Tasks;
using DCL.Prefs;
using DCL.RealmNavigation;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat.UI
{
    public class NearbyVoiceTipController : IDisposable
    {
        private readonly NearbyVoiceTipView view;
        private readonly CancellationTokenSource? cts;

        private readonly Action? onTryItNow;

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
            view.Hide();
            cts.SafeCancelAndDispose();

            view.CloseButton.onClick.RemoveListener(OnClose);
            view.TryItNowButton.onClick.RemoveListener(OnTryItNow);
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
        }

        private void OnClose()
        {
            DCLPlayerPrefs.SetBool(DCLPrefKeys.NEARBY_VOICE_TIP_DISMISSED, true, save: true);
            Dispose();
        }

        private void OnTryItNow()
        {
            onTryItNow?.Invoke();
            OnClose();
        }
    }
}
