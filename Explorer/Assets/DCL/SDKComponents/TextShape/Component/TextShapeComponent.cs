using DCL.Optimization.Pools;
using System;
using TMPro;

namespace DCL.SDKComponents.TextShape.Component
{
    public readonly struct TextShapeComponent : IPoolableComponentProvider<TextMeshPro>
    {
        public readonly TextMeshPro TextMeshPro;

        public TextMeshPro PoolableComponent => TextMeshPro;
        public Type PoolableComponentType => typeof(TextMeshPro);

        public TextShapeComponent(TextMeshPro textShape)
        {
            TextMeshPro = textShape;
        }

        public void Dispose() { }
    }
}
