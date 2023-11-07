using Cysharp.Threading.Tasks;
using DCL.UI;
using DG.Tweening;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ExplorePanel
{
    public class ExplorePanelView : ViewBase, IView
    {
        [field: SerializeField]
        public ExploreSections[] Sections { get; private set; }

        [field: SerializeField]
        public GameObject[] SectionsObjects { get; private set; }

        [field: SerializeField]
        public RectTransform AnimationTransform { get; private set; }

        [field: SerializeField]
        public TabSelectorView[] TabSelectorViews { get; private set; }

        [field: SerializeField]
        public Button CloseButton { get; private set; }

        protected override UniTask PlayShowAnimation(CancellationToken ct)
        {
            AnimationTransform.anchoredPosition = new Vector2(0, canvas.pixelRect.width);
            return AnimationTransform.DOAnchorPos(Vector2.zero, 0.5f).SetEase(Ease.OutCubic).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimation(CancellationToken ct)
        {
            AnimationTransform.anchoredPosition = Vector2.zero;
            return AnimationTransform.DOAnchorPos(new Vector2(canvas.pixelRect.width, 0), 0.5f).SetEase(Ease.OutCubic).ToUniTask(cancellationToken: ct);
        }
    }
}
