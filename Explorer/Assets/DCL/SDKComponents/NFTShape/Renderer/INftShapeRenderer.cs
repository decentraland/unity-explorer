using DCL.ECSComponents;
using System;
using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Renderer
{
    public interface INftShapeRenderer : IDisposable
    {
        void ApplyParent(Transform parent);

        void Apply(PBNftShape nftShape, bool sourceChanged);

        void Apply(Texture2D texture);

        void NotifyFailed();

        void Hide();

        void Show();
    }
}
