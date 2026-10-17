using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.RewardPanel;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits
{
    public class CreditsUnlockedView : ViewBase, IView
    {
        [field: SerializeField]
        public TMP_Text CreditsText { get; private set; }

        [field: SerializeField]
        public CanvasGroup PanelCanvasGroup { get; private set; }

        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public AudioClipConfig Sound { get; private set; }

        [field: SerializeField]
        public RewardBackgroundRaysAnimation RewardBackgroundRaysAnimation { get; private set; }

        public void SetCreditsText(string text) =>
            CreditsText.text = text;

        protected override async UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            SetCanvasGroupInteractable(true);
            await RewardBackgroundRaysAnimation.ShowAnimationAsync(ct);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(Sound);
        }

        protected override async UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            await RewardBackgroundRaysAnimation.HideAnimationAsync(ct);
            SetCanvasGroupInteractable(false);
        }

        private void SetCanvasGroupInteractable(bool value)
        {
            PanelCanvasGroup.alpha = value ? 0 : 1;
            PanelCanvasGroup.interactable = value;
            PanelCanvasGroup.blocksRaycasts = value;
        }
    }
}
