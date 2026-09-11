using System;
using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    [Serializable]
    public class AvatarCustomizationConfig
    {
        public string bodyShape;
        public Color eyeColor;
        public Color skinColor;
        public Color hairColor;
        public List<string> wearables;
        public bool skipped;

        public AvatarCustomizationConfig(string bodyShape, Color eyeColor, Color skinColor, Color hairColor, List<string> wearables, bool skipped)
        {
            this.bodyShape = bodyShape;
            this.eyeColor = eyeColor;
            this.skinColor = skinColor;
            this.hairColor = hairColor;
            this.wearables = wearables;
            this.skipped = skipped;
        }
    }
}