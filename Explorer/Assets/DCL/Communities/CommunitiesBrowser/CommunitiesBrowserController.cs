using DCL.Input;
using DCL.UI;
using System;
using TMPro;
using UnityEngine;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserController : ISection, IDisposable
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

            ConfigureSortBySelector();
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

        public void Dispose()
        {
            view.sortByDropdown.Dropdown.onValueChanged.RemoveListener(OnSortByChanged);
        }

        private void ConfigureSortBySelector()
        {
            view.sortByDropdown.Dropdown.interactable = true;
            view.sortByDropdown.Dropdown.MultiSelect = false;
            view.sortByDropdown.Dropdown.options.Clear();
            view.sortByDropdown.Dropdown.options.AddRange(new[]
            {
                new TMP_Dropdown.OptionData { text = "Alphabetically" },
                new TMP_Dropdown.OptionData { text = "Popularity" },
            });
            view.sortByDropdown.Dropdown.value = 0;

            view.sortByDropdown.Dropdown.onValueChanged.AddListener(OnSortByChanged);
        }

        private static void OnSortByChanged(int arg0)
        {

        }
    }
}
