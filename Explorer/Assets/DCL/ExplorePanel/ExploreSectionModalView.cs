using Cysharp.Threading.Tasks;
using DG.Tweening;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ExplorePanel
{
    /// <summary>
    ///     Frame that hosts one explore section while it is shown on its own, on top of whatever panel opened it.
    /// </summary>
    public class ExploreSectionModalView : ViewBase, IView
    {
        private const float ANIMATION_SPEED = 0.2f;

        [field: SerializeField]
        public CanvasGroup CanvasGroup { get; private set; } = null!;

        /// <summary>The section paints its own background over this whole slot, so this rect is what frames the modal.</summary>
        [field: SerializeField]
        public RectTransform SectionHost { get; internal set; } = null!;

        /// <summary>The explore panel owns the close control of its sections, so the frame brings its own.</summary>
        [field: SerializeField]
        public Button CloseButton { get; internal set; } = null!;

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            CanvasGroup.alpha = 0;
            return CanvasGroup.DOFade(1, ANIMATION_SPEED).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            CanvasGroup.DOFade(0, ANIMATION_SPEED).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
    }
}
