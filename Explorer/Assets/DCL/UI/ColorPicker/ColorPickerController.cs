using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DCL.UI
{
    public class ColorPickerController : IDisposable
    {
        private const float INCREMENT_AMOUNT = 0.1f;
        public event Action<Color, string> OnColorChanged;

        private readonly ColorPickerView view;
        private readonly ColorPresetsSO hairColors;
        private readonly ColorPresetsSO eyesColors;
        private readonly ColorPresetsSO bodyshapeColors;
        private readonly IObjectPool<ColorToggleView> colorTogglesPool;
        private readonly List<ColorToggleView> usedColorToggles = new ();
        private string currentCategory = "";

        private Color hairsColor;
        private Color eyesColor;
        private Color bodyshapeColor;

        public ColorPickerController(ColorPickerView view, ColorToggleView colorToggle, ColorPresetsSO hairColors, ColorPresetsSO eyesColors, ColorPresetsSO bodyshapeColors)
        {
            this.view = view;
            this.hairColors = hairColors;
            this.eyesColors = eyesColors;
            this.bodyshapeColors = bodyshapeColors;

            colorTogglesPool = new ObjectPool<ColorToggleView>(
                () => Object.Instantiate(colorToggle, view.ColorPresetsParent),
                actionOnGet:(toggle) => toggle.gameObject.SetActive(true),
                actionOnRelease:(toggle) => toggle.gameObject.SetActive(false));

            view.SliderHue.Slider.onValueChanged.AddListener(_ => SetColor());
            view.SliderHue.Slider.onValueChanged.AddListener(_ => SetSaturationColor());
            view.SliderHue.IncreaseButton.onClick.AddListener(() => ChangeProperty(view.SliderHue, INCREMENT_AMOUNT));
            view.SliderHue.DecreaseButton.onClick.AddListener(() => ChangeProperty(view.SliderHue, -INCREMENT_AMOUNT));

            view.SliderSaturation.Slider.onValueChanged.AddListener(_ => SetColor());
            view.SliderSaturation.IncreaseButton.onClick.AddListener(() => ChangeProperty(view.SliderSaturation, INCREMENT_AMOUNT));
            view.SliderSaturation.DecreaseButton.onClick.AddListener(() => ChangeProperty(view.SliderSaturation, -INCREMENT_AMOUNT));

            view.SliderValue.Slider.onValueChanged.AddListener(_ => SetColor());
            view.SliderValue.IncreaseButton.onClick.AddListener(() => ChangeProperty(view.SliderValue, INCREMENT_AMOUNT));
            view.SliderValue.DecreaseButton.onClick.AddListener(() => ChangeProperty(view.SliderValue, -INCREMENT_AMOUNT));

            view.ToggleButton.onClick.AddListener(TogglePanel);
        }

        public void Dispose()
        {
            view.SliderHue.Slider.onValueChanged.RemoveAllListeners();
            view.SliderHue.IncreaseButton.onClick.RemoveAllListeners();
            view.SliderHue.DecreaseButton.onClick.RemoveAllListeners();

            view.SliderSaturation.Slider.onValueChanged.RemoveAllListeners();
            view.SliderSaturation.IncreaseButton.onClick.RemoveAllListeners();
            view.SliderSaturation.DecreaseButton.onClick.RemoveAllListeners();

            view.SliderValue.Slider.onValueChanged.RemoveAllListeners();
            view.SliderValue.IncreaseButton.onClick.RemoveAllListeners();
            view.SliderValue.DecreaseButton.onClick.RemoveAllListeners();

            view.ToggleButton.onClick.RemoveAllListeners();
        }

        private void SetSaturationColor()
        {
            Color newColor = Color.HSVToRGB(view.SliderHue.Slider.value, 1, 1);
            ColorBlock block = view.SliderSaturation.Slider.colors;
            block.normalColor = newColor;
            block.highlightedColor = newColor;
            block.pressedColor = newColor;
            block.selectedColor = newColor;
            view.SliderSaturation.Slider.colors = block;
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
            ClearPool();
            switch (category)
            {
                case WearableCategories.Categories.EYES:
                    SetColors(eyesColors);
                    UpdateSliderValues(eyesColor);
                    currentCategory = WearableCategories.Categories.EYES;
                    break;
                case WearableCategories.Categories.HAIR:
                case WearableCategories.Categories.EYEBROWS:
                case WearableCategories.Categories.FACIAL_HAIR:
                    SetColors(hairColors);
                    UpdateSliderValues(hairsColor);
                    currentCategory = WearableCategories.Categories.HAIR;
                    break;
                case WearableCategories.Categories.BODY_SHAPE:
                    SetColors(bodyshapeColors);
                    UpdateSliderValues(bodyshapeColor);
                    currentCategory = WearableCategories.Categories.BODY_SHAPE;
                    break;
            }
        }

        private void TogglePanel() =>
            view.Container.SetActive(!view.Container.activeInHierarchy);

        private void SetColor()
        {
            Color newColor = Color.HSVToRGB(view.SliderHue.Slider.value, view.SliderSaturation.Slider.value, view.SliderValue.Slider.value);
            view.ColorPreviewImage.color = newColor;
            CheckButtonInteractivity(view.SliderHue);
            CheckButtonInteractivity(view.SliderSaturation);
            CheckButtonInteractivity(view.SliderValue);

            OnColorChanged?.Invoke(newColor, currentCategory);
        }

        private void ClearPool()
        {
            foreach (ColorToggleView usedColorToggle in usedColorToggles)
            {
                usedColorToggle.Button.onClick.RemoveAllListeners();
                colorTogglesPool.Release(usedColorToggle);
            }

            usedColorToggles.Clear();
        }

        private void ChangeProperty(SliderView slider, float amount)
        {
            slider.Slider.value += amount;
            CheckButtonInteractivity(slider);
        }

        private static void CheckButtonInteractivity(SliderView sliderComponent)
        {
            sliderComponent.IncreaseButton.interactable = sliderComponent.Slider.value < sliderComponent.Slider.maxValue;
            sliderComponent.DecreaseButton.interactable = sliderComponent.Slider.value > sliderComponent.Slider.minValue;
        }

        private void SetColors(ColorPresetsSO colorPreset)
        {
            foreach (Color presetColor in colorPreset.colors)
            {
                ColorToggleView toggleView = colorTogglesPool.Get();
                toggleView.transform.parent = view.ColorPresetsParent;
                toggleView.SelectionHighlight.gameObject.SetActive(false);
                toggleView.SetColor(presetColor, false);
                toggleView.Button.onClick.AddListener(() => ClickedOnPreset(presetColor, currentCategory));
                usedColorToggles.Add(toggleView);
            }
        }

        private void UpdateSliderValues(Color currentColor)
        {
            Color.RGBToHSV(currentColor, out float h, out float s, out float v);
            view.ColorPreviewImage.color = currentColor;
            view.SliderHue.Slider.SetValueWithoutNotify(h);
            view.SliderSaturation.Slider.SetValueWithoutNotify(s);
            view.SliderValue.Slider.SetValueWithoutNotify(v);
            SetSaturationColor();
        }

        private void ClickedOnPreset(Color presetColor, string category)
        {
            UpdateSliderValues(presetColor);
            OnColorChanged?.Invoke(presetColor, category);
        }
    }
}
