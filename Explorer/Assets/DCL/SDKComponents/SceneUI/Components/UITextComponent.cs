using DCL.Optimization.Pools;
using ECS.StreamableLoading.Fonts;
using System;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UITextComponent: IPoolableComponentProvider<Label>
    {
        public Label Label;

        public SceneFontRequest FontRequest;

        public FontAsset? CustomFont;

        Label IPoolableComponentProvider<Label>.PoolableComponent => Label;
        Type IPoolableComponentProvider<Label>.PoolableComponentType => typeof(Label);

        public void Dispose() { }
    }
}
