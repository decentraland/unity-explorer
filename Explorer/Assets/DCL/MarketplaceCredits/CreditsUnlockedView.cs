using Cysharp.Threading.Tasks;
using DCL.Audio;
using DG.Tweening;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.MarketplaceCredits
{
    public class CreditsUnlockedView : ViewBase, IView
    {
        private const float FADE_ANIMATION_DURATION = 0.4f;
        private const float SCALE_ANIMATION_DURATION = 0.5f;

        [field: SerializeField]
        public CanvasGroup PanelCanvasGroup { get; private set; }

        [field: SerializeField]
        public GameObject RaysGameObject { get; private set; }

        [field: SerializeField]
        public GameObject PanelContent { get; private set; }

        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public AudioClipConfig Sound { get; private set; }

        private CancellationTokenSource cts;

        protected override async UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            cts = new CancellationTokenSource();
            SetCanvasGroupInteractable(true);
            RaysGameObject.transform.rotation = Quaternion.identity;
            PanelContent.transform.localScale = Vector3.zero;
            await PanelCanvasGroup.DOFade(1, FADE_ANIMATION_DURATION).ToUniTask(cancellationToken: ct);
            await PanelContent.transform.DOScale(Vector3.one, SCALE_ANIMATION_DURATION).SetEase(Ease.OutBounce).ToUniTask(cancellationToken: ct);
            RaysGameObject.transform.DORotate(new Vector3(0,0,360), 2f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).ToUniTask(cancellationToken: cts.Token);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(Sound);
        }

        protected override async UniTask PlayHideAnimationAsync(CancellationToken ct)
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
