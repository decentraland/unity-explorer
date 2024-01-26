using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.TextShape.Renderer;
using System;

namespace DCL.SDKComponents.TextShape.Component
{
    public readonly struct TextShapeRendererComponent : IPoolableComponentProvider<ITextShapeRenderer>
    {
        private readonly ITextShapeRenderer textShape;

        public TextShapeRendererComponent(ITextShapeRenderer textShape)
        {
            this.textShape = textShape;
        }

        public void Apply(PBTextShape textShape)
        {
            this.textShape.Apply(textShape);
        }

        public void ApplyVisibility(bool visibility)
        {
            if (visibility)
                textShape.Show();
            else
                textShape.Hide();
        }

        public void Dispose() { }

        public ITextShapeRenderer PoolableComponent => textShape;

        public Type PoolableComponentType => typeof(ITextShapeRenderer);
    }
}
