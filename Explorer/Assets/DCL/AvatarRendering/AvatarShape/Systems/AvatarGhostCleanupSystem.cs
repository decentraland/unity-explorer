using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using Utility;

namespace DCL.AvatarRendering.AvatarShape
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [UpdateBefore(typeof(AvatarCleanUpSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarGhostCleanupSystem : BaseUnityLoopSystem
    {
        internal AvatarGhostCleanupSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            CleanupGhostQuery(World);
        }

        protected override void OnDispose()
        {
            CleanupGhostOnDisposeQuery(World);
        }

        [Query]
        private void CleanupGhost(ref AvatarGhostComponent ghost, ref AvatarBase avatarBase, ref DeleteEntityIntention _)
        {
            Cleanup(ref ghost, ref avatarBase);
        }

        [Query]
        private void CleanupGhostOnDispose(ref AvatarGhostComponent ghost, ref AvatarBase avatarBase)
        {
            Cleanup(ref ghost, ref avatarBase);
        }

        private void Cleanup(ref AvatarGhostComponent ghost, ref AvatarBase avatarBase)
        {
            if (UnityObjectUtils.IsQuitting)
                return;

            // The avatar hierarchy can be destroyed out-of-band before this delete-pending cleanup runs
            if (avatarBase != null && avatarBase.GhostGameObject != null)
                avatarBase.GhostGameObject.SetActive(false);

            UnityObjectUtils.SafeDestroy(ghost.GhostMaterial);
        }
    }
}
