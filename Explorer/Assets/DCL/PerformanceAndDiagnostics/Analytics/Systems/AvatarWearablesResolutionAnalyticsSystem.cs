using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS.Abstract;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.Analytics.Systems
{
    /// <summary>
    ///     It's detached from <see cref="AvatarInstantiatorAnalyticsSystem" /> to be able to measure how much time it take to instantiate the avatar
    ///     even if it was not deferred
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarLoaderAnalyticsSystem))]
    [UpdateBefore(typeof(AvatarInstantiatorSystem))] // Before the promise entity is destroyed
    public partial class AvatarWearablesResolutionAnalyticsSystem : BaseUnityLoopSystem
    {
        internal AvatarWearablesResolutionAnalyticsSystem(World world) : base(world) { }

        protected override void Update(float t) =>
            ReportWearablesResolutionFinishedQuery(World);

        [Query]
        private void ReportWearablesResolutionFinished(AvatarShapeComponent avatarShapeComponent, ref AvatarAnalytics avatarAnalytics)
        {
            // Don't modify the original promise on purpose:
            // $"{nameof(TryGetResult)} was called before {nameof(TryConsume)} for {LoadingIntention.ToString()}, the flow is inconclusive and should be fixed!"

            // If we have the result, wearables are already resolved
            if (avatarShapeComponent.WearablePromise.TryGetResult(World, out _) && avatarAnalytics.WearablesResolvedAt == AvatarAnalytics.WEARABLES_NOT_RESOLVED)
            {
                avatarAnalytics.WearablesResolvedAt = UnityEngine.Time.realtimeSinceStartup;

                // If we don't save it here this information will be destroyed with the promise itself
                GetWearablesByPointersIntention intent = World.Get<GetWearablesByPointersIntention>(avatarShapeComponent.WearablePromise.Entity);
                avatarAnalytics.MissingPointersCounter = intent.MissingPointersCount;
                avatarAnalytics.VisibleWearablesCount = intent.HideWearablesResolution.VisibleWearablesCount;
            }
        }
    }
}
