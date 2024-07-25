using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;

namespace MVC
{
    /// <summary>
    ///     Abstract class that should be used for all popups
    ///     It overrides the opening and closing animations only
    /// </summary>
    public abstract class PopupViewBase : ViewBase
    {
        [FormerlySerializedAs("ANIMATION_TIME")]
        [field: SerializeField]
        private float animationTime = 0.3f;

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            transform.localScale = Vector3.zero;
            return transform.DOScale(Vector3.one, animationTime).SetEase(Ease.OutBack).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            transform.localScale = Vector3.one;
            return transform.DOScale(Vector3.zero, animationTime).SetEase(Ease.OutBack).ToUniTask(cancellationToken: ct);
        }
    }
}
