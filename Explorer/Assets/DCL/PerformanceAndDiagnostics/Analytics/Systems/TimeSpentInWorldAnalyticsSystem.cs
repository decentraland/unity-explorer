using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS;
using ECS.Abstract;
using Segment.Serialization;
using System.Collections.Generic;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class TimeSpentInWorldAnalyticsSystem : BaseUnityLoopSystem
    {
        private readonly AnalyticsController analytics;
        private readonly IRealmData realmData;

        private float timeSpentInWorld;
        private string worldName;

        public TimeSpentInWorldAnalyticsSystem(World world, AnalyticsController analytics, IRealmData realmData) : base(world)
        {
            this.analytics = analytics;
            this.realmData = realmData;
        }

        public override void Dispose()
        {
            SendAnalytics();
            base.Dispose();
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured) return;

            if (realmData.RealmName != worldName)
            {
                if (!string.IsNullOrEmpty(worldName))
                    SendAnalytics();

                timeSpentInWorld = 0;
                worldName = realmData.RealmName.EndsWith(".dcl.eth") ? realmData.RealmName : string.Empty;
            }
            else if (!string.IsNullOrEmpty(worldName))
                timeSpentInWorld += t;
        }

        private void SendAnalytics()
        {
            analytics.Track(AnalyticsEvents.World.TIME_SPENT_IN_WORLD, new Dictionary<string, JsonElement>
            {
                ["time_spent"] = timeSpentInWorld,
                ["world_name"] = worldName,
            });
        }
    }
}
