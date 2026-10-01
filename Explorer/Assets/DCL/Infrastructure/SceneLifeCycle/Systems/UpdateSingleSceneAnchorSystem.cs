using Arch.Core;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SingleScene;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    public partial class UpdateSingleSceneAnchorSystem : BaseUnityLoopSystem
    {
        private readonly SingleSceneMode singleSceneMode;
        private readonly Entity playerEntity;
        private readonly Transform playerTransform;

        internal UpdateSingleSceneAnchorSystem(World world, SingleSceneMode singleSceneMode, Entity playerEntity) : base(world)
        {
            this.singleSceneMode = singleSceneMode;
            this.playerEntity = playerEntity;
            playerTransform = World.Get<CharacterTransform>(playerEntity).Transform;
        }

        protected override void Update(float t)
        {
            if (!singleSceneMode.IsActive)
                return;

            if (World.TryGet(playerEntity, out PlayerTeleportIntent intent))
            {
                singleSceneMode.SetAnchor(intent.IsPositionSet ? intent.Position.ToParcel() : intent.Parcel);
                return;
            }

            if (!singleSceneMode.HasAnchor)
                singleSceneMode.SetAnchor(playerTransform.position.ToParcel());
        }
    }
}
