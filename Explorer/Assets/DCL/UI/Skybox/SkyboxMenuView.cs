using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Skybox
{
    public class SkyboxMenuView: ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] public Toggle DynamicToggle { get; private set; } = null!;
        [field: SerializeField] public Slider TimeSlider { get; private set; } = null!;
        [field: SerializeField] public TMP_Text TimeText { get; private set; } = null!;
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
    }
}
