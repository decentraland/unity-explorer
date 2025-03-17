using DCL.DebugUtilities.UIBindings;

namespace DCL.DebugUtilities
{
    public class DebugFloatSliderDef : DebugFloatFieldDef
    {
        public readonly string LabelName;
        public readonly float Min;
        public readonly float Max;

        public DebugFloatSliderDef(string labelName, ElementBinding<float> binding, float min, float max) : base(binding)
        {
            LabelName = labelName;
            Min = min;
            Max = max;
        }
    }
}
