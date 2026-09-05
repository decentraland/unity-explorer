using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    [UxmlElement]
    public partial class DebugVector2IntFieldElement : DebugElementBase<DebugVector2IntFieldElement, DebugVector2IntFieldDef>
    {
        protected override void ConnectBindings()
        {
            definition.Binding.Connect(this.Q<Vector2IntField>());
        }
    }
}
