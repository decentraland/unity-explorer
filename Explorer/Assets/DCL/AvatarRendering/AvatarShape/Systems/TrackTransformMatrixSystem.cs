using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using System.Linq;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    /// <summary>
    ///     Tracks potential bugs with AvatarMatrixCalculation
    ///     if <see cref="AvatarCleanUpSystem" /> didn't work out for some reason
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    [UpdateAfter(typeof(AvatarCleanUpSystem))]
    public partial class TrackTransformMatrixSystem : BaseUnityLoopSystem
    {
        internal TrackTransformMatrixSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            TrackUnfinishedMatrixCalculationQuery(World);
            TrackAbandonedTransformMatrixQuery(World);
        }

        [Query]
        private void TrackAbandonedTransformMatrix(Entity entity, ref AvatarTransformMatrixComponent avatarTransformMatrixComponent, in DeleteEntityIntention deleteEntityIntention)
        {
            if (!deleteEntityIntention.DeferDeletion && !avatarTransformMatrixComponent.disposed)
            {
                avatarTransformMatrixComponent.Dispose();
                ReportHub.LogError(ReportCategory.AVATAR, $"{nameof(AvatarTransformMatrixComponent)} was not disposed properly. Archetype:\n {LogArchetype(World.GetArchetype(entity))}");
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void TrackUnfinishedMatrixCalculation(Entity entity, ref AvatarTransformMatrixComponent avatarTransformMatrixComponent)
        {
            if (!avatarTransformMatrixComponent.completed)
            {
                avatarTransformMatrixComponent.CompleteBoneMatrixCalculations();
                ReportHub.LogError(ReportCategory.AVATAR, $"{nameof(AvatarTransformMatrixComponent)} was not completed properly. {entity.ToString()} Archetype:\n {LogArchetype(World.GetArchetype(entity))}");
            }
        }

        private static string LogArchetype(Archetype archetype)
        {
            return string.Join(",", archetype.Types.Select(p => p.Type.Name));
        }
    }
}
