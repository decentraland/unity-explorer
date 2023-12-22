using DCL.ECSComponents;
using DCL.SDKComponents.NftShape.Component;
using System;
using TMPro;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Renderer.Factory
{
    public class NftShapeRendererFactory : INftShapeRendererFactory
    {
        private readonly PBNftShape nftShape = Default();
        private readonly Quaternion backward = Quaternion.Euler(0, 180, 0);

        public INftShapeRenderer New(Transform parent)
        {
            var text = new GameObject($"nft component: {HashCode.Combine(parent.GetHashCode(), parent.childCount)}");
            text.transform.SetParent(parent);
            text.transform.localRotation = backward;
            // var tmp = text.AddComponent<TextMeshPro>()!;
            // var renderer = new TMPTextShapeRenderer(tmp, fontsStorage);
            // renderer.Apply(textShape);
            // return renderer;
            throw new NotImplementedException();
        }

        private static PBNftShape Default()
        {
            var v = new PBNftShape();
            new NftShapeProperties().ApplyOn(v);
            return v;
        }
    }
}
