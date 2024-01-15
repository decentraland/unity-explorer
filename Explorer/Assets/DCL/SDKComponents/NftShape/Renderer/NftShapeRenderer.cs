using DCL.ECSComponents;
using DCL.SDKComponents.NftShape.Frame;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Renderer
{
    public class NftShapeRenderer : INftShapeRenderer
    {
        private readonly Transform transform;
        private readonly IFramesPool framesPool;

        private GameObject? frame;

        public NftShapeRenderer(Transform transform, IFramesPool framesPool)
        {
            this.transform = transform;
            this.framesPool = framesPool;
        }

        public void Apply(PBNftShape nftShape)
        {
            if (frame != null)
            {
                framesPool.Release(frame);
                frame = null;
            }
            frame = framesPool.NewFrame(nftShape.Style, transform);

            //TODO apply texture
        }

        public void Hide()
        {
            if (frame != null) { frame.SetActive(false); }
        }

        public void Show()
        {
            if (frame != null) { frame.SetActive(true); }
        }
    }
}
