using System.Collections.Generic;
using Runtime.Wearables;
using UnityEngine;
using Utils;

public static class WearablesConstants
{
    public const string BODY_SHAPE_MALE = "urn:decentraland:off-chain:base-avatars:BaseMale";
    public const string BODY_SHAPE_FEMALE = "urn:decentraland:off-chain:base-avatars:BaseFemale";

    public static readonly (string, string)[] BODY_PARTS_MAPPING =
    {
        ("head", WearableCategories.Categories.HEAD),
        ("ubody", WearableCategories.Categories.UPPER_BODY),
        ("lbody", WearableCategories.Categories.LOWER_BODY),
        ("hands", WearableCategories.Categories.HANDS),
        ("feet", WearableCategories.Categories.FEET),
        ("eyes", WearableCategories.Categories.HEAD), 
        ("eyebrows", WearableCategories.Categories.HEAD),
        ("mouth", WearableCategories.Categories.HEAD)
    };

    public static class Shaders
    {
        public static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        public static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");
        public static readonly int MASK_TEX_ID = Shader.PropertyToID("_MaskTex");
    }
}