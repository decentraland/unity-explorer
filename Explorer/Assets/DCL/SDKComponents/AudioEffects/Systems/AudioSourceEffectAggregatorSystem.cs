using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;

namespace DCL.SDKComponents.AudioEffects.Systems
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateBefore(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.AUDIO_EFFECTS)]
    public partial class AudioSourceEffectAggregatorSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly ISceneAudioEffectsRegistry registry;

        internal AudioSourceEffectAggregatorSystem(World world, ISceneAudioEffectsRegistry registry) : base(world)
        {
            this.registry = registry;
        }

        protected override void Update(float t)
        {
            HandleDirtyQuery(World);
            HandleRemovedQuery(World);
        }

        public void FinalizeComponents(in Query query) =>
            registry.Clear();

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HandleDirty(in PBAudioSourceEffect pb)
        {
            if (!pb.IsDirty) return;

            if (string.IsNullOrEmpty(pb.TargetAvatarId))
            {
                registry.Remove(pb);
                return;
            }

            registry.Upsert(pb.TargetAvatarId, pb);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleRemoved(in PBAudioSourceEffect pb)
        {
            registry.Remove(pb);
        }
    }
}
