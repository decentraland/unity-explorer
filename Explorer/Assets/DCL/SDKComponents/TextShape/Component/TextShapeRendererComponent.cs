using DCL.Optimization.Pools;
using System;
using TMPro;

namespace DCL.SDKComponents.TextShape.Component
{
    public readonly struct TextShapeRendererComponent : IPoolableComponentProvider<TextMeshPro>
    {
        public readonly TextMeshPro TextMeshPro;

        public TextMeshPro PoolableComponent => TextMeshPro;
        public Type PoolableComponentType => typeof(TextMeshPro);

        public TextShapeRendererComponent(TextMeshPro textShape)
        {
            TextMeshPro = textShape;
        }

        public void Dispose() { }
    }
}
