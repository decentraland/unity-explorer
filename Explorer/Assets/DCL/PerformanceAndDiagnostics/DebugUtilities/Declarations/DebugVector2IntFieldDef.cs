using DCL.DebugUtilities.UIBindings;
using UnityEngine;

namespace DCL.DebugUtilities
{
    public class DebugVector2IntFieldDef : IDebugElementDef
    {
        public readonly ElementBinding<Vector2Int> Binding;

        public DebugVector2IntFieldDef(ElementBinding<Vector2Int> binding)
        {
            Binding = binding;
        }
    }
}
