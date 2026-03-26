using Cysharp.Threading.Tasks;
using DCL.UI.SharedSpaceManager;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.VoiceChat.Proximity
{
    public class NearbyVoiceWidgetView : ViewBaseWithAnimationElement, IView, IPanelInSharedSpace<ControllerNoData>
    {
        [field: SerializeField] public Toggle HearOthersToggle { get; private set; } = null!;
        [field: SerializeField] public Slider VolumeSlider { get; private set; } = null!;
        [field: SerializeField] public Button SpeakButton { get; private set; } = null!;
        [field: SerializeField] internal Button closeAreaButton { get; private set; } = null!;

        public event Action? Closed;
        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public bool IsVisibleInSharedSpace => gameObject.activeSelf;

        private CancellationTokenSource showingCts = new ();

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ControllerNoData parameters)
        {
            gameObject.SetActive(true);
            await PlayShowAnimationAsync(ct);
            ViewShowingComplete?.Invoke(this);
            showingCts = showingCts.SafeRestart();
            await UniTask.WaitUntilCanceled(showingCts.Token);
            await HideAsync(ct);
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            showingCts = showingCts.SafeRestart();

            await UniTask.WaitUntil(() => !gameObject.activeSelf, PlayerLoopTiming.Update, ct);
        }

        private void Awake()
        {
            closeAreaButton?.onClick.AddListener(OnCloseAreaButtonClicked);
        }

        private void OnCloseAreaButtonClicked()
        {
            showingCts = showingCts.SafeRestart();
            Closed?.Invoke();
        }
    }
}
