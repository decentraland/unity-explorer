using Arch.Core;
using Arch.SystemGroups;
using DCL.Character.Components;
using ECS.Abstract;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Detects the scene the player is currently in
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class UpdateCurrentSceneSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;

        private Vector2Int lastParcelProcessed;

        internal UpdateCurrentSceneSystem(World world, IRealmData realmData, IScenesCache scenesCache, Entity playerEntity) : base(world)
        {
            this.realmData = realmData;
            this.scenesCache = scenesCache;
            this.playerEntity = playerEntity;
            ResetProcessedParcel();
        }

        private void ResetProcessedParcel()
        {
            lastParcelProcessed = new Vector2Int(int.MinValue, int.MinValue);
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured)
            {
                ResetProcessedParcel();
                return;
            }

            Vector3 playerPos = World.Get<CharacterTransform>(playerEntity).Transform.position;
            Vector2Int parcel = ParcelMathHelper.FloorToParcel(playerPos);

            if (lastParcelProcessed == parcel) return;

            scenesCache.TryGetByParcel(lastParcelProcessed, out ISceneFacade? lastProcessedScene);
            scenesCache.TryGetByParcel(parcel, out ISceneFacade? currentScene);

            if (lastProcessedScene != currentScene)
            {
                lastProcessedScene?.SetIsCurrent(false);
                currentScene?.SetIsCurrent(true);
            }
            else
                currentScene?.SetIsCurrent(true);

            lastParcelProcessed = parcel;
        }
    }
}
