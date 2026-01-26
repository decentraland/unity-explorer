using System;
using UnityEngine;

namespace DCL.UI
{
    public class NameColorPickerController : IDisposable
    {
        private readonly ColorPickerCore core;
        private readonly NameColorPickerView view;

        public event Action<Color> OnColorChanged;
        public event Action OnColorPickerClosed;

        public NameColorPickerController(
            NameColorPickerView view,
            ColorToggleView colorToggle,
            ColorPresetsSO colorPresets)
        {
            this.view = view;

            core = new ColorPickerCore(view.ColorPickerView, colorToggle);
            core.OnColorChanged += OnCoreColorChanged;
            core.SetPresets(colorPresets.colors);

            view.ToggleButton.onClick.AddListener(TogglePanel);
        }

        public void Dispose()
        {
            view.ToggleButton.onClick.RemoveAllListeners();
            core.OnColorChanged -= OnCoreColorChanged;
            core.Dispose();
        }

        // TODO (Maurizio) should set the color from the profile when opening passport?

        public void SetColor(Color color) =>
            core.SetColor(color);

        private void OnCoreColorChanged(Color color) =>
            OnColorChanged(color);

        private void TogglePanel()
        {
            // TODO (Maurizio) change button state here?

            bool isActive = view.ColorPickerView.gameObject.activeInHierarchy;
            view.ColorPickerView.gameObject.SetActive(!isActive);

            if (isActive) OnColorPickerClosed.Invoke();
        }

        private void ResetPanel() =>
            view.ColorPickerView.gameObject.SetActive(false);
    }
}
