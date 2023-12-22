using UnityEngine;

namespace DCL.SDKComponents.NftShape.Renderer.Factory
{
    public interface INftShapeRendererFactory
    {
        INftShapeRenderer New(Transform parent);
    }
}
