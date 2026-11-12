using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using ECS.Abstract;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>
    ///     Turns the own avatar's animations off while <see cref="LocomotionSuiteSession.AnimationsEnabled" /> is false: the
    ///     Animator (locomotion blend tree and the IK rigs it evaluates) stays disabled, so the model keeps the pose it had
    ///     and slides with the character. <see cref="FinishAvatarMatricesCalculationSystem" /> re-enables the Animator every
    ///     frame from its culling verdict, so this system runs right after it, before the next frame's animation update.
    ///     Movement does not depend on the Animator (no root motion), only what is on screen does.
    /// </summary>
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [UpdateAfter(typeof(FinishAvatarMatricesCalculationSystem))]
    [LogCategory(ReportCategory.MCP)]
    public partial class LocomotionAnimationsOffSystem : BaseUnityLoopSystem
    {
        private readonly LocomotionSuiteSession session;
        private readonly Entity playerEntity;

        internal LocomotionAnimationsOffSystem(World world, LocomotionSuiteSession session, Entity playerEntity) : base(world)
        {
            this.session = session;
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {
            if (session.AnimationsEnabled || !World.TryGet(playerEntity, out AvatarBase? avatarBase) || avatarBase == null)
                return;

            if (avatarBase.AvatarAnimator.enabled)
                avatarBase.AvatarAnimator.enabled = false;
        }
    }
}
