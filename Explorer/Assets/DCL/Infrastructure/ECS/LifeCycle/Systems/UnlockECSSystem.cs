using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.Groups;
using Utility.Multithreading;

namespace ECS.LifeCycle.Systems
{

// MultiThreadSync is not required in WebGL because it's always the single threaded environment
// Even if we use WebWorkers they still provide message passing to the main thread safely
#if !UNITY_WEBGL
    /// <summary>
    ///     Unlocks ECS when the whole cycle of the player loop has processed
    /// </summary>
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [UpdateAfter(typeof(SyncedPreRenderingSystemGroup))] // After all other systems
    public partial class UnlockECSSystem : BaseUnityLoopSystem
    {
        private readonly MultiThreadSync.BoxedScope boxedScope;
        private bool skippedFirstFrame;

        internal UnlockECSSystem(World world, MultiThreadSync.BoxedScope boxedScope) : base(world)
        {
            this.boxedScope = boxedScope;
        }

        protected override void Update(float t)
        {
            boxedScope.ReleaseIfAcquired();
        }
    }
#endif

}
