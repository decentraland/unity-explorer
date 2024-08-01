using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS;
using ECS.Abstract;
using Segment.Serialization;
using System;
using UnityEngine;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class TimeSpentInWorldAnalyticsSystem : BaseUnityLoopSystem
    {
        private readonly IAnalyticsController analytics;
        private readonly IRealmData realmData;

        private string realmName;

        private float timeSpentInWorld;
        private bool isDisposed;

        public TimeSpentInWorldAnalyticsSystem(World world, IAnalyticsController analytics, IRealmData realmData) : base(world)
        {
            this.analytics = analytics;
            this.realmData = realmData;
        }

        public override void Initialize()
        {
            base.Initialize();

            Application.wantsToQuit += () =>
            {
                SendAnalytics();
                return true;
            };

            Application.quitting += SendAnalytics;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => SendAnalytics();
        }

        ~TimeSpentInWorldAnalyticsSystem()
        {
            Dispose();
        }

        public override void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            GC.SuppressFinalize(this);
            SendAnalytics();

            base.Dispose();
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured) return;

            if (realmData.RealmName != realmName)
            {
                if (!string.IsNullOrEmpty(realmName))
                    SendAnalytics();

                realmName = realmData.RealmName;
            }
            else if (!string.IsNullOrEmpty(realmName))
                timeSpentInWorld += t;
        }

        private void SendAnalytics()
        {
            if (timeSpentInWorld == 0) return;

            analytics.Track(AnalyticsEvents.World.TIME_SPENT_IN_WORLD, new JsonObject
            {
                ["time_spent"] = timeSpentInWorld,
                ["realm_name"] = realmName,
            });

            timeSpentInWorld = 0;
        }
    }
}
