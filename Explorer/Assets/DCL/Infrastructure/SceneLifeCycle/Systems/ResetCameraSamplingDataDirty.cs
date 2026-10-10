using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterMotion.Systems;
using ECS.Abstract;
using ECS.Prioritization.Components;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(ChangeCharacterPositionGroup))]
    [UpdateBefore(typeof(TeleportCharacterSystem))]
    public partial class ResetCameraSamplingDataDirty : BaseUnityLoopSystem
    {
        private readonly IRealmData realmData;
        private readonly CameraSamplingData cameraSamplingData;

        internal ResetCameraSamplingDataDirty(World world, IRealmData realmData, CameraSamplingData cameraSamplingData) : base(world)
        {
            this.realmData = realmData;
            this.cameraSamplingData = cameraSamplingData;
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured) return;

            cameraSamplingData.IsDirty = false;
        }
    }
}
