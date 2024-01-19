using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using System;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UIInputComponent: IPoolableComponentProvider<TextField>
    {
        public TextField TextField;
        public TextFieldPlaceholder Placeholder;

        TextField IPoolableComponentProvider<TextField>.PoolableComponent => TextField;
        Type IPoolableComponentProvider<TextField>.PoolableComponentType => typeof(TextField);

        public void Dispose()
        {
            Placeholder.Dispose();
        }
    }
}
