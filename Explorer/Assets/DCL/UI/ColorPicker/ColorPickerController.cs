using DCL.AvatarRendering.Wearables.Helpers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.UI
{
    public class ColorPickerController
    {
        private readonly ColorPickerView view;
        private readonly ColorPresetsSO hairColors;
        private readonly ColorPresetsSO eyesColors;
        private readonly ColorPresetsSO bodyshapeColors;
        private readonly IObjectPool<ColorToggleView> colorTogglesPool;
        private readonly List<ColorToggleView> usedColorToggles = new ();

        public ColorPickerController(ColorPickerView view, ColorToggleView colorToggle, ColorPresetsSO hairColors, ColorPresetsSO eyesColors, ColorPresetsSO bodyshapeColors)
        {
            this.view = view;
            this.hairColors = hairColors;
            this.eyesColors = eyesColors;
            this.bodyshapeColors = bodyshapeColors;

            colorTogglesPool = new ObjectPool<ColorToggleView>(
                () => Object.Instantiate(colorToggle, view.ColorPresetsParent),
                actionOnGet:(toggle)=>toggle.gameObject.SetActive(true),
                actionOnRelease:(toggle)=>toggle.gameObject.SetActive(false));
        }

        public void SetColorPickerStatus(string category)
        {
            view.gameObject.SetActive(WearablesConstants.COLOR_PICKER_CATEGORIES.Contains(category));
            ClearPool();
            switch (category)
            {
                case WearablesConstants.Categories.EYES:
                    SetColors(hairColors);
                    break;
                case WearablesConstants.Categories.HAIR:
                    SetColors(eyesColors);
                    break;
                case WearablesConstants.Categories.BODY_SHAPE:
                    SetColors(bodyshapeColors);
                    break;
            }
        }

        private void ClearPool()
        {
            foreach (ColorToggleView usedColorToggle in usedColorToggles)
                colorTogglesPool.Release(usedColorToggle);

            usedColorToggles.Clear();
        }

        private void SetColors(ColorPresetsSO colorPreset)
        {
            foreach (Color presetColor in colorPreset.colors)
            {
                ColorToggleView toggleView = colorTogglesPool.Get();
                toggleView.SetColor(presetColor, false);
                usedColorToggles.Add(toggleView);
            }
        }
    }
}
