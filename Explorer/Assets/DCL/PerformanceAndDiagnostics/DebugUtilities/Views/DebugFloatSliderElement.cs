using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    [UxmlElement]
    public partial class DebugFloatSliderElement : DebugElementBase<DebugFloatSliderElement, DebugFloatSliderDef>
    {
        protected override void ConnectBindings()
        {
            Slider slider = this.Q<Slider>();

            slider.lowValue = definition.Min;
            slider.highValue = definition.Max;
            slider.label = definition.LabelName;

            definition.Binding.Connect(slider);
        }
    }
}
