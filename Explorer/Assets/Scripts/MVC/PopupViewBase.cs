using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;

namespace MVC
{
    /// <summary>
    ///     Abstract class that should be used for all popups
    ///     It overrides the opening and closing animations only
    /// </summary>
    public abstract class PopupViewBase : ViewBase
    {
        [field: SerializeField]
        private float ANIMATION_TIME = 0.3f;

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
