using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DCL.UI
{
    public class ColorPickerController : ControllerBase<ColorPickerView, ColorPickerPopupData>
    {
        private const float INCREMENT_AMOUNT = 0.1f;

        private readonly ColorToggleView colorTogglePrefab;
        private IObjectPool<ColorToggleView> colorTogglesPool;
        private readonly List<ColorToggleView> usedColorToggles = new ();
        private RectTransform viewRectTransform;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.POPUP;

        public ColorPickerController(
            ViewFactoryMethod viewFactory,
            ColorToggleView colorTogglePrefab)
            : base(viewFactory)
        {
            this.colorTogglePrefab = colorTogglePrefab;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewRectTransform = viewInstance!.GetComponent<RectTransform>();

            colorTogglesPool = new ObjectPool<ColorToggleView>(
                () => Object.Instantiate(colorTogglePrefab, viewInstance!.ColorPresetsParent),
                actionOnGet: (toggle) => toggle.gameObject.SetActive(true),
                actionOnRelease: (toggle) => toggle.gameObject.SetActive(false));

            SetupSliderListeners();
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            // Position the container (same approach as GenericContextMenu - done in OnBeforeViewShow)
            if (inputData.Position.HasValue && viewInstance!.ColorControlsContainer != null && viewRectTransform != null)
            {
                // Convert world position to local space (same approach as GenericContextMenu)
                Vector3 localPosition = viewRectTransform.InverseTransformPoint(inputData.Position.Value);
                viewInstance.ColorControlsContainer.localPosition = localPosition;
            }

            // Set slider visibility if specified
            if (inputData.EnableSaturationSlider.HasValue)
            {
                viewInstance!.EnableSaturationSlider = inputData.EnableSaturationSlider.Value;
                if (viewInstance.SliderSaturation != null)
                    viewInstance.SliderSaturation.gameObject.SetActive(inputData.EnableSaturationSlider.Value);
            }

            if (inputData.EnableValueSlider.HasValue)
            {
                viewInstance!.EnableValueSlider = inputData.EnableValueSlider.Value;
                if (viewInstance.SliderValue != null)
                    viewInstance.SliderValue.gameObject.SetActive(inputData.EnableValueSlider.Value);
            }

            SetPresets(inputData.ColorPresets);

            if (inputData.InitialColor != default)
                SetColor(inputData.InitialColor);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            await UniTask.WhenAny(
                inputData.CloseTask?.Task ?? UniTask.Never(ct)
            );

        public override void Dispose()
        {
            if (viewInstance != null)
            {
                if (viewInstance.SliderHue != null) {
                    viewInstance.SliderHue.Slider.onValueChanged.RemoveAllListeners();
                    viewInstance.SliderHue.IncreaseButton.onClick.RemoveAllListeners();
                    viewInstance.SliderHue.DecreaseButton.onClick.RemoveAllListeners();
                }

                if (viewInstance.SliderSaturation != null) {
                    viewInstance.SliderSaturation.Slider.onValueChanged.RemoveAllListeners();
                    viewInstance.SliderSaturation.IncreaseButton.onClick.RemoveAllListeners();
                    viewInstance.SliderSaturation.DecreaseButton.onClick.RemoveAllListeners();
                }

                if (viewInstance.SliderValue != null) {
                    viewInstance.SliderValue.Slider.onValueChanged.RemoveAllListeners();
                    viewInstance.SliderValue.IncreaseButton.onClick.RemoveAllListeners();
                    viewInstance.SliderValue.DecreaseButton.onClick.RemoveAllListeners();
                }
            }

            ClearPresets();
            base.Dispose();
        }

        private void SetupSliderListeners()
        {
            if (viewInstance!.EnableHueSlider)
            {
                viewInstance.SliderHue.Slider.onValueChanged.AddListener(_ => UpdateSlidersColor());
                viewInstance.SliderHue.Slider.onValueChanged.AddListener(_ => UpdateSaturationColor());
                viewInstance.SliderHue.IncreaseButton.onClick.AddListener(() => ChangeProperty(viewInstance.SliderHue, INCREMENT_AMOUNT));
                viewInstance.SliderHue.DecreaseButton.onClick.AddListener(() => ChangeProperty(viewInstance.SliderHue, -INCREMENT_AMOUNT));
            }

            if (viewInstance.EnableSaturationSlider)
            {
                viewInstance.SliderSaturation.Slider.onValueChanged.AddListener(_ => UpdateSlidersColor());
                viewInstance.SliderSaturation.IncreaseButton.onClick.AddListener(() => ChangeProperty(viewInstance.SliderSaturation, INCREMENT_AMOUNT));
                viewInstance.SliderSaturation.DecreaseButton.onClick.AddListener(() => ChangeProperty(viewInstance.SliderSaturation, -INCREMENT_AMOUNT));
            }

            if (viewInstance.EnableValueSlider)
            {
                viewInstance.SliderValue.Slider.onValueChanged.AddListener(_ => UpdateSlidersColor());
                viewInstance.SliderValue.IncreaseButton.onClick.AddListener(() => ChangeProperty(viewInstance.SliderValue, INCREMENT_AMOUNT));
                viewInstance.SliderValue.DecreaseButton.onClick.AddListener(() => ChangeProperty(viewInstance.SliderValue, -INCREMENT_AMOUNT));
            }
        }

        private void SetColor(Color color)
        {
            UpdateSliderValues(color);
            PreselectMatchingPreset(color);

            void PreselectMatchingPreset(Color colorToMatch)
            {
                foreach (var toggle in usedColorToggles)
                    toggle.SelectionHighlight.gameObject.SetActive(colorToMatch.Equals(toggle.ColorPicker.color));
            }
        }

        private void SetPresets(List<Color> presetColors)
        {
            ClearPresets();

            for (int i = 0; i < presetColors.Count; i++)
            {
                Color presetColor = presetColors[i];
                ColorToggleView toggleView = colorTogglesPool.Get();
                toggleView.transform.SetParent(viewInstance!.ColorPresetsParent, false);
                toggleView.transform.SetSiblingIndex(i);
                toggleView.SelectionHighlight.gameObject.SetActive(false);
                toggleView.SetColor(presetColor, false);
                toggleView.Button.onClick.AddListener(() => SelectPreset(presetColor, toggleView));
                usedColorToggles.Add(toggleView);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(viewInstance!.ColorPresetsParent.GetComponent<RectTransform>());

            void SelectPreset(Color color, ColorToggleView selectedToggleView)
            {
                foreach (var toggle in usedColorToggles)
                    toggle.SelectionHighlight.gameObject.SetActive(false);

                selectedToggleView.SelectionHighlight.gameObject.SetActive(true);
                UpdateSliderValues(color);
                InvokeColorChanged(color);
            }
        }

        private void ClearPresets()
        {
            foreach (ColorToggleView usedColorToggle in usedColorToggles)
            {
                usedColorToggle.Button.onClick.RemoveAllListeners();
                colorTogglesPool?.Release(usedColorToggle);
            }

            usedColorToggles.Clear();
        }

        private void UpdateSliderValues(Color currentColor)
        {
            Color.RGBToHSV(currentColor, out float h, out float s, out float v);

            if (viewInstance!.EnableHueSlider && viewInstance.SliderHue != null)
                viewInstance.SliderHue.Slider.SetValueWithoutNotify(h);

            if (viewInstance.EnableSaturationSlider && viewInstance.SliderSaturation != null)
                viewInstance.SliderSaturation.Slider.SetValueWithoutNotify(s);

            if (viewInstance.EnableValueSlider && viewInstance.SliderValue != null)
                viewInstance.SliderValue.Slider.SetValueWithoutNotify(v);

            UpdateSaturationColor();
        }

        private void UpdateSaturationColor()
        {
            if (!viewInstance!.EnableSaturationSlider || viewInstance.SliderSaturation == null)
                return;

            float hue = viewInstance.EnableHueSlider && viewInstance.SliderHue != null
                ? viewInstance.SliderHue.Slider.value
                : viewInstance.DefaultHue;

            Color newColor = Color.HSVToRGB(hue, 1, 1);
            ColorBlock block = viewInstance.SliderSaturation.Slider.colors;
            block.normalColor = newColor;
            block.highlightedColor = newColor;
            block.pressedColor = newColor;
            block.selectedColor = newColor;
            viewInstance.SliderSaturation.Slider.colors = block;
        }

        private void UpdateSlidersColor()
        {
            foreach (var toggle in usedColorToggles)
                toggle.SelectionHighlight.gameObject.SetActive(false);

            float h = viewInstance!.EnableHueSlider && viewInstance.SliderHue != null
                ? viewInstance.SliderHue.Slider.value
                : viewInstance.DefaultHue;

            float s = viewInstance.EnableSaturationSlider && viewInstance.SliderSaturation != null
                ? viewInstance.SliderSaturation.Slider.value
                : viewInstance.DefaultSaturation;

            float v = viewInstance.EnableValueSlider && viewInstance.SliderValue != null
                ? viewInstance.SliderValue.Slider.value
                : viewInstance.DefaultValue;

            Color newColor = Color.HSVToRGB(h, s, v);

            if (viewInstance.EnableHueSlider && viewInstance.SliderHue != null)
                CheckButtonInteractivity(viewInstance.SliderHue);

            if (viewInstance.EnableSaturationSlider && viewInstance.SliderSaturation != null)
                CheckButtonInteractivity(viewInstance.SliderSaturation);

            if (viewInstance.EnableValueSlider && viewInstance.SliderValue != null)
                CheckButtonInteractivity(viewInstance.SliderValue);

            InvokeColorChanged(newColor);
        }

        private void InvokeColorChanged(Color color)
        {
            inputData.OnColorChanged?.Invoke(color);
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
