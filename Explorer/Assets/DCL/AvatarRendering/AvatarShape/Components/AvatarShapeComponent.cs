using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using System.Collections.Generic;
using UnityEngine;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution, DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarShapeComponent
    {
        public bool IsDirty;
        public bool IsVisible;
        public bool HiddenByModifierArea;

        public Color SkinColor;
        public Color HairColor;
        public Color EyesColor;
        public BodyShape BodyShape;

        public WearablePromise WearablePromise;

        public string ID;
        public string Name;

        public readonly List<CachedAttachment> InstantiatedWearables;

        public AvatarShapeComponent(string name, string id, BodyShape bodyShape, WearablePromise wearablePromise,
            Color skinColor, Color hairColor, Color eyesColor)
        {
            ID = id;
            Name = name;
            BodyShape = bodyShape;
            IsDirty = true;
            WearablePromise = wearablePromise;
            InstantiatedWearables = new List<CachedAttachment>();
            SkinColor = skinColor;
            HairColor = hairColor;
            EyesColor = eyesColor;
            IsVisible = true;
            HiddenByModifierArea = false;
        }

        public AvatarShapeComponent(string name, string id) : this()
        {
            ID = id;
            Name = name;
            InstantiatedWearables = new List<CachedAttachment>();
            IsVisible = true;
        }
    }
}
