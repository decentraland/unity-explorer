using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using System.Collections.Generic;
using UnityEngine;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution, DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.Components.EmotesResolution, DCL.AvatarRendering.Emotes.Components.GetEmotesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarShapeComponent
    {
        public bool IsDirty;
        public bool IsVisible;
        public bool HiddenByModifierArea;

        public Color SkinColor;
        public Color HairColor;
        public BodyShape BodyShape;

        public WearablePromise WearablePromise;
        public EmotePromise EmotePromise;

        public readonly string ID;

        public readonly List<CachedWearable> InstantiatedWearables;
        public readonly string Name;

        public AvatarShapeComponent(string name, string id) : this()
        {
            ID = id;
            Name = name;

            InstantiatedWearables = new List<CachedWearable>();
        }

        public AvatarShapeComponent(string name, string id, BodyShape bodyShape, WearablePromise wearablePromise, Color skinColor,
            Color hairColor, EmotePromise emotePromise)
        {
            ID = id;
            Name = name;
            BodyShape = bodyShape;
            IsDirty = true;
            WearablePromise = wearablePromise;
            EmotePromise = emotePromise;
            InstantiatedWearables = new List<CachedWearable>();
            SkinColor = skinColor;
            HairColor = hairColor;
            IsVisible = true;
            HiddenByModifierArea = false;
        }
    }
}
