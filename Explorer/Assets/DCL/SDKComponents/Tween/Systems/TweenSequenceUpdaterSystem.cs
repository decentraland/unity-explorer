using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using SceneRunner.Scene;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.SDKComponents.Tween
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateTransformSystem))]
    [UpdateAfter(typeof(TweenLoaderSystem))]
    [UpdateAfter(typeof(InstantiateTransformSystem))]
    [LogCategory(ReportCategory.TWEEN)]
    public partial class TweenSequenceUpdaterSystem : BaseUnityLoopSystem
    {
        private readonly TweenerPool tweenerPool;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;

        public TweenSequenceUpdaterSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, TweenerPool tweenerPool, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.tweenerPool = tweenerPool;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            UpdatePBTweenSequenceQuery(World);
            UpdateTweenSequenceStateQuery(World);
        }

        [Query]
        private static void UpdatePBTweenSequence(ref PBTween pbTween, ref PBTweenSequence pbTweenSequence, ref SDKTweenSequenceComponent sdkTweenSequenceComponent)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            if (pbTweenSequence.IsDirty || pbTween.IsDirty)
                sdkTweenSequenceComponent.IsDirty = true;
        }

        [Query]
        private void UpdateTweenSequenceState(ref SDKTweenSequenceComponent sdkTweenSequenceComponent, in PBTween pbTween, in PBTweenSequence pbTweenSequence, CRDTEntity sdkEntity, TransformComponent transformComponent)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            if (sdkTweenSequenceComponent.IsDirty)
            {
                SetupTweenSequence(ref sdkTweenSequenceComponent, in pbTween, in pbTweenSequence, transformComponent.Transform);
                UpdateTweenSequenceStateInCRDT(sdkEntity, sdkTweenSequenceComponent);
            }
            else
            {
                UpdateTweenSequenceStateIfChanged(ref sdkTweenSequenceComponent, sdkEntity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTweenSequenceStateIfChanged(ref SDKTweenSequenceComponent sdkTweenSequenceComponent, CRDTEntity sdkEntity)
        {
            TweenStateStatus newState = TweenSDKComponentHelper.GetTweenerState(sdkTweenSequenceComponent.SequenceTweener);
            if (newState != sdkTweenSequenceComponent.TweenStateStatus)
            {
                sdkTweenSequenceComponent.TweenStateStatus = newState;
                UpdateTweenSequenceStateInCRDT(sdkEntity, sdkTweenSequenceComponent);
            }
        }

        private void SetupTweenSequence(ref SDKTweenSequenceComponent sdkTweenSequenceComponent, in PBTween firstTween, in PBTweenSequence pbTweenSequence, Transform transform)
        {
            tweenerPool.ReleaseSequenceTweenerFrom(sdkTweenSequenceComponent);

            TweenLoop? loopType = pbTweenSequence.HasLoop ? pbTweenSequence.Loop : null;
            sdkTweenSequenceComponent.SequenceTweener = tweenerPool.GetSequenceTweener(firstTween, pbTweenSequence.Sequence, loopType, transform);

            sdkTweenSequenceComponent.SequenceTweener.Play();
            sdkTweenSequenceComponent.TweenStateStatus = TweenStateStatus.TsActive;
            sdkTweenSequenceComponent.IsDirty = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTweenSequenceStateInCRDT(CRDTEntity sdkEntity, SDKTweenSequenceComponent sdkTweenSequenceComponent)
        {
            TweenSDKComponentHelper.WriteTweenStateInCRDT(ecsToCRDTWriter, sdkEntity, sdkTweenSequenceComponent.TweenStateStatus);
        }
    }
}

