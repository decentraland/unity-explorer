using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Renderer.Factory
{
    public interface INftShapeRendererFactory
    {
        INftShapeRenderer New(Transform parent);
    }
}
