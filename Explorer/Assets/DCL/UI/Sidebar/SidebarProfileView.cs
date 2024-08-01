using Cysharp.Threading.Tasks;
using DCL.ExplorePanel;
using DG.Tweening;
using MVC;
using System.Threading;
using UnityEngine;

namespace DCL.UI.Sidebar
{
    public class SidebarProfileView : ViewBase, IView
    {
        private const float PANEL_FADE_TIME = 0.3f;

        [field: SerializeField] public ProfileWidgetView ProfileMenuWidget { get; private set; }
        [field: SerializeField] public SystemMenuView SystemMenuView { get; private set; }
        [field: SerializeField] public CanvasGroup CanvasGroup { get; private set; } = null!;

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            CanvasGroup.alpha = 0;
            return CanvasGroup.DOFade(1, PANEL_FADE_TIME).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            CanvasGroup.DOFade(0, PANEL_FADE_TIME).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);

    }
}
