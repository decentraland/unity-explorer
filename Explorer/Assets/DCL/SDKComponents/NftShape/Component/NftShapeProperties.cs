using DCL.ECSComponents;
using ECS.Unity.ColorComponent;
using System;
using UnityEngine;
using UnityEngine.Serialization;
using Font = DCL.ECSComponents.Font;

namespace DCL.SDKComponents.NftShape.Component
{
    [Serializable]
    public class NftShapeProperties
    {
        public Color color = Color.white;
        public NftFrameType style = NftFrameType.NftNone;
        public string urn = string.Empty;
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
