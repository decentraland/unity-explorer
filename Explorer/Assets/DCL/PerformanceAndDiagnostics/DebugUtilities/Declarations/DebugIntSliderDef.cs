using DCL.DebugUtilities.UIBindings;

namespace DCL.DebugUtilities
{
    public class DebugIntSliderDef : DebugIntFieldDef
    {
        public readonly string LabelName;
        public readonly int Min;
        public readonly int Max;

        public DebugIntSliderDef(string labelName, ElementBinding<int> binding, int min, int max) : base(binding)
        {
            LabelName = labelName;
            Min = min;
            Max = max;
        }
    }
}
