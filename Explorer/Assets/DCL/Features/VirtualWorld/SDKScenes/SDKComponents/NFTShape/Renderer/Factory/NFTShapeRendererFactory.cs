using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Frames.Pool;
using System;
using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Renderer.Factory
{
    public class NFTShapeRendererFactory : INFTShapeRendererFactory
    {
        private readonly IFramesPool framesPool;
        private readonly PBNftShape nftShape = Default();

        public NFTShapeRendererFactory(IFramesPool framesPool)
        {
            this.framesPool = framesPool;
        }

        public INftShapeRenderer New(Transform parent)
        {
            var shape = new GameObject($"nft component: {HashCode.Combine(parent.GetHashCode(), parent.childCount)}");
            shape.transform.SetParent(parent);
            shape.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var renderer = new NftShapeRenderer(shape.transform, framesPool);
            renderer.Apply(nftShape, true);
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
