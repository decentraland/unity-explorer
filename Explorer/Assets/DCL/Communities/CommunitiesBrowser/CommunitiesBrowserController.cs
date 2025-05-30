using DCL.UI;
using UnityEngine;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserController : ISection
    {
        private readonly CommunitiesBrowserView view;
        private readonly RectTransform rectTransform;
        private readonly ICommunitiesDataProvider dataProvider;

        public CommunitiesBrowserController(CommunitiesBrowserView view, ICommunitiesDataProvider dataProvider)
        {
            this.view = view;
            this.dataProvider = dataProvider;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
        }

        public void Activate()
        {
            view.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
        }

        public void Animate(int triggerId)
        {

        }

        public void ResetAnimator()
        {

        }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
