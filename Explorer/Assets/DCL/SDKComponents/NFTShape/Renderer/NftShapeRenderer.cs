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
        private Transform transform;
        private readonly IFramesPool framesPool;

        private AbstractFrame? frame;

        public NftShapeRenderer(Transform transform, IFramesPool framesPool)
        {
            this.transform = transform;
            this.framesPool = framesPool;
        }

        public void ApplyParent(Transform parent)
        {
            this.transform = parent;
            TryAdjustFrameTransform();
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
            TryAdjustFrameTransform();
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

        public void Dispose()
        {
            if (frame == null)
                return;

            framesPool.Release(frame);
            frame = null;
        }

        private void TryAdjustFrameTransform()
        {
            if (frame != null)
            {
                frame.transform.SetParent(this.transform, false);
                frame.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                frame.transform.localScale = Vector3.one;
            }
        }
    }
}
