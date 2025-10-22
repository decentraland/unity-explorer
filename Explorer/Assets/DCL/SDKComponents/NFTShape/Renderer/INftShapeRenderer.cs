using DCL.ECSComponents;
using System;
using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Renderer
{
    public interface INftShapeRenderer : IDisposable
    {
        void ApplyParent(Transform parent);

        void Apply(PBNftShape nftShape, bool sourceChanged);

        public void Apply(Texture tex);

        void NotifyFailed();

        void Hide();

        void Show();

    }
}
