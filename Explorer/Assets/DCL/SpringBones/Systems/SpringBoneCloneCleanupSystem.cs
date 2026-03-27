using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using UnityEngine;

namespace DCL.SpringBones
{
    /// <summary>
    ///     Releases deferred clone transforms back to the pool after the spring bone simulation
    ///     has flushed the combined TransformAccessArray.
    /// </summary>
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(SpringBonesSimulationSystem))]
    public partial class SpringBoneCloneCleanupSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<Transform> transformPool;

        private SpringBoneCloneCleanupSystem(World world, IComponentPool<Transform> transformPool) : base(world)
        {
            this.transformPool = transformPool;
        }

        protected override void Update(float t) =>
            ReleaseQuery(World);

        protected override void OnDispose() =>
            ReleaseQuery(World);

        [Query]
        private void Release(ref SpringBonePendingCloneRelease pending)
        {
            foreach (Transform clone in pending.Clones) transformPool.Release(clone);
            pending.Clones.Clear();
        }
    }
}
