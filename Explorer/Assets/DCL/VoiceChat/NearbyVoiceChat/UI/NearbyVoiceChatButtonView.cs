using Cysharp.Threading.Tasks;
using DCL.UI;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.VoiceChat.Nearby
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

        [Space]
        [SerializeField] private MetaStateSprites disabledSprites;
        [SerializeField] private MetaStateSprites idleSprites;
        [SerializeField] private MetaStateSprites speakingSprites;
        [SerializeField] private MetaStateSprites suppressedSprites;

        private CancellationTokenSource tooltipCts = new ();

        public Button? Button => button;

        public bool IsSuppressed { get; set; }

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
                                           NearbyVoiceChatState.DISABLED => disabledSprites,
                                           NearbyVoiceChatState.IDLE => idleSprites,
                                           NearbyVoiceChatState.SPEAKING => speakingSprites,
                                           NearbyVoiceChatState.SUPPRESSED => suppressedSprites,
                                           _ => disabledSprites,
                                       };

            greenDotImage.SetActive(state is NearbyVoiceChatState.IDLE or NearbyVoiceChatState.SPEAKING);
            unselectedImage!.sprite = sprites.unselected;
            hoverStateImage!.sprite = sprites.hover;
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
