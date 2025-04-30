using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using Segment.Serialization;
using UnityEngine;
using Utility;
using Assets.DCL.RealtimeCommunication;

namespace DCL.Analytics.Systems
{
    [LogCategory(ReportCategory.ANALYTICS)]
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class PlayerParcelChangedAnalyticsSystem : BaseUnityLoopSystem
    {
        private static readonly Vector2Int MIN_INT2 = new (int.MinValue, int.MinValue);

        private readonly IAnalyticsController analytics;
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;
        private readonly IRealtimeReports realtimeReports;

        private readonly Entity playerEntity;

        private Vector2Int oldParcel;
        private ISceneFacade lastScene;

        public PlayerParcelChangedAnalyticsSystem(
            World world,
            IAnalyticsController analytics,
            IRealmData realmData,
            IScenesCache scenesCache,
            IRealtimeReports realtimeReports,
            Entity playerEntity
        ) : base(world)
        {
            this.analytics = analytics;
            this.realmData = realmData;
            this.scenesCache = scenesCache;
            this.realtimeReports = realtimeReports;
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

            Vector2Int newParcel =  World.Get<CharacterTransform>(playerEntity).Transform.ParcelPosition();

            if (newParcel != oldParcel)
            {
                bool sceneIsDefined = scenesCache.TryGetByParcel(newParcel, out ISceneFacade? currentScene);

                var json = new JsonObject
                {
                    { "old_parcel", oldParcel == MIN_INT2 ? "(NaN, NaN)" : oldParcel.ToString() },
                    { "new_parcel", newParcel.ToString() },
                    { "scene_hash", sceneIsDefined ? currentScene.Info.Name : IAnalyticsController.UNDEFINED },
                    { "is_empty_scene", sceneIsDefined ? currentScene.IsEmpty : IAnalyticsController.UNDEFINED },
                };

                string jsonContent = json.ToString();

                analytics.Track(AnalyticsEvents.World.MOVE_TO_PARCEL, json);
                realtimeReports.Report(jsonContent);

                oldParcel = newParcel;
            }
        }
    }
}
