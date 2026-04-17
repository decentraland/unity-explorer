using Cysharp.Threading.Tasks;
using DCL.UI;
using DCL.VoiceChat.Nearby;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.VoiceChat.UI
{
    public class NearbyVoiceChatButtonView : MonoBehaviour
    {
        [Serializable]
        public struct MetaStateSprites
        {
            public Sprite unselected;
            public Sprite hover;
        }

        [field: SerializeField] public Button CloseAreaButton { get; private set; } = null!;

        [Space]
        [SerializeField] private Button button = null!;
        [SerializeField] private Image unselectedImage = null!;
        [SerializeField] private Image hoverStateImage = null!;

        [Space]
        [SerializeField] private GameObject suppressedTooltip = null!;
        [field: SerializeField] public TMP_Text SuppressedText { get; private set; } = null!;

        [SerializeField] private ViewAnimationElementBase tooltipAnimation = null!;
        [SerializeField] private GameObject hoverTooltip = null!;
        [SerializeField] private GameObject greenDotImage = null!;

        [Space]
        [SerializeField] private SoundWaveAnimator soundWaveAnimator = null!;

        [Space]
        [SerializeField] private MetaStateSprites disconnectedSprites;
        [SerializeField] private MetaStateSprites hearingSprites;
        [SerializeField] private MetaStateSprites speakingSprites;
        [SerializeField] private MetaStateSprites blockedSprites;

        private CancellationTokenSource tooltipCts = new ();

        public Button Button => button;

        public bool IsSuppressed { get; set; }

        private void Awake()
        {
            soundWaveAnimator.gameObject.SetActive(false);
            button.onClick.AddListener(HideHoverTooltip);
        }

        private void OnDestroy()
        {
            tooltipCts.SafeCancelAndDispose();
        }

        public void SetState(NearbyVoiceChatState state)
        {
            MetaStateSprites sprites = state switch
                                       {
                                           NearbyVoiceChatState.DISABLED => disconnectedSprites,
                                           NearbyVoiceChatState.IDLE => hearingSprites,
                                           NearbyVoiceChatState.SPEAKING => speakingSprites,
                                           NearbyVoiceChatState.SUPPRESSED => blockedSprites,
                                           _ => disconnectedSprites,
                                       };

            greenDotImage.SetActive(state is NearbyVoiceChatState.IDLE or NearbyVoiceChatState.SPEAKING);
            unselectedImage.sprite = sprites.unselected;
            hoverStateImage.sprite = sprites.hover;

            soundWaveAnimator.gameObject.SetActive(state == NearbyVoiceChatState.SPEAKING);
        }

        public void InitializeSoundWave(Func<float> amplitudeProvider)
        {
            soundWaveAnimator.Initialize(amplitudeProvider);
        }

        public void ShowDisabledTooltip()
        {
            tooltipCts = tooltipCts.SafeRestart();
            ShowTooltipAsync(tooltipCts.Token).Forget();
        }

        public void HideDisabledTooltip()
        {
            if (!suppressedTooltip.activeSelf) return;

            tooltipCts = tooltipCts.SafeRestart();
            HideTooltipAsync(tooltipCts.Token).Forget();
        }

        private async UniTaskVoid ShowTooltipAsync(CancellationToken ct)
        {
            suppressedTooltip.SetActive(true);
            CloseAreaButton.gameObject.SetActive(true);
            await tooltipAnimation.PlayShowAnimation(ct);
        }

        private void HideHoverTooltip() =>
            hoverTooltip.SetActive(false);

        private async UniTaskVoid HideTooltipAsync(CancellationToken ct)
        {
            await tooltipAnimation.PlayHideAnimation(ct);

            if (ct.IsCancellationRequested) return;

            suppressedTooltip.SetActive(false);
            CloseAreaButton.gameObject.SetActive(false);
        }
    }
}
