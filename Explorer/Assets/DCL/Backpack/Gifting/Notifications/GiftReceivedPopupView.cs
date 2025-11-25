using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Views;
using DCL.RewardPanel;
using DG.Tweening;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.Gifting.Notifications
{
    public class GiftReceivedPopupView : ViewBase, IView
    {
        [field: Header("Canvas Group")]
        [field: SerializeField] public CanvasGroup MainCanvasGroup { get; private set; }
        
        [field: Header("Labels")]
        [field: SerializeField] public TMP_Text SubTitleText { get; private set; }

        [field: SerializeField] public TMP_Text TitleText { get; private set; }
        [field: SerializeField] public TMP_Text ItemNameText { get; private set; }

        [field: Header("Item Container")]
        [field: SerializeField] public GiftOpenedItemView GiftItemView { get; private set; }

        [field: Header("Buttons")]
        [field: SerializeField] public Button OpenBackpackButton { get; private set; }

        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public Button BackgroundOverlayButton { get; private set; }

        [field: SerializeField]
        public GiftTransferBackgroundAnimation BackgroundRaysAnimation { get; private set; }

        public async UniTask AnimateFadeIn(float duration, CancellationToken ct)
        {
            if (MainCanvasGroup == null) return;

            MainCanvasGroup.alpha = 0f;
            MainCanvasGroup.interactable = true;
            MainCanvasGroup.blocksRaycasts = true;

            MainCanvasGroup.DOKill();

            await MainCanvasGroup.DOFade(1f, duration)
                .SetEase(Ease.OutQuad)
                .ToUniTask(cancellationToken: ct);
        }

        public async UniTask AnimateFadeOut(float duration, CancellationToken ct)
        {
            if (MainCanvasGroup == null) return;

            MainCanvasGroup.interactable = false;
            MainCanvasGroup.blocksRaycasts = false;

            MainCanvasGroup.DOKill();

            await MainCanvasGroup.DOFade(0f, duration)
                .SetEase(Ease.InQuad)
                .ToUniTask(cancellationToken: ct);
        }

        public void Cleanup()
        {
            MainCanvasGroup.DOKill();
        }
    }
}