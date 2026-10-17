using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Renderer.Factory
{
    public interface INFTShapeRendererFactory
    {
        INftShapeRenderer New(Transform parent);
    }
}
