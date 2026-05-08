using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.ECSComponents;
using ECS.Unity.ColorComponent;
using System;
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
        public int InstantiationVersion;

        public Color SkinColor;
        public Color HairColor;
        public Color EyesColor;
        public BodyShape BodyShape;

        public WearablePromise WearablePromise;

        public string ID;
        public string Name;

        public readonly List<CachedAttachment> InstantiatedWearables;
        public readonly List<Renderer> OutlineCompatibleRenderers;

        public bool ShowOnlyWearables;

        /// <summary>
        /// Snapshot of the last applied wearable URN list. Reused across updates to detect
        /// structural changes without per-frame allocation.
        /// </summary>
        public readonly List<string> LastWearables;

        public AvatarShapeComponent(string name, string id, BodyShape bodyShape, WearablePromise wearablePromise,
            Color skinColor, Color hairColor, Color eyesColor, bool showOnlyWearables = false)
        {
            ID = id;
            Name = name;
            BodyShape = bodyShape;
            IsDirty = true;
            WearablePromise = wearablePromise;
            InstantiatedWearables = new List<CachedAttachment>();
            OutlineCompatibleRenderers = new List<Renderer>();
            LastWearables = new List<string>();
            SkinColor = skinColor;
            HairColor = hairColor;
            EyesColor = eyesColor;
            IsVisible = true;
            HiddenByModifierArea = false;
            IsPreview = false;
            ShowOnlyWearables = showOnlyWearables;
            InstantiationVersion = -1;
        }

        public void CreateOutlineCompatibilityList()
        {
            // TODO: support outline for wearables when body is invisible
            if (ShowOnlyWearables) return;

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
            LastWearables = new List<string>();
            IsVisible = true;
        }

        /// <summary>
        /// Returns true when <paramref name="other"/> changes any field that requires re-instantiation:
        /// BodyShape, ShowOnlyWearables, Wearables, or any of the avatar colors. Colors are included
        /// because they only reach the GPU through SetAvatarColors at instantiation — no live refresh
        /// path exists. Expression triggers and the talking flag are intentionally NOT checked: those
        /// tick frequently and must not trigger a rebuild.
        /// </summary>
        public readonly bool HasStructuralChange(PBAvatarShape other)
        {
            BodyShape newBodyShape = other;
            if (!BodyShape.Equals(newBodyShape)) return true;

            bool newShowOnlyWearables = other is { HasShowOnlyWearables: true, ShowOnlyWearables: true };
            if (ShowOnlyWearables != newShowOnlyWearables) return true;

            if (HairColor != other.GetHairColor().ToUnityColor()) return true;
            if (SkinColor != other.GetSkinColor().ToUnityColor()) return true;
            if (EyesColor != other.GetEyeColor().ToUnityColor()) return true;

            if (LastWearables.Count != other.Wearables.Count) return true;
            for (int i = 0; i < LastWearables.Count; i++)
                if (!string.Equals(LastWearables[i], other.Wearables[i], StringComparison.Ordinal))
                    return true;

            return false;
        }

        public void CaptureWearablesSnapshot(IReadOnlyList<string> wearables)
        {
            LastWearables.Clear();
            for (int i = 0; i < wearables.Count; i++)
                LastWearables.Add(wearables[i]);
        }
    }
}
