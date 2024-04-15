using Arch.Core;
using Arch.SystemGroups;
using DCL.Character.Components;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
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
        private Vector2Int? pendingParcel;

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

            // If the scene was not cached but it's pending
            if (parcel == pendingParcel && scenesCache.TryGetByParcel(parcel, out ISceneFacade sceneFacade))
            {
                sceneFacade.SetIsCurrent(true);
                pendingParcel = null;
            }

            if (lastParcelProcessed == parcel) return;

            // Reset the previous current scene, it's ok if it's not cached (already)
            if (scenesCache.TryGetByParcel(lastParcelProcessed, out sceneFacade))
                sceneFacade.SetIsCurrent(false);

            if (scenesCache.TryGetByParcel(parcel, out sceneFacade))
            {
                sceneFacade.SetIsCurrent(true);
                pendingParcel = null;
            }
            else
                pendingParcel = parcel;

            lastParcelProcessed = parcel;

        }
    }
}
