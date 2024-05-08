using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;

namespace ECS.LifeCycle.Systems
{
    /// <summary>
    ///     Resets dirty flag at the end of the frame
    /// </summary>
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]

    // Update survived components only
    [UpdateAfter(typeof(CleanUpGroup))]
    [ThrottlingEnabled] // Reacts on the SDK update only
    public partial class ResetDirtyFlagSystem<T> : BaseUnityLoopSystem where T: IDirtyMarker
    {
        internal ResetDirtyFlagSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ResetDirtyQuery(World);
        }

        [Query]
        private void ResetDirty(ref T component)
        {
            component.IsDirty = false;
        }
    }
}
