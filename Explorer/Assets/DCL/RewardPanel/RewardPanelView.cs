using Cysharp.Threading.Tasks;
using DCL.UI;
using DG.Tweening;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.RewardPanel
{
    public class RewardPanelView : ViewBase, IView
    {
        private const float FADE_ANIMATION_DURATION = 0.4f;
        private const float SCALE_ANIMATION_DURATION = 0.5f;

        [field: SerializeField]
        public Button ContinueButton { get; private set; }

        [field: SerializeField]
        public CanvasGroup PanelCanvasGroup { get; private set; }

        [field: SerializeField]
        public GameObject RaysGameObject { get; private set; }

        [field: SerializeField]
        public Image RaysImage { get; private set; }

        [field: SerializeField]
        public GameObject PanelContent { get; private set; }

        [field: SerializeField]
        public ImageView ThumbnailImage { get; private set; }

        [field: SerializeField]
        public Image RarityBackground { get; private set; }

        [field: SerializeField]
        public TMP_Text ItemName { get; private set; }

        private readonly Vector3 finalRotation = new (0,0,120);
        private CancellationTokenSource cts;

        protected override async UniTask PlayShowAnimation(CancellationToken ct)
        {
            cts = new CancellationTokenSource();
            SetCanvasGroupInteractable(true);
            RaysGameObject.transform.rotation = Quaternion.identity;
            PanelContent.transform.localScale = Vector3.zero;
            await PanelCanvasGroup.DOFade(1, FADE_ANIMATION_DURATION).ToUniTask(cancellationToken: ct);
            await PanelContent.transform.DOScale(Vector3.one, SCALE_ANIMATION_DURATION).SetEase(Ease.OutBounce).ToUniTask(cancellationToken: ct);
            RaysGameObject.transform.DORotate(finalRotation, 2f).SetLoops(-1, LoopType.Yoyo).ToUniTask(cancellationToken: cts.Token);
        }

        protected override async UniTask PlayHideAnimation(CancellationToken ct)
        {
            cts.SafeCancelAndDispose();
            PanelContent.transform.DOScale(Vector3.zero, SCALE_ANIMATION_DURATION / 2);
            await PanelCanvasGroup.DOFade(0, FADE_ANIMATION_DURATION / 2).ToUniTask(cancellationToken: ct);
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
