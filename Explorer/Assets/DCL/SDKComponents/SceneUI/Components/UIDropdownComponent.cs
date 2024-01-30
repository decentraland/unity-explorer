using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using System;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UIDropdownComponent: IPoolableComponentProvider<DCLDropdown>
    {
        public DCLDropdown Dropdown;

        DCLDropdown IPoolableComponentProvider<DCLDropdown>.PoolableComponent => Dropdown;
        Type IPoolableComponentProvider<DCLDropdown>.PoolableComponentType => typeof(DCLDropdown);

        public void Dispose()
        {
            Dropdown.Dispose();
        }
    }
}
