using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.Groups;
using Utility.Multithreading;

namespace ECS.LifeCycle.Systems
{
    /// <summary>
    ///     Unlocks ECS when the whole cycle of the player loop has processed
    /// </summary>
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [UpdateAfter(typeof(SyncedPostRenderingSystemGroup))] // After all other systems
    public partial class UnlockECSSystem : BaseUnityLoopSystem
    {
        private readonly MutexSync mutexSync;

        internal UnlockECSSystem(World world, MutexSync mutexSync) : base(world)
        {
            this.mutexSync = mutexSync;
        }

        protected override void Update(float t)
        {
            // We could skip the first frame of LockECSSystem
            if (mutexSync.Acquired)
                mutexSync.Release();
        }
    }
}
