using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DCL.Backpack.AvatarSection.Outfits.Models
{
    [Serializable]
    public class Outfit
    {
        [JsonProperty("bodyShape")]
        public string bodyShape;

        [JsonProperty("eyes")]
        public Eyes eyes;

        [JsonProperty("hair")]
        public Hair hair;

        [JsonProperty("skin")]
        public Skin skin;

        [JsonProperty("wearables")]
        public List<string> wearables;
    }

    [Serializable]
    public class Eyes
    {
        [JsonProperty("color")] public Color color;
    }

    [Serializable]
    public class Hair
    {
        [JsonProperty("color")] public Color color;
    }

    [Serializable]
    public class Skin
    {
        [JsonProperty("color")] public Color color;
    }
}