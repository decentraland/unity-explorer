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

        public void ResetPanel() =>
            view.ColorPickerView.gameObject.SetActive(false);

        public void SetColor(Color color) =>
            controller.SetColor(color);

        private void OnControllerColorChanged(Color color) =>
            OnColorChanged(color);

        private void TogglePanel()
        {
            bool isActive = view.ColorPickerView.gameObject.activeInHierarchy;
            view.ColorPickerView.gameObject.SetActive(!isActive);

            if (isActive) OnColorPickerClosed.Invoke();
        }
    }
}
