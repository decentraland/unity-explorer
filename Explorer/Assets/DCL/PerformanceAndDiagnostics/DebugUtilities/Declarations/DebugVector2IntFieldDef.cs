using DCL.DebugUtilities.UIBindings;
using UnityEngine;

namespace DCL.DebugUtilities
{
    public class DebugVector2IntFieldDef : IDebugElementDef
    {
        public readonly IElementBinding<Vector2Int> Binding;

        public DebugVector2IntFieldDef(IElementBinding<Vector2Int> binding)
        {
            Binding = binding;
        }
    }
}
