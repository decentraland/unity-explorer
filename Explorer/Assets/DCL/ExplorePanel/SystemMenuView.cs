using Cysharp.Threading.Tasks;
using DG.Tweening;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ExplorePanel
{
    public class SystemMenuView : ViewBase, IView
    {
        private const float PANEL_FADE_TIME = 0.3f;

        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;

        [field: SerializeField]
        public Button PreviewProfileButton { get; private set; } = null!;

        [field: SerializeField]
        public Button LogoutButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ExitAppButton { get; private set; } = null!;

        [field: SerializeField]
        public Button PrivacyPolicyButton { get; private set; } = null!;

        [field: SerializeField]
        public Button TermsOfServiceButton { get; private set; } = null!;

        [field: SerializeField]
        public CanvasGroup? CanvasGroup { get; private set; }

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            if (CanvasGroup != null)
            {
                CanvasGroup.alpha = 0;
                return CanvasGroup.DOFade(1, PANEL_FADE_TIME).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
            }
            return UniTask.CompletedTask;
        }

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            if (CanvasGroup != null) { return CanvasGroup.DOFade(0, PANEL_FADE_TIME).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct); }

            return UniTask.CompletedTask;
        }
    }
}
