using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using System.Collections.Generic;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarShapeComponent
    {
        public bool IsDirty;

        public readonly string ID;
        public readonly string Name;
        public Color SkinColor;
        public Color HairColor;
        public BodyShape BodyShape;

        public Promise WearablePromise;

        public readonly List<CachedWearable> InstantiatedWearables;

        public AvatarShapeComponent(string name, string id, BodyShape bodyShape, Promise wearablePromise, Color skinColor,
            Color hairColor)
        {
            ID = id;
            Name = name;
            BodyShape = bodyShape;
            IsDirty = true;
            WearablePromise = wearablePromise;
            InstantiatedWearables = new List<CachedWearable>();
            SkinColor = skinColor;
            HairColor = hairColor;
        }
    }
}
