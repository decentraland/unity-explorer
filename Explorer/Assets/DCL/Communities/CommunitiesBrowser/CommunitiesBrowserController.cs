using DCL.Input;
using DCL.UI;
using UnityEngine;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserController : ISection
    {
        private readonly CommunitiesBrowserView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;

        public CommunitiesBrowserController(
            CommunitiesBrowserView view,
            ICursor cursor)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
        }

        public void Activate()
        {
            view.gameObject.SetActive(true);
            cursor.Unlock();
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
        }

        public void Animate(int triggerId)
        {
            view.panelAnimator.SetTrigger(triggerId);
            view.headerAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            view.panelAnimator.Rebind();
            view.headerAnimator.Rebind();
            view.panelAnimator.Update(0);
            view.headerAnimator.Update(0);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
