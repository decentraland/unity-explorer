using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using System;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UIInputComponent: IPoolableComponentProvider<DCLInputText>
    {
        public DCLInputText Input;

        DCLInputText IPoolableComponentProvider<DCLInputText>.PoolableComponent => Input;
        Type IPoolableComponentProvider<DCLInputText>.PoolableComponentType => typeof(DCLInputText);

        public void Dispose()
        {
            Input.Dispose();
        }
    }
}
