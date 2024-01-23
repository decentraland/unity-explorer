using DCL.ECSComponents;
using DCL.SDKComponents.NftShape.Component;
using DCL.SDKComponents.NftShape.Frames;
using DCL.SDKComponents.NftShape.Frames.Pool;
using System;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Renderer.Factory
{
    public class NftShapeRendererFactory : INftShapeRendererFactory
    {
        private readonly IFramesPool framesPool;
        private readonly PBNftShape nftShape = Default();
        private readonly Quaternion backward = Quaternion.Euler(0, 180, 0);

        public NftShapeRendererFactory(IFramesPool framesPool)
        {
            this.framesPool = framesPool;
        }

        public INftShapeRenderer New(Transform parent)
        {
            var shape = new GameObject($"nft component: {HashCode.Combine(parent.GetHashCode(), parent.childCount)}");
            shape.transform.SetParent(parent);
            shape.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var renderer = new NftShapeRenderer(shape.transform, framesPool);
            renderer.Apply(nftShape);
            return renderer;
        }

        private static PBNftShape Default()
        {
            var v = new PBNftShape();
            new NftShapeProperties().ApplyOn(v);
            return v;
        }
    }
}
