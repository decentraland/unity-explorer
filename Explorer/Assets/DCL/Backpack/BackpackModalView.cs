using Cysharp.Threading.Tasks;
using DG.Tweening;
using MVC;
using System.Threading;
using UnityEngine;

namespace DCL.Backpack
{
    /// <summary>
    ///     Frame that hosts the backpack view while it is shown on its own, on top of whatever panel opened it.
    /// </summary>
    public class BackpackModalView : ViewBase, IView
    {
        private const float ANIMATION_SPEED = 0.2f;

        [field: SerializeField]
        public CanvasGroup CanvasGroup { get; private set; } = null!;

        /// <summary>
        ///     The backpack paints its own background over this whole slot, so this rect is what frames the modal.
        /// </summary>
        [field: SerializeField]
        public RectTransform BackpackHost { get; private set; } = null!;

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            CanvasGroup.alpha = 0;
            return CanvasGroup.DOFade(1, ANIMATION_SPEED).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            CanvasGroup.DOFade(0, ANIMATION_SPEED).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
    }
}
