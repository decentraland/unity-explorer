using DCL.ECSComponents;
using ECS.Unity.ColorComponent;
using System;
using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Component
{
    [Serializable]
    public class NftShapeProperties
    {
        public Color color = Color.white;
        public NftFrameType style = NftFrameType.NftNone;
        public string urn = string.Empty;

        public NftShapeProperties With(NftFrameType frameType) =>
            new ()
            {
                color = this.color,
                style = frameType,
                urn = this.urn,
            };
    }

    public static class TextShapePropertiesExtensions
    {
        public static void ApplyOn(this NftShapeProperties properties, PBNftShape nftShape)
        {
            nftShape.Color = properties.color.ToColor3();
            nftShape.Style = properties.style;
            nftShape.Urn = properties.urn;
        }
    }
}
