using DCL.Input;
using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Places
{
    public class PlacesController : ISection, IDisposable
    {
        public Action<PlacesSections?, PlacesSections>? SectionChanged;
        public Action? PlacesClosed;

        private readonly PlacesView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;

        private bool isSectionActivated;
        private readonly DiscoverSectionController discoverSectionController;

        public PlacesController(
            PlacesView view,
            ICursor cursor)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;

            discoverSectionController = new DiscoverSectionController(this, view.DiscoverView);

            view.SectionChanged += OnSectionChanged;
        }

        public void Dispose()
        {
            view.SectionChanged -= OnSectionChanged;
            discoverSectionController.Dispose();
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
            PlacesClosed?.Invoke();
        }

        public void Animate(int triggerId) =>
            view.PlayAnimator(triggerId);

        public void ResetAnimator() =>
            view.ResetAnimator();

        public RectTransform GetRectTransform() =>
            rectTransform;

        private void OnSectionChanged(PlacesSections? fromSection, PlacesSections toSection)
        {
            SectionChanged?.Invoke(fromSection, toSection);
        }
    }
}
