using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterPreview
{
    public struct CharacterPreviewModel
    {
        public string BodyShape;

        public Color SkinColor;
        public Color HairColor;

        public List<string> Wearables;
        public HashSet<string> ForceRender;
    }
}
