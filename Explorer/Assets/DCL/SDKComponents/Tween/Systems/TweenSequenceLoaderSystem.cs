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
    /// <summary>
    /// Handles the loading of PBTween and PBTweenSequence components
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [LogCategory(ReportCategory.TWEEN)]
    [ThrottlingEnabled]
    public partial class TweenSequenceLoaderSystem : BaseUnityLoopSystem
    {
        public TweenSequenceLoaderSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            LoadTweenQuery(World);
            LoadTweenSequenceQuery(World);
        }

        [Query]
        [None(typeof(SDKTweenComponent), typeof(PBTweenSequence))]
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
        [All(typeof(PBTweenSequence))]
        private void LoadTweenSequence(Entity entity, ref PBTween pbTween)
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

