using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.UI.Skybox
{
    public class SkyboxMenuView: ViewBaseWithAnimationElement, IView, IPointerClickHandler
    {
        [field: SerializeField] public Toggle TimeProgressionToggle { get; private set; } = null!;
        [field: SerializeField] public Slider TimeSlider { get; private set; } = null!;
        [field: SerializeField] public TMP_Text TimeText { get; private set; } = null!;
        [field: SerializeField] public CanvasGroup TimeProgressionGroup { get; private set; } = null!;
        [field: SerializeField] public CanvasGroup TopSliderGroup { get; private set; } = null!;
        [field: SerializeField] public CanvasGroup TextSliderGroup { get; private set; } = null!;

        // Stops pointer-click events from bubbling up to the SidebarSkyboxButton ancestor,
        // whose Button would otherwise re-trigger OpenSkyboxSettingsPanel and freeze the view.
        public void OnPointerClick(PointerEventData eventData) { }
    }
}
