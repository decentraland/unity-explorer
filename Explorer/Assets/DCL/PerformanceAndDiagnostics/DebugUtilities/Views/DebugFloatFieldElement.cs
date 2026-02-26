using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    [UxmlElement]
    public partial class DebugFloatFieldElement : DebugElementBase<DebugFloatFieldElement, DebugFloatFieldDef>
    {
        protected override void ConnectBindings()
        {
            definition.Binding.Connect(this.Q<FloatField>());
        }
    }
}
