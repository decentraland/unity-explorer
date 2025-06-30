using DCL.Optimization.Pools;
using System;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UITextComponent : IPoolableComponentProvider<Label>
    {
        public Label Label;

        Label IPoolableComponentProvider<Label>.PoolableComponent => Label;
        Type IPoolableComponentProvider<Label>.PoolableComponentType => typeof(Label);

        public void Dispose()
        {
        }
    }
}
