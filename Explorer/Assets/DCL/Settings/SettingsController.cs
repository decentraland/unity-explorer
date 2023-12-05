using DCL.UI;
using UnityEngine;

namespace DCL.Settings
{
    public class SettingsController : ISection
    {
        private readonly SettingsView view;
        private readonly RectTransform rectTransform;

        public SettingsController(SettingsView view)
        {
            this.view = view;
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

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
