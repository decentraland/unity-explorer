using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    [UxmlElement]
    public partial class DebugIntSliderElement : DebugElementBase<DebugIntSliderElement, DebugIntSliderDef>
    {
        protected override void ConnectBindings()
        {
            SliderInt sliderInt = this.Q<SliderInt>();

            sliderInt.lowValue = definition.Min;
            sliderInt.highValue = definition.Max;
            sliderInt.label = definition.LabelName;

            definition.Binding.Connect(sliderInt);
        }
    }
}
