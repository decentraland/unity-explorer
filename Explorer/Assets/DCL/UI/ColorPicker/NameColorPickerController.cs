using System;
using UnityEngine;

namespace DCL.UI
{
    public class NameColorPickerController : IDisposable
    {
        private readonly ColorPickerCore core;
        private readonly ColorPickerView view;
        private readonly ColorPresetsSO colorPresets;

        private Color currentColor;

        public event Action<Color> OnColorChanged;

        public NameColorPickerController(ColorPickerView view, ColorToggleView colorToggle, ColorPresetsSO colorPresets)
        {
            core = new ColorPickerCore(view, colorToggle);
            core.OnColorChanged += OnCoreColorChanged;

            this.view = view;
            this.colorPresets = colorPresets;

            core.SetPresets(this.colorPresets.colors, (presetColor, _) => OnPresetClicked(presetColor));
        }

        public void Dispose()
        {
            core.OnColorChanged -= OnCoreColorChanged;
            core.Dispose();
        }

        public void Reset() =>
            core.Reset();

        private void OnPresetClicked(Color presetColor)
        {
            core.UpdateSliderValues(presetColor);
            OnColorChanged(presetColor);
        }

        private void OnCoreColorChanged(Color color) =>
            OnColorChanged(color);
    }
}
