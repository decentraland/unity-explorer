using System.Collections.Generic;
using Data;
using Runtime.Wearables;
using UnityEngine;
using Utils;

public static class WearablesConstants
{
    public const string BODY_SHAPE_MALE = "urn:decentraland:off-chain:base-avatars:BaseMale";
    public const string BODY_SHAPE_FEMALE = "urn:decentraland:off-chain:base-avatars:BaseFemale";

    // The categories filled with a base wearable when nothing else covers them, so a lone item is
    // shown on a dressed avatar.
    public static readonly string[] DEFAULT_FACIAL_CATEGORIES =
    {
        WearableCategories.Categories.EYEBROWS,
        WearableCategories.Categories.MOUTH,
        WearableCategories.Categories.EYES,
    };

    public static readonly string[] DEFAULT_BODY_CATEGORIES =
    {
        WearableCategories.Categories.HAIR,
        WearableCategories.Categories.UPPER_BODY,
        WearableCategories.Categories.LOWER_BODY,
        WearableCategories.Categories.FEET,
    };

    /// <summary>
    /// The base wearable that fills <paramref name="category"/> for <paramref name="bodyShape"/>,
    /// or null for a category left empty.
    /// </summary>
    public static string GetDefaultWearableUrn(string category, BodyShape bodyShape)
    {
        var male = bodyShape == BodyShape.Male;

        var name = category switch
        {
            WearableCategories.Categories.EYEBROWS => male ? "eyebrows_00" : "f_eyebrows_00",
            WearableCategories.Categories.MOUTH => male ? "mouth_00" : "f_mouth_00",
            WearableCategories.Categories.EYES => male ? "eyes_00" : "f_eyes_00",
            WearableCategories.Categories.HAIR => male ? "casual_hair_01" : "standard_hair",
            WearableCategories.Categories.UPPER_BODY => male ? "green_hoodie" : "f_sweater",
            WearableCategories.Categories.LOWER_BODY => male ? "brown_pants" : "f_jeans",
            WearableCategories.Categories.FEET => male ? "sneakers" : "bun_shoes",
            _ => null,
        };

        return name == null ? null : $"urn:decentraland:off-chain:base-avatars:{name}";
    }

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