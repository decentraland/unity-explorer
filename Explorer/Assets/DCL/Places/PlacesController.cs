using DCL.Input;
using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Places
{
    public class PlacesController : ISection, IDisposable
    {
        private readonly PlacesView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;

        private bool isSectionActivated;

        public PlacesController(
            PlacesView view,
            ICursor cursor)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;

            view.OnSectionChanged += OnSectionChanged;
        }

        public void Dispose()
        {
            view.OnSectionChanged -= OnSectionChanged;
        }

        public void Activate()
        {
            if (isSectionActivated)
                return;

            isSectionActivated = true;
            view.SetViewActive(true);
            view.OpenSection(PlacesSections.DISCOVER, true);
            cursor.Unlock();
        }

        public void Deactivate()
        {
            isSectionActivated = false;
            view.SetViewActive(false);
        }

        public void Animate(int triggerId) =>
            view.PlayAnimator(triggerId);

        public void ResetAnimator() =>
            view.ResetAnimator();

        public RectTransform GetRectTransform() =>
            rectTransform;

        private void OnSectionChanged(PlacesSections section)
        {
            // TODO (Santi): Implement this!
            // ...
        }
    }
}
