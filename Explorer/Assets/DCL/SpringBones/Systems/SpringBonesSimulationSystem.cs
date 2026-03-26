using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using System.Collections.Generic;
using UnityEngine;
using UniVRM10.FastSpringBones;

namespace DCL.SpringBones
{
    /// <summary>
    ///     Ticks the FastSpringBone simulation each frame and releases deferred clone transforms
    ///     after the combined TransformAccessArray has been rebuilt.
    /// </summary>
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(SpringBoneRegistrationSystem))]
    [UpdateBefore(typeof(StartAvatarMatricesCalculationSystem))]
    public partial class SpringBonesSimulationSystem : BaseUnityLoopSystem
    {
        private readonly FastSpringBoneService springBoneService;
        private readonly IComponentPool<Transform> transformPool;
        private readonly List<Transform> pendingCloneRelease;

        internal SpringBonesSimulationSystem(World world, FastSpringBoneService springBoneService,
            IComponentPool<Transform> transformPool, List<Transform> pendingCloneRelease) : base(world)
        {
            this.springBoneService = springBoneService;
            this.transformPool = transformPool;
            this.pendingCloneRelease = pendingCloneRelease;
        }

        protected override void Update(float t)
        {
            springBoneService.ManualUpdate(t);

            // Release old clones AFTER ManualUpdate has flushed the combined TAA
            if (pendingCloneRelease.Count == 0) return;

            foreach (Transform clone in pendingCloneRelease)
                transformPool.Release(clone);

            pendingCloneRelease.Clear();
        }
    }
}
