using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS;
using ECS.Abstract;
using Segment.Serialization;
using System.Collections.Generic;
using UnityEngine;
using Entity = Arch.Core.Entity;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class WalkedDistanceAnalyticsSystem : BaseUnityLoopSystem
    {
        private readonly AnalyticsController analytics;
        private readonly IRealmData realmData;
        private readonly Entity playerEntity;

        private Vector3 lastPosition;
        private string lastRealm;

        private float totalDistanceSquared;

        public WalkedDistanceAnalyticsSystem(World world, AnalyticsController analytics, IRealmData realmData, in Entity playerEntity) : base(world)
        {
            this.analytics = analytics;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured) return;

            if (realmData.RealmName != lastRealm)
            {
                lastRealm = realmData.RealmName;
                lastPosition = World.Get<CharacterTransform>(playerEntity).Transform.position;
            }
            else
                UpdateWalkedDistance();
        }

        public override void Dispose()
        {
            analytics.Track(AnalyticsEvents.World.WALKED_DISTANCE, new JsonObject
            {
                ["distance"] = Mathf.Sqrt(totalDistanceSquared),
            });

            base.Dispose();
        }

        private void UpdateWalkedDistance()
        {
            Vector3 currentPosition = World.Get<CharacterTransform>(playerEntity).Transform.position;

            float distanceSquared = (currentPosition - lastPosition).sqrMagnitude;

            if (IsTeleported()) return;

            totalDistanceSquared += distanceSquared;
            lastPosition = currentPosition;

            bool IsTeleported() =>
                distanceSquared > 5 * 5;
        }
    }
}
