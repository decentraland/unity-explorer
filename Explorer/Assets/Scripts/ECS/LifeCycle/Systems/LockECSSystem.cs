using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.Groups;
using Utility.Multithreading;

namespace ECS.LifeCycle.Systems
{
    /// <summary>
    ///     Locks ECS from modification while the whole cycle of the Unity systems is running
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(SyncedInitializationSystemGroup))] // Before any other scene system
    public partial class LockECSSystem : BaseUnityLoopSystem
    {
        private readonly MultithreadSync multithreadSync;

        internal LockECSSystem(World world, MultithreadSync multithreadSync) : base(world)
        {
            this.multithreadSync = multithreadSync;
        }

        protected override void Update(float t)
        {
            multithreadSync.Acquire();
        }
    }
}
