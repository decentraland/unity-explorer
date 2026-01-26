using Runtime.Wearables;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class WearablesColorPickerController : IDisposable
    {
        private readonly ColorPickerCore core;
        private readonly WearablesColorPickerView view;
        private readonly ColorPresetsSO hairColors;
        private readonly ColorPresetsSO eyesColors;
        private readonly ColorPresetsSO bodyshapeColors;

        private Color hairColor;
        private Color eyesColor;
        private Color bodyShapeColor;
        private string currentCategory = "";

        public event Action<Color, string> OnColorChanged;

        public WearablesColorPickerController(
            WearablesColorPickerView view,
            ColorToggleView colorToggle,
            ColorPresetsSO hairColors,
            ColorPresetsSO eyesColors,
            ColorPresetsSO bodyshapeColors)
        {
            this.view = view;
            this.hairColors = hairColors;
            this.eyesColors = eyesColors;
            this.bodyshapeColors = bodyshapeColors;

            core = new ColorPickerCore(view.ColorPickerView, colorToggle);
            core.OnColorChanged += OnCoreColorChanged;

            view.ToggleButton.onClick.AddListener(TogglePanel);
        }

        public void Dispose()
        {
            view.ToggleButton.onClick.RemoveAllListeners();
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
                    hairColor = newColor;
                    break;
                case WearableCategories.Categories.BODY_SHAPE:
                    bodyShapeColor = newColor;
                    break;
            }
        }

        public void SetColorPickerStatus(string category)
        {
            view.gameObject.SetActive(WearableCategories.COLOR_PICKER_CATEGORIES.Contains(category));
            core.ClearPresets();

            switch (category)
            {
                case WearableCategories.Categories.EYES:
                    SetPresets(eyesColors);
                    core.SetColor(eyesColor);
                    currentCategory = WearableCategories.Categories.EYES;
                    break;
                case WearableCategories.Categories.HAIR:
                case WearableCategories.Categories.EYEBROWS:
                case WearableCategories.Categories.FACIAL_HAIR:
                    SetPresets(hairColors);
                    core.SetColor(hairColor);
                    currentCategory = WearableCategories.Categories.HAIR;
                    break;
                case WearableCategories.Categories.BODY_SHAPE:
                    SetPresets(bodyshapeColors);
                    core.SetColor(bodyShapeColor);
                    currentCategory = WearableCategories.Categories.BODY_SHAPE;
                    break;
            }

            // RectTransform colorControlsTransform = view.ColorPickerView.ColorPresetsParent.parent.parent.GetComponent<RectTransform>();
            // LayoutRebuilder.ForceRebuildLayoutImmediate(colorControlsTransform);

            ResetPanel();
        }

        private void OnCoreColorChanged(Color color)
        {
            UpdateColorPreviewImage(color);
            OnColorChanged(color, currentCategory);
        }

        private void TogglePanel()
        {
            view.ColorPickerView.gameObject.SetActive(!view.ColorPickerView.gameObject.activeInHierarchy);
            view.ArrowDownMark.SetActive(!view.ArrowDownMark.activeInHierarchy);
            view.ArrowUpMark.SetActive(!view.ArrowUpMark.activeInHierarchy);
        }

        private void ResetPanel()
        {
            view.ColorPickerView.gameObject.SetActive(false);
            view.ArrowDownMark.SetActive(true);
            view.ArrowUpMark.SetActive(false);
        }

        private void SetPresets(ColorPresetsSO colorPreset) =>
            core.SetPresets(colorPreset.colors);

        private void UpdateColorPreviewImage(Color newColor) =>
            view.ColorPreviewImage.color = newColor;
    }
}
