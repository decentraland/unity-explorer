using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugFloatSliderElement : DebugElementBase<DebugFloatSliderElement, DebugFloatSliderDef>
    {
        public new class UxmlFactory : UxmlFactory<DebugFloatSliderElement, UxmlTraits> { }

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
