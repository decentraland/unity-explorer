using Runtime.Wearables;
using System;
using System.Linq;
using UnityEngine;

namespace DCL.UI
{
    public class WearablesColorPickerController : IDisposable
    {
        private readonly ColorPickerCore core;
        private readonly ColorPickerView view;
        private readonly ColorPresetsSO hairColors;
        private readonly ColorPresetsSO eyesColors;
        private readonly ColorPresetsSO bodyshapeColors;

        private Color hairsColor;
        private Color eyesColor;
        private Color bodyshapeColor;
        private string currentCategory = "";

        public event Action<Color, string> OnColorChanged;

        public WearablesColorPickerController(ColorPickerView view, ColorToggleView colorToggle, ColorPresetsSO hairColors, ColorPresetsSO eyesColors, ColorPresetsSO bodyshapeColors)
        {
            this.view = view;
            this.hairColors = hairColors;
            this.eyesColors = eyesColors;
            this.bodyshapeColors = bodyshapeColors;

            core = new ColorPickerCore(view, colorToggle);
            core.OnColorChanged += OnCoreColorChanged;
        }

        public void Dispose()
        {
            core.OnColorChanged -= OnCoreColorChanged;
            core.Dispose();
        }

        public void SetCurrentColor(Color newColor, string category)
        {
            switch (category)
            {
                case WearableCategories.Categories.EYES:
                    eyesColor = newColor;
                    break;
                case WearableCategories.Categories.HAIR:
                    hairsColor = newColor;
                    break;
                case WearableCategories.Categories.BODY_SHAPE:
                    bodyshapeColor = newColor;
                    break;
            }
        }

        public void SetColorPickerStatus(string category)
        {
            view.gameObject.SetActive(WearableCategories.COLOR_PICKER_CATEGORIES.Contains(category));
            view.Container.SetActive(false);
            core.ClearPresets();

            switch (category)
            {
                case WearableCategories.Categories.EYES:
                    SetColors(eyesColors, WearableCategories.Categories.EYES);
                    core.UpdateSliderValues(eyesColor);
                    currentCategory = WearableCategories.Categories.EYES;
                    break;
                case WearableCategories.Categories.HAIR:
                case WearableCategories.Categories.EYEBROWS:
                case WearableCategories.Categories.FACIAL_HAIR:
                    SetColors(hairColors, WearableCategories.Categories.HAIR);
                    core.UpdateSliderValues(hairsColor);
                    currentCategory = WearableCategories.Categories.HAIR;
                    break;
                case WearableCategories.Categories.BODY_SHAPE:
                    SetColors(bodyshapeColors, WearableCategories.Categories.BODY_SHAPE);
                    core.UpdateSliderValues(bodyshapeColor);
                    currentCategory = WearableCategories.Categories.BODY_SHAPE;
                    break;
            }
        }

        private void OnCoreColorChanged(Color color) =>
            OnColorChanged(color, currentCategory);

        private void SetColors(ColorPresetsSO colorPreset, string category) =>
            core.SetPresets(colorPreset.colors, (presetColor, _) => OnPresetClicked(presetColor, category));

        private void OnPresetClicked(Color presetColor, string category)
        {
            core.UpdateSliderValues(presetColor);
            OnColorChanged(presetColor, category);
        }
    }
}
