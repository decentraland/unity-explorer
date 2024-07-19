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
using UnityEngine;
using Utility;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class PlayerParcelChangedAnalyticsSystem : BaseUnityLoopSystem
    {
        private const string UNDEFINED = "UNDEFINED";
        private static readonly Vector2Int MIN_INT2 = new (int.MinValue, int.MinValue);

        private readonly IAnalyticsController analytics;
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;

        private readonly Entity playerEntity;

        private Vector2Int oldParcel;
        private ISceneFacade lastScene;

        public PlayerParcelChangedAnalyticsSystem(World world, IAnalyticsController analytics, IRealmData realmData, IScenesCache scenesCache, in Entity playerEntity) : base(world)
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
                bool sceneIsDefined = scenesCache.TryGetByParcel(newParcel, out ISceneFacade? currentScene);

                analytics.Track(AnalyticsEvents.World.MOVE_TO_PARCEL, new JsonObject
                {
                    { "old parcel", oldParcel == MIN_INT2 ? "(NaN, NaN)" : oldParcel.ToString() },
                    { "new parcel", newParcel.ToString() },
                    { "scene hash", sceneIsDefined ? currentScene.Info.Name : UNDEFINED },
                    { "is empty scene", sceneIsDefined ? currentScene.IsEmpty : UNDEFINED },
                });

                oldParcel = newParcel;
            }
        }
    }
}
