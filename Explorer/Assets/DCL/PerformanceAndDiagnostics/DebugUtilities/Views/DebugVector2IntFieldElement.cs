using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugVector2IntFieldElement : DebugElementBase<DebugVector2IntFieldElement, DebugVector2IntFieldDef>
    {
        protected override void ConnectBindings()
        {
            definition.Binding.Connect(this.Q<Vector2IntField>());
        }

        public new class UxmlFactory : UxmlFactory<DebugVector2IntFieldElement, UxmlTraits> { }
    }
}
