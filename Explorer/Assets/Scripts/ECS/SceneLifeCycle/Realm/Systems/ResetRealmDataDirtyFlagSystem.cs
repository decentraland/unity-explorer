using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;

namespace ECS.SceneLifeCycle.Realm.Systems
{
    [LogCategory(ReportCategory.REALM_DATA_DIRTY_RESET_SYSTEM)]
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [UpdateAfter(typeof(CleanUpGroup))]
    public partial class ResetRealmDataDirtyFlagSystem : BaseUnityLoopSystem
    {
        private readonly RealmData realmData;

        public ResetRealmDataDirtyFlagSystem(World world, RealmData realmData) : base (world)
        {
            this.realmData = realmData;
        }

        protected override void Update(float t)
        {
            if (!realmData.IsDirty) return;

            realmData.IsDirty = false;
        }
    }
}
