﻿using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiling;
using ECS;
using ECS.Abstract;
using Segment.Serialization;
using System;
using UnityEngine;

namespace DCL.Analytics.Systems
{
    [LogCategory(ReportCategory.ANALYTICS)]
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class TimeSpentInWorldAnalyticsSystem : BaseUnityLoopSystem
    {
        private readonly IAnalyticsController analytics;
        private readonly IRealmData realmData;

        private string currentRealmName;

        private float timeSpentInWorld;

        public TimeSpentInWorldAnalyticsSystem(World world, IAnalyticsController analytics, IRealmData realmData) : base(world)
        {
            this.analytics = analytics;
            this.realmData = realmData;
        }

        public override void Initialize()
        {
            base.Initialize();

            Application.quitting += Dispose;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
        }

        ~TimeSpentInWorldAnalyticsSystem()
        {
            Dispose();
        }

        protected override void OnDispose()
        {
            GC.SuppressFinalize(this);
            SendAnalytics();
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured) return;

            if (realmData.RealmName != currentRealmName)
            {
                if (!string.IsNullOrEmpty(currentRealmName))
                    SendAnalytics();

                currentRealmName = realmData.RealmName;
            }
            else if (!string.IsNullOrEmpty(currentRealmName))
                timeSpentInWorld += t;
        }

        private void SendAnalytics()
        {
            if (timeSpentInWorld == 0) return;

            analytics.Track(AnalyticsEvents.World.TIME_SPENT_IN_WORLD, new JsonObject
            {
                ["time_spent"] = timeSpentInWorld,
                ["realm_name"] = currentRealmName,
            });

            timeSpentInWorld = 0;
        }
    }
}
