using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Materials.Components;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using SceneRunner.Scene;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.SDKComponents.Tween
{
    /// <summary>
    /// Handles the update logic of PBTween and PBTweenSequence components
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UpdateTransformSystem))] // The transform has to be correctly updated at least once before the Tween can use it
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
            bool isCurrentScene = sceneStateProvider.IsCurrent;
            UpdatePBTweenQuery(World);
            UpdateTweenTransformQuery(World, isCurrentScene);
            UpdateTweenTextureQuery(World, isCurrentScene);
            UpdateTweenSequenceStateQuery(World, isCurrentScene);
        }

        [Query]
        [None(typeof(PBTweenSequence))]
        private void UpdatePBTween(ref PBTween pbTween, ref SDKTweenComponent sdkTweenComponent)
        {
            TweenSDKComponentHelper.UpdatePBTween(ref pbTween, ref sdkTweenComponent);
        }

        [Query]
        [None(typeof(PBTweenSequence))]
        private void UpdateTweenTransform(ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, in PBTween pbTween, CRDTEntity sdkEntity, TransformComponent transformComponent, bool isCurrentScene)
        {
            TweenSDKComponentHelper.UpdateTweenTransform(ref sdkTweenComponent, ref sdkTransform, in pbTween, sdkEntity, transformComponent, tweenerPool, ecsToCRDTWriter, isCurrentScene);
        }

        [Query]
        [None(typeof(PBTweenSequence))]
        private void UpdateTweenTexture(CRDTEntity sdkEntity, in PBTween pbTween, ref SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent, bool isCurrentScene)
        {
            TweenSDKComponentHelper.UpdateTweenTexture(sdkEntity, in pbTween, ref sdkTweenComponent, ref materialComponent, tweenerPool, ecsToCRDTWriter, isCurrentScene);
        }

        /// <summary>
        /// Merged from former UpdatePBTweenSequence + UpdateTweenSequenceState queries to halve archetype traversal.
        /// </summary>
        [Query]
        private void UpdateTweenSequenceState(Entity entity, in PBTween pbTween, in PBTweenSequence pbTweenSequence,
            ref SDKTweenSequenceComponent sdkTweenSequenceComponent, ref SDKTransform sdkTransform,
            CRDTEntity sdkEntity, ref TransformComponent transformComponent, bool isCurrentScene)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            // Propagate dirty flag from SDK components (was formerly UpdatePBTweenSequence)
            if (pbTweenSequence.IsDirty || pbTween.IsDirty)
                sdkTweenSequenceComponent.IsDirty = true;

            if (sdkTweenSequenceComponent.IsDirty)
            {
                // Single pass over the sequence list to determine both flags at once
                AnalyzeSequence(in pbTween, in pbTweenSequence, out bool requiresMaterial, out bool hasTransformTweens);

                Material? material = null;

                if (requiresMaterial)
                {
                    if (!World.TryGet(entity, out MaterialComponent materialComponent) || materialComponent.Result == null)
                        return; // The Material Component may be configured in a future frame

                    material = materialComponent.Result;
                }

                SetupTweenSequence(ref sdkTweenSequenceComponent, in pbTween, in pbTweenSequence, transformComponent.Transform, material, hasTransformTweens);
                UpdateTweenSequenceStateAndTransform(sdkEntity, sdkTweenSequenceComponent, ref sdkTransform, ref transformComponent, isCurrentScene);
            }
            else
            {
                UpdateTweenSequenceStateIfChanged(ref sdkTweenSequenceComponent, ref sdkTransform, sdkEntity, ref transformComponent, isCurrentScene);
            }
        }

        /// <summary>
        /// Single pass over the sequence to compute both flags, avoiding two separate iterations.
        /// Uses indexed access to avoid enumerator allocation on the protobuf RepeatedField.
        /// </summary>
        private static void AnalyzeSequence(in PBTween firstTween, in PBTweenSequence pbTweenSequence, out bool requiresMaterial, out bool hasTransformTweens)
        {
            requiresMaterial = IsTextureTween(firstTween.ModeCase);
            hasTransformTweens = IsTransformTween(firstTween.ModeCase);

            var sequence = pbTweenSequence.Sequence;
            for (int i = 0; i < sequence.Count && !(requiresMaterial && hasTransformTweens); i++)
            {
                PBTween.ModeOneofCase mode = sequence[i].ModeCase;
                if (!requiresMaterial) requiresMaterial = IsTextureTween(mode);
                if (!hasTransformTweens) hasTransformTweens = IsTransformTween(mode);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsTextureTween(PBTween.ModeOneofCase mode) =>
            mode == PBTween.ModeOneofCase.TextureMove || mode == PBTween.ModeOneofCase.TextureMoveContinuous;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsTransformTween(PBTween.ModeOneofCase mode) =>
            mode == PBTween.ModeOneofCase.Move || mode == PBTween.ModeOneofCase.Rotate || mode == PBTween.ModeOneofCase.Scale ||
            mode == PBTween.ModeOneofCase.MoveContinuous || mode == PBTween.ModeOneofCase.RotateContinuous ||
            mode == PBTween.ModeOneofCase.MoveRotateScale;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTweenSequenceStateIfChanged(ref SDKTweenSequenceComponent sdkTweenSequenceComponent, ref SDKTransform sdkTransform, CRDTEntity sdkEntity, ref TransformComponent transformComponent, bool isCurrentScene)
        {
            TweenStateStatus newState = TweenSDKComponentHelper.GetTweenerState(sdkTweenSequenceComponent.SequenceTweener);
            if (newState != sdkTweenSequenceComponent.TweenStateStatus)
            {
                sdkTweenSequenceComponent.TweenStateStatus = newState;
                UpdateTweenSequenceStateAndTransform(sdkEntity, sdkTweenSequenceComponent, ref sdkTransform, ref transformComponent, isCurrentScene);
            }
            else if (newState == TweenStateStatus.TsActive)
            {
                // Update transform while sequence is playing
                if (sdkTweenSequenceComponent.HasTransformTweens)
                    UpdateSequenceTransform(sdkEntity, ref sdkTransform, ref transformComponent, isCurrentScene);
            }
        }

        private void SetupTweenSequence(ref SDKTweenSequenceComponent sdkTweenSequenceComponent, in PBTween firstTween, in PBTweenSequence pbTweenSequence, Transform transform, Material? material, bool hasTransformTweens)
        {
            tweenerPool.ReleaseSequenceTweenerFrom(sdkTweenSequenceComponent);

            TweenLoop? loopType = pbTweenSequence.HasLoop ? pbTweenSequence.Loop : null;
            sdkTweenSequenceComponent.SequenceTweener = tweenerPool.GetSequenceTweener(firstTween, pbTweenSequence.Sequence, loopType, transform, material);

            sdkTweenSequenceComponent.HasTransformTweens = hasTransformTweens;

            sdkTweenSequenceComponent.SequenceTweener.Play();
            sdkTweenSequenceComponent.TweenStateStatus = TweenStateStatus.TsActive;
            sdkTweenSequenceComponent.IsDirty = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTweenSequenceStateAndTransform(CRDTEntity sdkEntity, SDKTweenSequenceComponent sdkTweenSequenceComponent, ref SDKTransform sdkTransform, ref TransformComponent transformComponent, bool isCurrentScene)
        {
            if (sdkTweenSequenceComponent.HasTransformTweens)
                UpdateSequenceTransform(sdkEntity, ref sdkTransform, ref transformComponent, isCurrentScene);

            TweenSDKComponentHelper.WriteTweenStateInCRDT(ecsToCRDTWriter, sdkEntity, sdkTweenSequenceComponent.TweenStateStatus);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateSequenceTransform(CRDTEntity sdkEntity, ref SDKTransform sdkTransform, ref TransformComponent transformComponent, bool isCurrentScene)
        {
            // Read back from Unity Transform (DOTween Sequence updates it directly)
            TweenSDKComponentHelper.SyncTransformToSDKTransform(transformComponent.Transform, ref sdkTransform, ref transformComponent, isCurrentScene);
            TweenSDKComponentHelper.WriteSDKTransformUpdateInCRDT(sdkTransform, ecsToCRDTWriter, sdkEntity);
        }
    }
}
