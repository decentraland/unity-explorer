using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Frames;
using DCL.SDKComponents.NFTShape.Frames.Pool;
using DCL.Utilities.Extensions;
using ECS.Unity.ColorComponent;
using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Renderer
{
    public class NftShapeRenderer : INftShapeRenderer
    {
        private readonly Transform transform;
        private readonly IFramesPool framesPool;

        private AbstractFrame? frame;

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
            frame.Paint(nftShape.Color.ToUnityColor());
            frame.UpdateStatus(AbstractFrame.Status.Loading);
        }

        public void Apply(Texture2D material)
        {
            frame.EnsureNotNull().Place(material);
        }

        public void NotifyFailed()
        {
            frame.EnsureNotNull().UpdateStatus(AbstractFrame.Status.Failed);
        }

        public void Hide()
        {
            if (frame != null) { frame.gameObject.SetActive(false); }
        }

        public void Show()
        {
            if (frame != null) { frame.gameObject.SetActive(true); }
        }
    }
}
