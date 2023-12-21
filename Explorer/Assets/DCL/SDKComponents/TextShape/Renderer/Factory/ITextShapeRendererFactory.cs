using UnityEngine;

namespace DCL.SDKComponents.TextShape.Renderer.Factory
{
    public interface ITextShapeRendererFactory
    {
        ITextShapeRenderer New(Transform parent);
    }
}
