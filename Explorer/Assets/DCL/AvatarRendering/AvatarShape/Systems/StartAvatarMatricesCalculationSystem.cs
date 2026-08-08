using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;

namespace DCL.AvatarRendering.AvatarShape
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
            RegisterMainPlayerQuery(World);
            RegisterRemoteAvatarsQuery(World);

            // Refresh each registered avatar's matrix count from the authoritative
            // AvatarCustomSkinningComponent.BoneCount (the exact range ComputeSkinning uploads) AFTER
            // registration and BEFORE scheduling, so a re-equip that changes the bone count is honoured
            // even when the avatar (e.g. the main player) is not released/re-registered.
            RefreshBoneCountsQuery(World);

            avatarTransformMatrixBatchJob.ScheduleBoneMatrixCalculation();
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void RegisterMainPlayer(ref AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            //Already registered
            if (transformMatrixComponent.IndexInGlobalJobArray.IsValid())
                return;

            avatarTransformMatrixBatchJob.RegisterMainPlayerAvatar(avatarBase, ref transformMatrixComponent);
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        private void RegisterRemoteAvatars(ref AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            //Already registered
            if (transformMatrixComponent.IndexInGlobalJobArray.IsValid())
                return;

            avatarTransformMatrixBatchJob.RegisterAvatar(avatarBase, ref transformMatrixComponent);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void RefreshBoneCounts(ref AvatarTransformMatrixComponent transformMatrixComponent, ref AvatarCustomSkinningComponent skinningComponent)
        {
            avatarTransformMatrixBatchJob.SetBoneCount(ref transformMatrixComponent, skinningComponent.BoneCount);
        }
    }
}
