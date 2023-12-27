using DCL.AvatarRendering.Wearables;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterPreview
{
    /// <summary>
    ///     This model is supplied from the View-Controller
    /// </summary>
    public struct CharacterPreviewModel
    {
        public BodyShape BodyShape;

        public Color SkinColor;
        public Color HairColor;

        public List<string> Wearables;
    }
}
