using Cysharp.Threading.Tasks;
using DCL.UI;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.RewardPanel
{
    public class RewardPanelView : ViewBase, IView
    {
        [field: SerializeField]
        public Button ContinueButton { get; private set; }

        [field: SerializeField]
        public CanvasGroup PanelCanvasGroup { get; private set; }

        [field: SerializeField]
        public Image RaysImage { get; private set; }

        [field: SerializeField]
        public ImageView ThumbnailImage { get; private set; }

        [field: SerializeField]
        public Image RarityBackground { get; private set; }

        [field: SerializeField]
        public Image RarityMark { get; private set; }

        [field: SerializeField]
        public Image CategoryImage { get; private set; }

        [field: SerializeField]
        public TMP_Text ItemName { get; private set; }

        [field: SerializeField]
        public RewardBackgroundRaysAnimation RewardBackgroundRaysAnimation { get; private set; }

        protected override async UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            SetCanvasGroupInteractable(true);
            await RewardBackgroundRaysAnimation.ShowAnimationAsync(ct);
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
