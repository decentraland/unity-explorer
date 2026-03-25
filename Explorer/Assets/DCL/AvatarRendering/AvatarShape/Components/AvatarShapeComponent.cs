using Arch.Core;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution, DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarShapeComponent
    {
        public enum WearableLoadingStatus
        {
            None,
            Loading,
            Consumed,
        }

        /// <summary>
        ///     Wraps a <see cref="WearablePromise"/> with an explicit loading status
        ///     so callers never rely on <c>default</c> or <c>IsConsumed</c> to decide
        ///     whether loading has been requested.
        /// </summary>
        public class WearableLoadingState
        {
            private WearablePromise promise;

            public WearableLoadingStatus Status { get; private set; }

            public GetWearablesByPointersIntention LoadingIntention => promise.LoadingIntention;

            public StreamableLoadingResult<WearablesResolution>? Result => promise.Result;

            public Entity PromiseEntity => promise.Entity;

            public void SetPromise(WearablePromise wearablePromise)
            {
                promise = wearablePromise;
                Status = WearableLoadingStatus.Loading;
            }

            public void ForgetLoading(World world)
            {
                if (Status == WearableLoadingStatus.Loading)
                    promise.ForgetLoading(world);

                Status = WearableLoadingStatus.None;
                promise = default;
            }

            public bool SafeTryConsume(World world, ReportData reportData, out StreamableLoadingResult<WearablesResolution> result)
            {
                if (promise.SafeTryConsume(world, reportData, out result))
                {
                    Status = WearableLoadingStatus.Consumed;
                    return true;
                }

                return false;
            }

            public bool TryGetResult(World world, out StreamableLoadingResult<WearablesResolution> result) =>
                promise.TryGetResult(world, out result);
        }

        public bool IsDirty;
        public bool IsVisible;
        public bool HiddenByModifierArea;
        public bool IsPreview;

        public Color SkinColor;
        public Color HairColor;
        public Color EyesColor;
        public BodyShape BodyShape;

        public readonly WearableLoadingState WearableLoading;

        public string ID;
        public string Name;

        public readonly List<CachedAttachment> InstantiatedWearables;
        public readonly List<Renderer> OutlineCompatibleRenderers;

        public IAcquiredBudget LoadingBudget;
        public bool ShowOnlyWearables;

        public AvatarShapeComponent(string name, string id, BodyShape bodyShape,
            Color skinColor, Color hairColor, Color eyesColor, IAcquiredBudget loadingBudget, bool showOnlyWearables = false)
        {
            ID = id;
            Name = name;
            BodyShape = bodyShape;
            IsDirty = true;
            WearableLoading = new WearableLoadingState();
            InstantiatedWearables = new List<CachedAttachment>();
            OutlineCompatibleRenderers = new List<Renderer>();
            SkinColor = skinColor;
            HairColor = hairColor;
            EyesColor = eyesColor;
            IsVisible = true;
            HiddenByModifierArea = false;
            IsPreview = false;
            ShowOnlyWearables = showOnlyWearables;
            this.LoadingBudget = loadingBudget;
        }

        public bool IsWearableInstantiated => InstantiatedWearables.Count > 0;

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

        public AvatarShapeComponent(string name, string id, IAcquiredBudget loadingBudget) : this()
        {
            ID = id;
            Name = name;
            WearableLoading = new WearableLoadingState();
            InstantiatedWearables = new List<CachedAttachment>();
            OutlineCompatibleRenderers = new List<Renderer>();
            IsVisible = true;
            this.LoadingBudget = loadingBudget;
        }
    }
}
