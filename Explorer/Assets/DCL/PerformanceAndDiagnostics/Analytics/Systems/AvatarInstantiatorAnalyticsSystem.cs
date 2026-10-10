using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS.Abstract;
using Newtonsoft.Json.Linq;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class AvatarInstantiatorAnalyticsSystem : BaseUnityLoopSystem
    {
        private readonly IAnalyticsController analyticsController;

        public AvatarInstantiatorAnalyticsSystem(World world, IAnalyticsController analyticsController) : base(world)
        {
            this.analyticsController = analyticsController;
        }

        protected override void Update(float t) =>
            ReportAvatarInstantiationFinishedQuery(World, UnityEngine.Time.realtimeSinceStartup);

        [Query]
        [All(typeof(AvatarCustomSkinningComponent), typeof(AvatarBase), typeof(AvatarTransformMatrixComponent))]
        private void ReportAvatarInstantiationFinished([Data] float timestamp, Entity entity, in AvatarShapeComponent avatarShapeComponent, ref AvatarAnalytics avatarAnalytics)
        {
            // But the avatar may be not instantiated yet
            if (avatarShapeComponent.WearablePromise.IsConsumed)
            {
                analyticsController.Track(AnalyticsEvents.Endpoints.AVATAR_RESOLVED, new JObject
                {
                    { "wearables_count", avatarAnalytics.WearablesCount },
                    { "visible_wearables_count", avatarAnalytics.VisibleWearablesCount },
                    { "new_pointers", avatarAnalytics.MissingPointersCounter },
                    { "wearables_resolution_duration", (avatarAnalytics.WearablesResolvedAt - avatarAnalytics.StartedAt) * 1000 },
                    { "instantiation_duration", (timestamp - avatarAnalytics.WearablesResolvedAt) * 1000 },
                });

                World.Remove<AvatarAnalytics>(entity);
            }
        }
    }
}
