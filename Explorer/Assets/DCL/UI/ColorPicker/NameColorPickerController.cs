using Cysharp.Threading.Tasks;
using MVC;
using System;
using UnityEngine;

namespace DCL.UI
{
    public class NameColorPickerController : IDisposable
    {
        private readonly IMVCManager mvcManager;
        private readonly NameColorPickerView view;
        private readonly ColorPresetsSO colorPresets;
        private Color currentColor;

        public event Action<Color> OnColorChanged;
        public event Action OnColorPickerClosed;

        public NameColorPickerController(
            IMVCManager mvcManager,
            NameColorPickerView view,
            ColorPresetsSO colorPresets)
        {
            this.mvcManager = mvcManager;
            this.view = view;
            this.colorPresets = colorPresets;

            view.ToggleButton.onClick.AddListener(TogglePanel);
        }

        public void Dispose()
        {
            view.ToggleButton.onClick.RemoveAllListeners();
            OnColorChanged = null;
        }

        public void SetColor(Color color) =>
            currentColor = color;

        private void TogglePanel() =>
            ShowColorPickerPopup();

        private void ShowColorPickerPopup()
        {
            // Get anchor position in world space
            Vector2 anchorPosition = view.ColorPickerAnchor != null ? view.ColorPickerAnchor.transform.position : Vector2.zero;

            var data = new ColorPickerPopupData
            {
                InitialColor = currentColor,
                ColorPresets = colorPresets.colors,
                Position = anchorPosition,
                EnableSaturationSlider = false,
                EnableValueSlider = false,
                OnColorChanged = (color) =>
                {
                    currentColor = color;
                    OnColorChanged.Invoke(color);
                }
            };

            mvcManager.ShowAsync(ColorPickerController.IssueCommand(data)).ContinueWith(() =>
            {
                OnColorPickerClosed.Invoke();
            }).Forget();
        }
    }
}
