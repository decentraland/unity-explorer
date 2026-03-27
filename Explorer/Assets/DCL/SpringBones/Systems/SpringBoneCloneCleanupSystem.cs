using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SpringBones
{
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(SpringBonesSimulationSystem))]
    public partial class SpringBoneCloneCleanupSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<Transform> transformPool;

        internal SpringBoneCloneCleanupSystem(World world, IComponentPool<Transform> transformPool) : base(world)
        {
            this.transformPool = transformPool;
        }

        protected override void Update(float t)
        {
            ReleaseClonesQuery(World);
            ReleaseOnDeleteQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ReleaseClones(ref SpringBonePendingCloneRelease pending)
        {
            if (pending.Pending.Count == 0) return;

            foreach (Transform clone in pending.Pending) transformPool.Release(clone);
            pending.Pending.Clear();
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void ReleaseOnDelete(ref SpringBonePendingCloneRelease pending)
        {
            ReleaseClones(ref pending);

            ListPool<Transform>.Release(pending.Pending);
        }
    }
}
