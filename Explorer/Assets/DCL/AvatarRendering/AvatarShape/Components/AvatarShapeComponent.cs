using CommunicationData.URLHelpers;
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
        public bool NameTagHiddenByModifierArea;
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

        /// <summary>
        /// Snapshot of the last applied force-render category list (profile path only). Reused across
        /// updates to detect structural changes without per-frame allocation.
        /// </summary>
        public readonly List<string> LastForceRender;

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
            LastForceRender = new List<string>();
            SkinColor = skinColor;
            HairColor = hairColor;
            EyesColor = eyesColor;
            IsVisible = true;
            HiddenByModifierArea = false;
            NameTagHiddenByModifierArea = false;
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
            LastForceRender = new List<string>();
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

        /// <summary>
        /// Profile-path counterpart of <see cref="HasStructuralChange(PBAvatarShape)"/>. Takes plain fields
        /// (not a <c>Profile</c>/<c>Avatar</c>) because DCL.Profiles depends on DCL.AvatarRendering, so this
        /// assembly cannot see those types. Returns true when any field that requires re-instantiation changed:
        /// BodyShape, the three avatar colors, the wearable set, or the force-render set. Wearables and
        /// force-render are compared as sets (count-equal + one-way containment) because both snapshots are
        /// captured from HashSets (<c>Avatar.wearables</c> / <c>Avatar.forceRender</c>) and carry no duplicates.
        /// This is a semantic delta from the order-sensitive SDK-path overload above.
        /// </summary>
        public readonly bool HasStructuralChange(in BodyShape bodyShape, in Color hairColor, in Color skinColor,
            in Color eyesColor, IReadOnlyCollection<URN> wearables, IReadOnlyCollection<string> forceRender)
        {
            if (!BodyShape.Equals(bodyShape)) return true;

            if (HairColor != hairColor) return true;
            if (SkinColor != skinColor) return true;
            if (EyesColor != eyesColor) return true;

            if (LastWearables.Count != wearables.Count) return true;
            foreach (URN urn in wearables)
                if (!ContainsOrdinal(LastWearables, urn.LowerCaseUrn()))
                    return true;

            if (LastForceRender.Count != forceRender.Count) return true;
            foreach (string category in forceRender)
                if (!ContainsOrdinal(LastForceRender, category))
                    return true;

            return false;
        }

        /// <summary>
        /// Profile-path snapshot: stores lowercase URNs (matching <see cref="URN.LowerCaseUrn"/> equality) for
        /// wearables and the raw force-render categories.
        /// </summary>
        public void CaptureProfileSnapshot(IReadOnlyCollection<URN> wearables, IReadOnlyCollection<string> forceRender)
        {
            LastWearables.Clear();
            foreach (URN urn in wearables)
                LastWearables.Add(urn.LowerCaseUrn());

            LastForceRender.Clear();
            foreach (string category in forceRender)
                LastForceRender.Add(category);
        }

        private static bool ContainsOrdinal(List<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], value, StringComparison.Ordinal))
                    return true;

            return false;
        }
    }
}
