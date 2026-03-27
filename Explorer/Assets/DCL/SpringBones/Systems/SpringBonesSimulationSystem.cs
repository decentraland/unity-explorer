using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.Diagnostics;
using ECS.Abstract;
using UniVRM10.FastSpringBones;

namespace DCL.SpringBones
{
    /// <summary>
    ///     Ticks the FastSpringBone simulation each frame.
    /// </summary>
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(SpringBoneRegistrationSystem))]
    [UpdateBefore(typeof(StartAvatarMatricesCalculationSystem))]
    public partial class SpringBonesSimulationSystem : BaseUnityLoopSystem
    {
        private readonly FastSpringBoneService springBoneService;

        internal SpringBonesSimulationSystem(World world, FastSpringBoneService springBoneService) : base(world)
        {
            this.springBoneService = springBoneService;
        }

        protected override void Update(float t)
        {
            springBoneService.ManualUpdate(t);
        }
    }
}
