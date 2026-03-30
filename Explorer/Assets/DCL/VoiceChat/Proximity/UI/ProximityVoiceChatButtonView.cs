using Cysharp.Threading.Tasks;
using DCL.UI;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.VoiceChat.Proximity
{
    public class ProximityVoiceChatButtonView : MonoBehaviour
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
        [field: SerializeField] public GameObject? DisabledTooltip { get; private set; }
        [field: SerializeField] public Button CloseAreaButton { get; private set; } = null!;
        [SerializeField] private ViewAnimationElementBase? tooltipAnimation;
        [SerializeField] private GameObject hoverTooltip = null!;

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
            SetState(ProximityVoiceChatState.Disconnected);
            button!.onClick.AddListener(HideHoverTooltip);
        }

        private void HideHoverTooltip()
        {
            hoverTooltip.SetActive(false);
        }

        public void SetState(ProximityVoiceChatState state)
        {
            MetaStateSprites sprites = state switch
                                       {
                                           ProximityVoiceChatState.Disconnected => disconnectedSprites,
                                           ProximityVoiceChatState.Hearing => hearingSprites,
                                           ProximityVoiceChatState.Speaking => speakingSprites,
                                           ProximityVoiceChatState.Blocked => blockedSprites,
                                           _ => disconnectedSprites,
                                       };

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
            if (!DisabledTooltip!.activeSelf) return;

            tooltipCts = tooltipCts.SafeRestart();
            HideTooltipAsync(tooltipCts.Token).Forget();
        }

        private async UniTaskVoid ShowTooltipAsync(CancellationToken ct)
        {
            DisabledTooltip!.SetActive(true);
            CloseAreaButton.gameObject.SetActive(true);
            await tooltipAnimation!.PlayShowAnimation(ct);
        }

        private async UniTaskVoid HideTooltipAsync(CancellationToken ct)
        {
            await tooltipAnimation!.PlayHideAnimation(ct);

            if (ct.IsCancellationRequested) return;

            DisabledTooltip!.SetActive(false);
            CloseAreaButton.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            tooltipCts.SafeCancelAndDispose();
        }
    }
}
