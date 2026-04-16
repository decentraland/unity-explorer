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

        [SerializeField] private Button? button;
        [SerializeField] private Image? unselectedImage;
        [SerializeField] private Image? hoverStateImage;
        [SerializeField] private GameObject? suppressedTooltip;
        [field: SerializeField] public TMP_Text SuppressedText { get; private set; } = null!;
        [field: SerializeField] public Button CloseAreaButton { get; private set; } = null!;
        [SerializeField] private ViewAnimationElementBase? tooltipAnimation;
        [SerializeField] private GameObject hoverTooltip = null!;
        [SerializeField] private GameObject greenDotImage = null!;
        [SerializeField] private SoundWaveAnimator? soundWaveAnimator;

        [Space]
        [SerializeField] private MetaStateSprites disconnectedSprites;
        [SerializeField] private MetaStateSprites hearingSprites;
        [SerializeField] private MetaStateSprites speakingSprites;
        [SerializeField] private MetaStateSprites blockedSprites;

        private CancellationTokenSource tooltipCts = new ();

        public Button? Button => button;

        public bool IsBlocked { get; set; }

        private void Awake()
        {
            SetState(NearbyVoiceChatState.DISABLED);
            button!.onClick.AddListener(HideHoverTooltip);
        }

        private void HideHoverTooltip()
        {
            hoverTooltip.SetActive(false);
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
            unselectedImage!.sprite = sprites.unselected;
            hoverStateImage!.sprite = sprites.hover;

            if (soundWaveAnimator != null)
                soundWaveAnimator.gameObject.SetActive(state == NearbyVoiceChatState.SPEAKING);
        }

        public void InitializeSoundWave(Func<float> amplitudeProvider)
        {
            if (soundWaveAnimator != null)
            {
                soundWaveAnimator.Initialize(amplitudeProvider);
                soundWaveAnimator.gameObject.SetActive(false);
            }
        }

        public void ShowDisabledTooltip()
        {
            tooltipCts = tooltipCts.SafeRestart();
            ShowTooltipAsync(tooltipCts.Token).Forget();
        }

        public void HideDisabledTooltip()
        {
            if (!suppressedTooltip!.activeSelf) return;

            tooltipCts = tooltipCts.SafeRestart();
            HideTooltipAsync(tooltipCts.Token).Forget();
        }

        private async UniTaskVoid ShowTooltipAsync(CancellationToken ct)
        {
            suppressedTooltip!.SetActive(true);
            CloseAreaButton.gameObject.SetActive(true);
            await tooltipAnimation!.PlayShowAnimation(ct);
        }

        private async UniTaskVoid HideTooltipAsync(CancellationToken ct)
        {
            await tooltipAnimation!.PlayHideAnimation(ct);

            if (ct.IsCancellationRequested) return;

            suppressedTooltip!.SetActive(false);
            CloseAreaButton.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            tooltipCts.SafeCancelAndDispose();
        }
    }
}
