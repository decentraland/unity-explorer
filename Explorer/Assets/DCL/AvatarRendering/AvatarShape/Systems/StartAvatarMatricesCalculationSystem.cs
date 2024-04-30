using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine.PlayerLoop;

namespace DCL.AvatarRendering.AvatarShape.GPUSkinning
{
    /// <summary>
    ///     TODO Inject it right after <see cref="PreLateUpdate.LegacyAnimationUpdate" />.
    ///     It is crucial to schedule it as early as possible to give Unity some space to decide
    ///     how to distribute workload
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateBefore(typeof(AvatarInstantiatorSystem))]
    public partial class StartAvatarMatricesCalculationSystem : BaseUnityLoopSystem
    {
        internal StartAvatarMatricesCalculationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ExecuteQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void Execute(ref AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent, ref AvatarShapeComponent avatarShapeComponent)
        {
            if (avatarShapeComponent.IsDirty)
                return;

            transformMatrixComponent.ScheduleBoneMatrixCalculation(avatarBase.transform.worldToLocalMatrix);
        }
    }
}
