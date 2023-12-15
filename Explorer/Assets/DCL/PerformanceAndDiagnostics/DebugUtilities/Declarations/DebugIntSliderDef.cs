using DCL.DebugUtilities.UIBindings;

namespace DCL.DebugUtilities
{
    public class DebugIntSliderDef : DebugIntFieldDef
    {
        public readonly int Min;
        public readonly int Max;

        public DebugIntSliderDef(ElementBinding<int> binding, int min, int max) : base(binding)
        {
            Min = min;
            Max = max;
        }
    }
}
