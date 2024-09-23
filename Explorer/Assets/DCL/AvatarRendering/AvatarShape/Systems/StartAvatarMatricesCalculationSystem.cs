using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    /// <summary>
    ///     It is crucial to schedule it as early as possible to give Unity some space to decide
    ///     how to distribute workload
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))] // right after AvatarBase is instantiated
    public partial class StartAvatarMatricesCalculationSystem : BaseUnityLoopSystem
    {
        private readonly AvatarTransformMatrixJobWrapper avatarTransformMatrixBatchJob;

        internal StartAvatarMatricesCalculationSystem(World world, AvatarTransformMatrixJobWrapper jobWrapper) :
            base(world)
        {
            avatarTransformMatrixBatchJob = jobWrapper;
        }

        protected override void Update(float t)
        {
            ExecuteQuery(World);
            avatarTransformMatrixBatchJob.ScheduleBoneMatrixCalculation();
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void Execute(ref AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            avatarTransformMatrixBatchJob.UpdateAvatar(avatarBase, ref transformMatrixComponent);
        }
    }
}
