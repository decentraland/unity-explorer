using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using Segment.Serialization;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class PlayerParcelChangedAnalyticsSystem : BaseUnityLoopSystem
    {
        private static readonly Vector2Int MIN_INT2 = new (int.MinValue, int.MinValue);

        private readonly AnalyticsController analytics;
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;

        private readonly Entity playerEntity;

        private Vector2Int oldParcel;
        private ISceneFacade lastScene;

        public PlayerParcelChangedAnalyticsSystem(World world, AnalyticsController analytics, IRealmData realmData, IScenesCache scenesCache, in Entity playerEntity) : base(world)
        {
            this.analytics = analytics;
            this.realmData = realmData;
            this.scenesCache = scenesCache;
            this.playerEntity = playerEntity;

            ResetOldParcel();
        }

        private void ResetOldParcel()
        {
            oldParcel = MIN_INT2;
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured)
            {
                ResetOldParcel();
                return;
            }

            Vector3 playerPos = World.Get<CharacterTransform>(playerEntity).Transform.position;
            Vector2Int newParcel = ParcelMathHelper.FloorToParcel(playerPos);

            if (newParcel != oldParcel)
            {
                analytics.Track(AnalyticsEvents.World.MOVE_TO_PARCEL,
                    new Dictionary<string, JsonElement>
                    {
                        { "old parcel", oldParcel == MIN_INT2 ? "(NaN, NaN)" : oldParcel.ToString() },
                        { "new parcel", newParcel.ToString() },
                    });

                oldParcel = newParcel;

                if (scenesCache.TryGetByParcel(newParcel, out var currentScene) && currentScene != lastScene)
                {
                    analytics.Track(AnalyticsEvents.World.VISIT_SCENE,
                        new Dictionary<string, JsonElement>
                        {
                            { "scene name", currentScene.Info.Name },
                        });

                    lastScene = currentScene;
                }
            }
        }
    }
}
