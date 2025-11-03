using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.SDKComponents.Tween
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [LogCategory(ReportCategory.TWEEN)]
    [ThrottlingEnabled]
    public partial class TweenLoaderSystem : BaseUnityLoopSystem
    {
        private readonly bool tweenSequenceSupport;

        public TweenLoaderSystem(World world, bool tweenSequenceSupport) : base(world)
        {
            this.tweenSequenceSupport = tweenSequenceSupport;
        }

        protected override void Update(float t)
        {
            if (tweenSequenceSupport)
            {
                LoadTween_WithTweenSequenceSupportQuery(World);
                LoadTweenSequenceQuery(World);
            }
            else
            {
                LoadTweenQuery(World);
            }
        }

        [Query]
        [None(typeof(SDKTweenComponent), typeof(PBTweenSequence))]
        private void LoadTween_WithTweenSequenceSupport(Entity entity, ref PBTween pbTween)
            => LoadTween(entity, ref pbTween);

        [Query]
        [None(typeof(SDKTweenComponent))]
        private void LoadTween(Entity entity, ref PBTween pbTween)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            var sdkTweenComponent = new SDKTweenComponent
            {
                IsDirty = true,
            };

            World.Add(entity, sdkTweenComponent);
        }

        [Query]
        [None(typeof(SDKTweenSequenceComponent))]
        private void LoadTweenSequence(Entity entity, ref PBTween pbTween, ref PBTweenSequence pbTweenSequence)
        {
            // For sequences, PBTween must exist and be valid
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            var sdkTweenSequenceComponent = new SDKTweenSequenceComponent
            {
                IsDirty = true,
            };

            World.Add(entity, sdkTweenSequenceComponent);
        }
    }
}
