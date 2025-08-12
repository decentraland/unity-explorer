using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Utilities;
using ECS;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    [UpdateAfter(typeof(ChangeCharacterPositionGroup))]
    public partial class PlayerParcelTrackingSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;
        private readonly PlayerParcelTrackerService parcelTracker;
        private readonly IScenesCache scenesCache;
        private readonly IRealmData realmData;

        private Vector2Int lastParcel;

        internal PlayerParcelTrackingSystem(World world, Entity playerEntity, PlayerParcelTrackerService parcelTracker, IScenesCache scenesCache, IRealmData realmData) : base(world)
        {
            this.playerEntity = playerEntity;
            this.parcelTracker = parcelTracker;
            this.scenesCache = scenesCache;
            this.realmData = realmData;
            lastParcel = new Vector2Int(int.MinValue, int.MinValue);
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured)
            {
                lastParcel = new Vector2Int(int.MinValue, int.MinValue);
                return;
            }
            Vector2Int currentParcel = World.Get<CharacterTransform>(playerEntity).Transform.ParcelPosition();

            if (currentParcel != lastParcel)
            {
                bool sceneIsDefined = scenesCache.TryGetByParcel(currentParcel, out ISceneFacade? currentScene);

                PlayerParcelData parcelData = sceneIsDefined
                    ? new PlayerParcelData(currentParcel, currentScene!.Info.Name, currentScene.IsEmpty, true)
                    : new PlayerParcelData(currentParcel, string.Empty);

                parcelTracker.UpdateParcelData(parcelData);

                lastParcel = currentParcel;
            }
        }
    }
}
