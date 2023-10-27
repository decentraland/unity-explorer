using DCL.DebugUtilities.Declarations;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Builders
{
    public interface IDebugElementFactory
    {
        VisualElement Create(IDebugElementDef def);
    }

    internal interface IDebugElementFactory<out TElement, in TDef> : IDebugElementFactory where TElement: VisualElement where TDef: IDebugElementDef
    {
        VisualElement IDebugElementFactory.Create(IDebugElementDef def) =>
            Create((TDef)def);

        TElement Create(TDef def);
    }
}
