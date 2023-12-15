using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugIntSliderElement : DebugElementBase<DebugIntSliderElement, DebugIntSliderDef>
    {
        public new class UxmlFactory : UxmlFactory<DebugIntSliderElement, UxmlTraits> { }

        protected override void ConnectBindings()
        {
            SliderInt sliderInt = this.Q<SliderInt>();

            sliderInt.lowValue = definition.Min;
            sliderInt.highValue = definition.Max;

            definition.Binding.Connect(sliderInt);
        }
    }
}
