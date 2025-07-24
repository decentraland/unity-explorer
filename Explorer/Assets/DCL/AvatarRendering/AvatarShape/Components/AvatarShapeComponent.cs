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
        public bool IsPreview;

        public Color SkinColor;
        public Color HairColor;
        public Color EyesColor;
        public BodyShape BodyShape;

        public WearablePromise WearablePromise;

        public string ID;
        public string Name;

        public readonly List<CachedAttachment> InstantiatedWearables;
        public readonly List<Renderer> OutlineCompatibleRenderers;

        public bool IsBodyInvisible;

        public AvatarShapeComponent(string name, string id, BodyShape bodyShape, WearablePromise wearablePromise,
            Color skinColor, Color hairColor, Color eyesColor, bool isBodyInvisible = false)
        {
            ID = id;
            Name = name;
            BodyShape = bodyShape;
            IsDirty = true;
            WearablePromise = wearablePromise;
            InstantiatedWearables = new List<CachedAttachment>();
            OutlineCompatibleRenderers = new List<Renderer>();
            SkinColor = skinColor;
            HairColor = hairColor;
            EyesColor = eyesColor;
            IsVisible = true;
            HiddenByModifierArea = false;
            IsPreview = false;
            IsBodyInvisible = isBodyInvisible;
        }

        public void CreateOutlineCompatibilityList()
        {
            // TODO: support outline for wearables when body is invisible
            if (IsBodyInvisible) return;

            foreach (var wearable in InstantiatedWearables)
            {
                if (wearable.OutlineCompatible)
                {
                    foreach (var rend in wearable.Renderers)
                    {
                        if (rend.gameObject.activeSelf && rend.enabled && rend.sharedMaterial.renderQueue >= 2000 && rend.sharedMaterial.renderQueue < 3000)
                            OutlineCompatibleRenderers.Add(rend);
                    }
                }
            }
        }

        public AvatarShapeComponent(string name, string id) : this()
        {
            ID = id;
            Name = name;
            InstantiatedWearables = new List<CachedAttachment>();
            OutlineCompatibleRenderers = new List<Renderer>();
            IsVisible = true;
        }
    }
}
