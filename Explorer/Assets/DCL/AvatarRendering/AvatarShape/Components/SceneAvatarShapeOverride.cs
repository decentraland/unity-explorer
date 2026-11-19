using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct SceneAvatarShapeOverride
    {
        /// <summary>
        ///     Empty keeps the profile wearables. Not empty replaces the whole profile wearable set.
        /// </summary>
        public readonly List<URN> Wearables;

        public BodyShape? BodyShape;
        public Color? SkinColor;
        public Color? HairColor;
        public Color? EyesColor;
        public bool? ShowOnlyWearables;
        public bool IsDirty;

        public SceneAvatarShapeOverride(List<URN> wearables)
        {
            Wearables = wearables;
            BodyShape = null;
            SkinColor = null;
            HairColor = null;
            EyesColor = null;
            ShowOnlyWearables = null;
            IsDirty = false;
        }

        public readonly bool IsEmpty =>
            Wearables.Count == 0 && !BodyShape.HasValue && !SkinColor.HasValue && !HairColor.HasValue && !EyesColor.HasValue && !ShowOnlyWearables.HasValue;

        public void Clear()
        {
            Wearables.Clear();
            BodyShape = null;
            SkinColor = null;
            HairColor = null;
            EyesColor = null;
            ShowOnlyWearables = null;
        }
    }
}
