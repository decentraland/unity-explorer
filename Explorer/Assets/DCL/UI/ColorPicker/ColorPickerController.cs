using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DCL.UI
{
    public class ColorPickerController : IDisposable
    {
        private const float INCREMENT_AMOUNT = 0.1f;

        private readonly ColorPickerView view;
        private readonly IObjectPool<ColorToggleView> colorTogglesPool;
        private readonly List<ColorToggleView> usedColorToggles = new ();

        public event Action<Color> OnColorChanged;

        public ColorPickerController(ColorPickerView view, ColorToggleView colorToggle)
        {
            this.view = view;

            colorTogglesPool = new ObjectPool<ColorToggleView>(
                () => Object.Instantiate(colorToggle, view.ColorPresetsParent),
                actionOnGet: (toggle) => toggle.gameObject.SetActive(true),
                actionOnRelease: (toggle) => toggle.gameObject.SetActive(false));

            SetupSliderListeners();
        }

        private void SetupSliderListeners()
        {
            if (view.EnableHueSlider)
            {
                view.SliderHue.Slider.onValueChanged.AddListener(_ => UpdateSlidersColor());
                view.SliderHue.Slider.onValueChanged.AddListener(_ => UpdateSaturationColor());
                view.SliderHue.IncreaseButton.onClick.AddListener(() => ChangeProperty(view.SliderHue, INCREMENT_AMOUNT));
                view.SliderHue.DecreaseButton.onClick.AddListener(() => ChangeProperty(view.SliderHue, -INCREMENT_AMOUNT));
            }

            if (view.EnableSaturationSlider)
            {
                view.SliderSaturation.Slider.onValueChanged.AddListener(_ => UpdateSlidersColor());
                view.SliderSaturation.IncreaseButton.onClick.AddListener(() => ChangeProperty(view.SliderSaturation, INCREMENT_AMOUNT));
                view.SliderSaturation.DecreaseButton.onClick.AddListener(() => ChangeProperty(view.SliderSaturation, -INCREMENT_AMOUNT));
            }

            if (view.EnableValueSlider)
            {
                view.SliderValue.Slider.onValueChanged.AddListener(_ => UpdateSlidersColor());
                view.SliderValue.IncreaseButton.onClick.AddListener(() => ChangeProperty(view.SliderValue, INCREMENT_AMOUNT));
                view.SliderValue.DecreaseButton.onClick.AddListener(() => ChangeProperty(view.SliderValue, -INCREMENT_AMOUNT));
            }
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
        }

        public void SetColor(Color color)
        {
            UpdateSliderValues(color);
            PreselectMatchingPreset(color);

            void PreselectMatchingPreset(Color color)
            {
                foreach (var toggle in usedColorToggles)
                    toggle.SelectionHighlight.gameObject.SetActive(color.Equals(toggle.ColorPicker.color));
            }
        }

        public void SetPresets(IEnumerable<Color> presetColors)
        {
            ClearPresets();

            foreach (Color presetColor in presetColors)
            {
                ColorToggleView toggleView = colorTogglesPool.Get();
                toggleView.transform.parent = view.ColorPresetsParent;
                toggleView.SelectionHighlight.gameObject.SetActive(false);
                toggleView.SetColor(presetColor, false);
                toggleView.Button.onClick.AddListener(() => SelectPreset(presetColor, toggleView));
                usedColorToggles.Add(toggleView);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.ColorPresetsParent.GetComponent<RectTransform>());

            void SelectPreset(Color color, ColorToggleView selectedToggleView)
            {
                foreach (var toggle in usedColorToggles)
                    toggle.SelectionHighlight.gameObject.SetActive(false);

                selectedToggleView.SelectionHighlight.gameObject.SetActive(true);
                UpdateSliderValues(color);
                OnColorChanged(color);
            }
        }

        public void ClearPresets()
        {
            foreach (ColorToggleView usedColorToggle in usedColorToggles)
            {
                usedColorToggle.Button.onClick.RemoveAllListeners();
                colorTogglesPool.Release(usedColorToggle);
            }

            usedColorToggles.Clear();
        }

        private void UpdateSliderValues(Color currentColor)
        {
            Color.RGBToHSV(currentColor, out float h, out float s, out float v);

            if (view.EnableHueSlider && view.SliderHue != null)
                view.SliderHue.Slider.SetValueWithoutNotify(h);

            if (view.EnableSaturationSlider && view.SliderSaturation != null)
                view.SliderSaturation.Slider.SetValueWithoutNotify(s);

            if (view.EnableValueSlider && view.SliderValue != null)
                view.SliderValue.Slider.SetValueWithoutNotify(v);

            UpdateSaturationColor();
        }

        private void UpdateSaturationColor()
        {
            if (!view.EnableSaturationSlider || view.SliderSaturation == null)
                return;

            float hue = view.EnableHueSlider && view.SliderHue != null
                ? view.SliderHue.Slider.value
                : view.DefaultHue;

            Color newColor = Color.HSVToRGB(hue, 1, 1);
            ColorBlock block = view.SliderSaturation.Slider.colors;
            block.normalColor = newColor;
            block.highlightedColor = newColor;
            block.pressedColor = newColor;
            block.selectedColor = newColor;
            view.SliderSaturation.Slider.colors = block;
        }

        private void UpdateSlidersColor()
        {
            foreach (var toggle in usedColorToggles)
                toggle.SelectionHighlight.gameObject.SetActive(false);

            float h = view.EnableHueSlider && view.SliderHue != null
                ? view.SliderHue.Slider.value
                : view.DefaultHue;

            float s = view.EnableSaturationSlider && view.SliderSaturation != null
                ? view.SliderSaturation.Slider.value
                : view.DefaultSaturation;

            float v = view.EnableValueSlider && view.SliderValue != null
                ? view.SliderValue.Slider.value
                : view.DefaultValue;

            Color newColor = Color.HSVToRGB(h, s, v);

            if (view.EnableHueSlider && view.SliderHue != null)
                CheckButtonInteractivity(view.SliderHue);

            if (view.EnableSaturationSlider && view.SliderSaturation != null)
                CheckButtonInteractivity(view.SliderSaturation);

            if (view.EnableValueSlider && view.SliderValue != null)
                CheckButtonInteractivity(view.SliderValue);

            OnColorChanged(newColor);
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
    }
}
