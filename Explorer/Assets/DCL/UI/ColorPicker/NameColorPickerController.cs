using System;
using UnityEngine;

namespace DCL.UI
{
    public class NameColorPickerController : IDisposable
    {
        private readonly ColorPickerController controller;
        private readonly NameColorPickerView view;

        public event Action<Color> OnColorChanged;
        public event Action OnColorPickerClosed;

        public NameColorPickerController(
            NameColorPickerView view,
            ColorToggleView colorToggle,
            ColorPresetsSO colorPresets)
        {
            this.view = view;

            controller = new ColorPickerController(view.ColorPickerView, colorToggle);
            controller.OnColorChanged += OnControllerColorChanged;
            controller.SetPresets(colorPresets.colors);

            view.ToggleButton.onClick.AddListener(TogglePanel);
        }

        public void Dispose()
        {
            view.ToggleButton.onClick.RemoveAllListeners();
            controller.OnColorChanged -= OnControllerColorChanged;
            controller.Dispose();
        }

        // TODO (Maurizio) should set the color from the profile when opening passport?

        public void SetColor(Color color) =>
            controller.SetColor(color);

        private void OnControllerColorChanged(Color color) =>
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
