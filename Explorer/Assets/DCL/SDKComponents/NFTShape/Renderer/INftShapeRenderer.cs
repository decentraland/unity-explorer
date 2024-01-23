using DCL.ECSComponents;
using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Renderer
{
    public interface INftShapeRenderer
    {
        void Apply(PBNftShape nftShape);

        void Apply(Texture2D texture);

        void NotifyFailed();

        void Hide();

        void Show();
    }
}
