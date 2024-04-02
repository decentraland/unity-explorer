using CommunicationData.URLHelpers;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterPreview
{
    public struct CharacterPreviewAvatarModel
    {
        public string BodyShape;

        public Color SkinColor;
        public Color HairColor;
        public Color EyesColor;

        public List<URN>? Wearables;
        public HashSet<URN>? Emotes;
        public HashSet<string> ForceRenderCategories;

        public bool Initialized;
    }
}
