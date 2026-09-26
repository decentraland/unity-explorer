using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.RewardPanel
{
    public class RewardBackgroundRaysAnimation : MonoBehaviour
    {
        private const float FADE_ANIMATION_DURATION = 0.4f;
        private const float SCALE_ANIMATION_DURATION = 0.5f;

        [field: SerializeField]
        public GameObject RaysGameObject { get; private set; }

        [field: SerializeField]
        public GameObject PanelContent { get; private set; }

        [field: SerializeField]
        public CanvasGroup PanelCanvasGroup { get; private set; }

        private CancellationTokenSource cts;

        public async UniTask ShowAnimationAsync(CancellationToken ct)
        {
            cts = cts.SafeRestart();

            RaysGameObject.transform.rotation = Quaternion.identity;
            PanelContent.transform.localScale = Vector3.zero;
            RaysGameObject.transform.DORotate(new Vector3(0,0,-360), 5f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).ToUniTask(cancellationToken: cts.Token);
            await PanelCanvasGroup.DOFade(1, FADE_ANIMATION_DURATION).ToUniTask(cancellationToken: ct);
            await PanelContent.transform.DOScale(Vector3.one, SCALE_ANIMATION_DURATION).SetEase(Ease.OutBounce).ToUniTask(cancellationToken: ct);
        }

        public async UniTask HideAnimationAsync(CancellationToken ct)
        {
            cts.SafeCancelAndDispose();

            PanelContent.transform.DOScale(Vector3.zero, SCALE_ANIMATION_DURATION / 2);
            await PanelCanvasGroup.DOFade(0, FADE_ANIMATION_DURATION / 2).ToUniTask(cancellationToken: ct);
        }
    }
}
