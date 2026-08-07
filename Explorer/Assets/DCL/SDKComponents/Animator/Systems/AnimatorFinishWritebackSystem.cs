using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Animator.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.GLTFContainer.Components;
using Google.Protobuf.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

using UAnimator = UnityEngine.Animator;

namespace DCL.SDKComponents.Animator.Systems
{
    /// <summary>
    ///     Detects non-looping animation clips that reached their natural end and PUTs the updated
    ///     <see cref="PBAnimator" /> (with the finished states' playing == false) back to the scene, so scenes can
    ///     observe completion instead of hardcoding timeouts matching the clip length.
    /// </summary>
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(SDKAnimatorUpdaterSystem))]
    [UpdateAfter(typeof(AnimationPlayerSystem))]
    [UpdateAfter(typeof(LegacyAnimationPlayerSystem))]
    [LogCategory(ReportCategory.ANIMATOR)]
    [ThrottlingEnabled]
    public partial class AnimatorFinishWritebackSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        internal AnimatorFinishWritebackSystem(World world, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            DetectFinishedMecanimClipsQuery(World);
            DetectFinishedLegacyClipsQuery(World);
        }

        [Query]
        [None(typeof(LegacyGltfAnimation), typeof(DeleteEntityIntention))]
        private void DetectFinishedMecanimClips(in CRDTEntity sdkEntity, ref PBAnimator pbAnimator, ref SDKAnimatorComponent sdkAnimator, ref GltfContainerComponent gltfContainerComponent)
        {
            if (!CanDetect(in sdkAnimator, in gltfContainerComponent)) return;

            List<UAnimator> animators = gltfContainerComponent.Promise.Result!.Value.Asset.Animators;
            List<SDKAnimationState> states = sdkAnimator.SDKAnimationStates;
            var anyFinished = false;

            for (var i = 0; i < states.Count; i++)
            {
                if (!IsTracked(states[i])) continue;

                if (!UpdateLatch(states, i, IsMecanimClipActive(animators, states[i].Clip))) continue;

                MarkClipStopped(pbAnimator, states[i].Clip);
                anyFinished = true;
            }

            if (anyFinished)
                PutAnimator(in sdkEntity, pbAnimator);
        }

        [Query]
        [All(typeof(LegacyGltfAnimation))]
        [None(typeof(DeleteEntityIntention))]
        private void DetectFinishedLegacyClips(in CRDTEntity sdkEntity, ref PBAnimator pbAnimator, ref SDKAnimatorComponent sdkAnimator, ref GltfContainerComponent gltfContainerComponent)
        {
            if (!CanDetect(in sdkAnimator, in gltfContainerComponent)) return;

            List<Animation> animations = gltfContainerComponent.Promise.Result!.Value.Asset.Animations;
            List<SDKAnimationState> states = sdkAnimator.SDKAnimationStates;
            var anyFinished = false;

            for (var i = 0; i < states.Count; i++)
            {
                if (!IsTracked(states[i])) continue;

                if (!UpdateLatch(states, i, IsLegacyClipActive(animations, states[i].Clip))) continue;

                MarkClipStopped(pbAnimator, states[i].Clip);
                anyFinished = true;
            }

            if (anyFinished)
                PutAnimator(in sdkEntity, pbAnimator);
        }

        /// <summary>
        ///     Only non-looping states asked to play by the scene can finish naturally.
        /// </summary>
        internal static bool IsTracked(in SDKAnimationState state) =>
            state.Playing && !state.Loop;

        /// <summary>
        ///     A dirty <see cref="SDKAnimatorComponent" /> means a scene write has not been applied to the Unity
        ///     animators yet, so any playback observation would be stale.
        /// </summary>
        private static bool CanDetect(in SDKAnimatorComponent sdkAnimator, in GltfContainerComponent gltfContainerComponent) =>
            gltfContainerComponent.State == LoadingState.Finished && !sdkAnimator.IsDirty;

        /// <summary>
        ///     Advances the edge-trigger latch for a single state and returns whether the clip just finished:
        ///     the state must have been observed actively playing before, and no animator reports it active anymore.
        ///     A clip that never started (e.g. a mecanim transition still pending) is not a finish.
        /// </summary>
        internal static bool UpdateLatch(List<SDKAnimationState> states, int index, bool isActive)
        {
            SDKAnimationState state = states[index];

            if (isActive)
            {
                if (!state.ObservedPlaying)
                    states[index] = state.WithObserved();

                return false;
            }

            if (!state.ObservedPlaying)
                return false;

            states[index] = state.AsStopped();
            return true;
        }

        /// <summary>
        ///     Pure mecanim activity predicate: a clip is active while its layer's current state is the clip state
        ///     and the playback cursor has not reached the end. speed == 0 freezes normalizedTime below 1,
        ///     so a frozen clip never finishes.
        /// </summary>
        internal static bool IsMecanimStateActive(bool stateIsClip, float normalizedTime) =>
            stateIsClip && normalizedTime < 1f;

        private static bool IsMecanimClipActive(List<UAnimator> animators, string clip)
        {
            for (var i = 0; i < animators.Count; i++)
            {
                UAnimator animator = animators[i];
                int layerIndex = animator.GetLayerIndex(clip);

                if (layerIndex == -1) continue;

                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);

                if (IsMecanimStateActive(stateInfo.IsName(clip), stateInfo.normalizedTime))
                    return true;
            }

            return false;
        }

        private static bool IsLegacyClipActive(List<Animation> animations, string clip)
        {
            for (var i = 0; i < animations.Count; i++)
                if (animations[i].IsPlaying(clip))
                    return true;

            return false;
        }

        private static void MarkClipStopped(PBAnimator pbAnimator, string clip)
        {
            RepeatedField<PBAnimationState> pbStates = pbAnimator.States;

            for (var i = 0; i < pbStates.Count; i++)
            {
                if (!string.Equals(pbStates[i].Clip, clip, StringComparison.Ordinal)) continue;

                // In-place mutation without dirtying: SDKAnimatorUpdaterSystem must not treat this observational
                // update as a scene write (re-running SetAnimationState would snap poses).
                pbStates[i].Playing = false;
                return;
            }
        }

        private void PutAnimator(in CRDTEntity sdkEntity, PBAnimator pbAnimator)
        {
            // Sharing the PBAnimationState references into the rented message is safe: the outgoing serializer only
            // reads them, the pool's clear-on-get merely empties the rented message's own RepeatedField (it never
            // touches the shared instances), and incoming CRDT updates deserialize into fresh instances instead of
            // mutating these. Only the PBAnimator container itself must be the rented copy — PUTting the live
            // entity-attached instance would put it on the shared pool free-list (aliasing corruption).
            ecsToCRDTWriter.PutMessage<PBAnimator, PBAnimator>(
                static (dst, src) => dst.States.AddRange(src.States),
                sdkEntity, pbAnimator);
        }
    }
}
