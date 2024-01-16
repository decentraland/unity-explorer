using DCL.ECSComponents;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Renderer
{
    public interface INftShapeRenderer
    {
        void Apply(PBNftShape nftShape);

        void Apply(Material material);

        void NotifyFailed();

        void Hide();

        void Show();
    }
}
