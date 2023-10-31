using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;

namespace MVC
{
    public abstract class PopupViewBase : ViewBase
    {
        private const float ANIMATION_TIME = 0.3f;

        protected override UniTask PlayShowAnimation(CancellationToken ct)
        {
            transform.localScale = Vector3.zero;
            return transform.DOScale(Vector3.one, ANIMATION_TIME).SetEase(Ease.OutBack).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimation(CancellationToken ct)
        {
            transform.localScale = Vector3.one;
            return transform.DOScale(Vector3.zero, ANIMATION_TIME).SetEase(Ease.OutBack).ToUniTask(cancellationToken: ct);
        }
    }
}
