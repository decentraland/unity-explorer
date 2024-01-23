using DCL.AvatarRendering.Wearables;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterPreview
{
    public struct CharacterPreviewModel
    {
        public BodyShape BodyShape;

        public Color SkinColor;
        public Color HairColor;

        public List<string> Wearables;
    }
}
