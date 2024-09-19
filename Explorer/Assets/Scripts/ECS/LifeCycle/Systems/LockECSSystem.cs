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
        private readonly MultithreadSync.BoxedScope boxedScope;
        private readonly MultithreadSync.Owner owner;

        internal LockECSSystem(World world, MultithreadSync.BoxedScope boxedScope, MultithreadSync.Owner owner) : base(world)
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
