using Cysharp.Threading.Tasks;
using MVC;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.UI
{
    public class WearablesColorPickerController : IDisposable
    {
        private readonly IMVCManager mvcManager;
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
            IMVCManager mvcManager,
            WearablesColorPickerView view,
            ColorPresetsSO hairColors,
            ColorPresetsSO eyesColors,
            ColorPresetsSO bodyshapeColors)
        {
            this.mvcManager = mvcManager;
            this.view = view;
            this.hairColors = hairColors;
            this.eyesColors = eyesColors;
            this.bodyshapeColors = bodyshapeColors;

            view.ToggleButton.onClick.AddListener(TogglePanel);
        }

        public void Dispose() =>
            view.ToggleButton.onClick.RemoveAllListeners();

        public void UpdateCategoriesColors(Color newEyesColor, Color newHairColor, Color newBodyShapeColor)
        {
            eyesColor = newEyesColor;
            hairColor = newHairColor;
            bodyShapeColor = newBodyShapeColor;
        }

        public void SetColorPickerStatus(string category)
        {
            view.gameObject.SetActive(WearableCategories.COLOR_PICKER_CATEGORIES.Contains(category));

            switch (category)
            {
                case WearableCategories.Categories.EYES:
                    currentCategory = category;
                    UpdateColorPreviewImage(eyesColor);
                    break;
                case WearableCategories.Categories.HAIR:
                case WearableCategories.Categories.EYEBROWS:
                case WearableCategories.Categories.FACIAL_HAIR:
                    currentCategory = WearableCategories.Categories.HAIR;
                    UpdateColorPreviewImage(hairColor);
                    break;
                case WearableCategories.Categories.BODY_SHAPE:
                    currentCategory = category;
                    UpdateColorPreviewImage(bodyShapeColor);
                    break;
            }

            ResetPanel();
        }

        private void ResetPanel()
        {
            view.ArrowDownMark.SetActive(true);
            view.ArrowUpMark.SetActive(false);
        }

        private void TogglePanel() =>
            ShowColorPickerPopup();

        private void ShowColorPickerPopup()
        {
            Color initialColor;
            List<Color> presets;

            switch (currentCategory)
            {
                case WearableCategories.Categories.EYES:
                    initialColor = eyesColor;
                    presets = eyesColors.colors;
                    break;
                case WearableCategories.Categories.HAIR:
                    initialColor = hairColor;
                    presets = hairColors.colors;
                    break;
                case WearableCategories.Categories.BODY_SHAPE:
                    initialColor = bodyShapeColor;
                    presets = bodyshapeColors.colors;
                    break;
                default:
                    return;
            }

            Vector2 anchorPosition = view.ColorPickerAnchor != null ? view.ColorPickerAnchor.transform.position : Vector2.zero;

            var data = new ColorPickerPopupData
            {
                InitialColor = initialColor,
                ColorPresets = presets,
                Position = anchorPosition,
                OnColorChanged = (color) =>
                {
                    UpdateColorPreviewImage(color);
                    UpdateCategoryColor(color, currentCategory);
                    OnColorChanged.Invoke(color, currentCategory);
                }
            };

            mvcManager.ShowAsync(ColorPickerController.IssueCommand(data)).Forget();
        }

        private void UpdateColorPreviewImage(Color newColor) =>
            view.ColorPreviewImage.color = newColor;

        private void UpdateCategoryColor(Color newColor, string category)
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
    }
}
