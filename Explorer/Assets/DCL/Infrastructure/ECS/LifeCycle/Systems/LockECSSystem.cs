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
        private readonly MultiThreadSync.BoxedScope boxedScope;
        private readonly MultiThreadSync.Owner owner;

        internal LockECSSystem(World world, MultiThreadSync.BoxedScope boxedScope, MultiThreadSync.Owner owner) : base(world)
        {
            this.boxedScope = boxedScope;
            this.owner = owner;
        }

        protected override void Update(float t)
        {
            boxedScope.Acquire(owner);
        }
    }
}
