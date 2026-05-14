using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PerformanceAndDiagnostics.Optimization.Renderer;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using Utility.Animations;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Emotes.Play
{
    public class EmotePlayer
    {
        private const string AVATAR_ANIMATION_PLACEHOLDER_NAME = "AvatarAnimationPlaceholder";
        private const string PROP_ANIMATION_PLACEHOLDER_NAME = "PropAnimationPlaceholder";

        private readonly GameObjectPool<AudioSource> audioSourcePool;
        private readonly Action<EmoteReferences> releaseEmoteReferences;
        private readonly Dictionary<GameObject, GameObjectPool<EmoteReferences>> pools = new ();
        private readonly Dictionary<EmoteReferences, GameObjectPool<EmoteReferences>> emotesInUse = new ();
        private readonly Transform poolRoot;
        private readonly EmoteMaskCatalog emoteMaskCatalog;
        private readonly bool legacyAnimationsEnabled;
        private readonly bool forceBackfaceCullingEnabled;

        public EmotePlayer(AudioSource audioSourcePrefab, EmoteMaskCatalog emoteMaskCatalog, bool legacyAnimationsEnabled = false, bool forceBackfaceCullingEnabled = false)
        {
            this.forceBackfaceCullingEnabled = forceBackfaceCullingEnabled;
            this.emoteMaskCatalog = emoteMaskCatalog;
            this.legacyAnimationsEnabled = legacyAnimationsEnabled;
            poolRoot = GameObject.Find("ROOT_POOL_CONTAINER")!.transform;

            audioSourcePool = new GameObjectPool<AudioSource>(poolRoot, () => Object.Instantiate(audioSourcePrefab));

            releaseEmoteReferences = references =>
            {
                if (references.audioSource != null)
                    audioSourcePool.Release(references.audioSource);

                references.audioSource = null;
            };
        }

        public bool Play(GameObject mainAsset, AudioClip? audioAsset, bool isLooping, bool isSpatial, in IAvatarView view,
            ref CharacterEmoteComponent emoteComponent)
        {
            EmoteReferences? emoteInUse = emoteComponent.CurrentEmoteReference;

            if (IsSameLoopingEmote(emoteInUse, mainAsset, emoteComponent.EmoteLoop, isLooping))
                return true;

            EmoteReferences? emoteReferences = AcquireEmoteReferences(mainAsset, audioAsset, isLooping, isSpatial, in view, emoteInUse);
            if (emoteReferences == null) return false;

            if (emoteReferences.legacy)
            {
                if (!legacyAnimationsEnabled)
                {
                    Stop(emoteReferences);
                    return false;
                }

                PlayLegacyEmote(view, ref emoteComponent, emoteReferences, emoteComponent.EmoteLoop || isLooping);
            }
            else
                PlayMecanimEmote(view, ref emoteComponent, emoteReferences, isLooping);

            emotesInUse.Add(emoteReferences, pools[mainAsset]);
            emoteComponent.CurrentEmoteReference = emoteReferences;
            return true;
        }

        public bool PlayMasked(GameObject mainAsset, AudioClip? audioAsset, bool isLooping, bool isSpatial, in IAvatarView view,
            ref CharacterMaskedEmoteComponent maskedEmote)
        {
            EmoteReferences? emoteInUse = maskedEmote.CurrentEmoteReference;

            if (IsSameLoopingEmote(emoteInUse, mainAsset, maskedEmote.EmoteLoop, isLooping))
                return true;

            EmoteReferences? emoteReferences = AcquireEmoteReferences(mainAsset, audioAsset, isLooping, isSpatial, in view, emoteInUse);
            if (emoteReferences == null) return false;

            if (emoteReferences.legacy)
            {
                if (!PlayMaskedLegacyEmote(view, ref maskedEmote, emoteReferences, isLooping))
                {
                    Stop(emoteReferences);
                    return false;
                }
            }
            else
                PlayMaskedMecanimEmote(view, ref maskedEmote, emoteReferences, isLooping);

            emotesInUse.Add(emoteReferences, pools[mainAsset]);
            maskedEmote.CurrentEmoteReference = emoteReferences;
            return true;
        }

        private bool PlayMaskedLegacyEmote(in IAvatarView view, ref CharacterMaskedEmoteComponent maskedEmote, EmoteReferences emoteReferences, bool isLooping)
        {
            if (emoteReferences.avatarClip == null) return false;

            if (!emoteMaskCatalog.TryGet(maskedEmote.Mask, out AvatarMask? avatarMask))
            {
                ReportHub.LogError(ReportCategory.EMOTE,
                    $"{nameof(EmoteMaskCatalog)} has no entry for {maskedEmote.Mask}, masked legacy emote ignored.");
                return false;
            }

            view.StartMaskedLegacyEmote(emoteReferences.avatarClip, avatarMask!, isLooping);

            maskedEmote.EmoteLoop = isLooping;

            SetupPropAnimation(emoteReferences, isLooping);
            return true;
        }

        public void Stop(EmoteReferences emoteReference)
        {
            if (!emotesInUse.Remove(emoteReference, out GameObjectPool<EmoteReferences>? pool))
                return;

            pool!.Release(emoteReference);
        }

        public void StopMasked(EmoteReferences emoteReference, in IAvatarView avatarView, AvatarEmoteMask mask)
        {
            if (avatarView.IsMaskedLegacyEmotePlaying || avatarView.HasMaskedLegacyEmoteFinished)
            {
                avatarView.StopMaskedLegacyEmote();
                Stop(emoteReference);
                return;
            }

            avatarView.ResetAnimatorTrigger(AnimationHashes.MASKED_EMOTE);
            avatarView.ResetAnimatorTrigger(AnimationHashes.MASKED_EMOTE_REFRESH);
            avatarView.SetAnimatorTrigger(AnimationHashes.MASKED_EMOTE_STOP);

            string layer = AnimatorEmoteLayers.GetFromEmoteMask(mask);
            avatarView.SetLayerWeight(layer, 0);

            avatarView.ClearMaskedEmoteAnimationCache();

            Stop(emoteReference);
        }

        /// <summary>
        /// Checks if a masked emote should be cancelled based on its animator state.
        /// Used by CharacterEmoteSystem for remote player entities.
        /// </summary>
        /// <returns>True if the emote was cancelled, false otherwise.</returns>
        public bool TryCancelMaskedEmote(ref CharacterMaskedEmoteComponent masked, IAvatarView avatarView)
        {
            if (masked.CurrentEmoteReference == null) return false;

            bool shouldCancel = masked.StopEmote;

            if (!shouldCancel)
            {
                if (avatarView.IsMaskedLegacyEmotePlaying || avatarView.HasMaskedLegacyEmoteFinished)
                    shouldCancel = avatarView.HasMaskedLegacyEmoteFinished;
                else if (masked.IsPlaying)
                {
                    string layer = AnimatorEmoteLayers.GetFromEmoteMask(masked.Mask);
                    int currentTag = avatarView.GetAnimatorCurrentStateTag(layer);
                    shouldCancel = currentTag != AnimationHashes.MASKED_EMOTE && currentTag != AnimationHashes.MASKED_EMOTE_LOOP;
                }
            }

            if (!shouldCancel) return false;

            StopMasked(masked.CurrentEmoteReference, in avatarView, masked.Mask);
            masked.Reset();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateMaskedEmoteTag(ref CharacterMaskedEmoteComponent masked, IAvatarView avatarView)
        {
            if (masked.CurrentEmoteReference == null) return;

            // Legacy-blender path doesn't use Mecanim layer tags; cancellation is driven
            // by HasMaskedLegacyEmoteFinished on the avatar view, not by tag transitions.
            if (avatarView.IsMaskedLegacyEmotePlaying || avatarView.HasMaskedLegacyEmoteFinished) return;

            string layer = AnimatorEmoteLayers.GetFromEmoteMask(masked.Mask);
            int currentStateTag = avatarView.GetAnimatorCurrentStateTag(layer);
            masked.SetAnimationTag(currentStateTag);
        }

        private bool IsSameLoopingEmote(EmoteReferences? emoteInUse, GameObject mainAsset, bool currentlyLooping, bool isLooping) =>
            emoteInUse != null &&
            emotesInUse.ContainsKey(emoteInUse) &&
            pools.ContainsKey(mainAsset) &&
            emotesInUse[emoteInUse] == pools[mainAsset] &&
            currentlyLooping &&
            isLooping;

        private EmoteReferences? AcquireEmoteReferences(GameObject mainAsset,
            AudioClip? audioAsset,
            bool isLooping,
            bool isSpatial,
            in IAvatarView view,
            EmoteReferences? emoteInUse)
        {
            if (emoteInUse != null)
                Stop(emoteInUse);

            view.StopLegacyAnimation();

            if (!pools.ContainsKey(mainAsset))
            {
                if (IsValid(mainAsset))
                    pools.Add(mainAsset, new GameObjectPool<EmoteReferences>(poolRoot, () => CreateNewEmoteReference(mainAsset, forceBackfaceCullingEnabled), onRelease: releaseEmoteReferences));
                else
                    return null;
            }

            EmoteReferences? emoteReferences = pools[mainAsset]!.Get();
            if (!emoteReferences) return null;

            Transform avatarTransform = view.GetTransform();
            Transform emoteTransform = emoteReferences!.transform;
            emoteTransform.SetParent(avatarTransform, false);
            emoteTransform.localPosition = Vector3.zero;
            emoteTransform.localRotation = Quaternion.identity;

            emoteTransform.gameObject.layer = avatarTransform.gameObject.layer;

            using PoolExtensions.Scope<List<Transform>> children = avatarTransform.gameObject.GetComponentsInChildrenIntoPooledList<Transform>(true);

            foreach (Transform? child in children.Value)
                if (child != null)
                    child.gameObject.layer = avatarTransform.gameObject.layer;

            if (audioAsset != null)
            {
                AudioSource audioSource = audioSourcePool.Get();
                audioSource.clip = audioAsset;
                audioSource.spatialize = isSpatial;
                audioSource.spatialBlend = isSpatial ? 1 : 0;
                audioSource.transform.position = avatarTransform.position;
                audioSource.loop = isLooping;
                audioSource.Play();
                emoteReferences.audioSource = audioSource;
            }

            return emoteReferences;
        }

        private bool IsValid(GameObject mainAsset) =>
            mainAsset.GetComponentInChildren<Animator>(true)
            || (legacyAnimationsEnabled && mainAsset.GetComponentInChildren<Animation>(true));

        private static EmoteReferences CreateNewEmoteReference(GameObject mainAsset, bool forceBackfaceCullingEnabled)
        {
            GameObject mainGameObject = Object.Instantiate(mainAsset);

            Animator? animatorComp = mainGameObject.GetComponentInChildren<Animator>(true);
            Animation? animationComp = null;
            AnimationClip[] animationClips;

            if (animatorComp != null && animatorComp.runtimeAnimatorController != null)
                animationClips = animatorComp.runtimeAnimatorController.animationClips;
            else
            {
                // Legacy path: GLTFast attached an Animation component with legacy clips
                animatorComp = null;
                animationComp = mainGameObject.GetComponentInChildren<Animation>(true);

                List<AnimationClip> legacyClipList = ListPool<AnimationClip>.Get()!;

                if (animationComp != null)
                    foreach (AnimationState state in animationComp)
                        if (state.clip != null)
                            legacyClipList.Add(state.clip);

                animationClips = legacyClipList.ToArray();
                ListPool<AnimationClip>.Release(legacyClipList);
            }

            EmoteReferences references = mainGameObject.AddComponent<EmoteReferences>();
            IReadOnlyList<Renderer> renderers = mainGameObject.GetComponentsInChildren<Renderer>();
            List<AnimationClip> uniqueClips = ListPool<AnimationClip>.Get()!;

            ExtractClips(animationClips, uniqueClips, out AnimationClip? avatarClip, out AnimationClip? propClip, out int propClipHash, out bool legacy);

            if (uniqueClips.Count == 1)
            {
                foreach (Renderer renderer in renderers)
                {
                    renderer.enabled = false;
                    renderer.forceRenderingOff = true;
                }
            }
            else
            {
                foreach (Renderer renderer in renderers)
                {
                    // Some old emotes contain references to the avatar to ease animation production
                    // Since emotes 2.0 only the renderers representing the props should be visible
                    if (renderer.name.Contains("_reference", StringComparison.InvariantCultureIgnoreCase)
                        || renderer.name.EndsWith("_basemesh", StringComparison.InvariantCultureIgnoreCase)
                        || renderer.name.StartsWith("m_mask_", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Disable the renderer too for possible performance optimizations such as shadow casting or material changes
                        renderer.enabled = false;
                        renderer.forceRenderingOff = true;
                        continue;
                    }

                    if (forceBackfaceCullingEnabled)
                        renderer.ForceBackfaceCulling();
                }
            }

            references.Initialize(avatarClip, propClip, animatorComp, animationComp, propClipHash, legacy);

            ListPool<AnimationClip>.Release(uniqueClips);

            if (animatorComp != null)
                animatorComp.fireEvents = false;

            return references;
        }


        private void PlayLegacyEmote(IAvatarView avatarView, ref CharacterEmoteComponent emoteComponent, EmoteReferences emoteReferences, bool loop)
        {
            // Disable the Mecanim animator before the legacy Animation starts: on the very first legacy
            // emote of a fresh AvatarBase the Animation component is added live, and if the Animator is
            // still enabled during this same frame it drives the shared transforms and the legacy clip
            // has no visible effect until the next Play. (DisableAnimatorWhenPlayingLegacyAnimations
            // later in the frame is a defence-in-depth, not a substitute.)
            avatarView.AvatarAnimator.enabled = false;

            Animation animationComp = avatarView.AddOrGetLegacyAnimation();

            animationComp.playAutomatically = false;
            animationComp.Stop();

            if (emoteReferences.avatarClip != null)
            {
                emoteComponent.EmoteLoop = loop;
                string avatarClipName = emoteReferences.avatarClip.name;
                animationComp.AddClip(emoteReferences.avatarClip, avatarClipName);
                animationComp[avatarClipName].wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
                animationComp.Play(avatarClipName);
            }

            SetupPropAnimation(emoteReferences, loop);
        }

        private void PlayMecanimEmote(in IAvatarView view, ref CharacterEmoteComponent emoteComponent, EmoteReferences emoteReferences, bool isLooping)
        {
            if (emoteReferences.avatarClip != null)
            {
                view.ReplaceEmoteAnimation(emoteReferences.avatarClip);
                emoteComponent.EmoteLoop = isLooping;

                // See https://github.com/decentraland/unity-explorer/issues/4198
                view.ResetArmatureInclination();
            }

            view.ResetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
            view.ResetAnimatorTrigger(AnimationHashes.EMOTE);
            view.ResetAnimatorTrigger(AnimationHashes.EMOTE_RESET);

            view.SetAnimatorTrigger(view.IsAnimatorInTag(AnimationHashes.EMOTE) || view.IsAnimatorInTag(AnimationHashes.EMOTE_LOOP)
                ? AnimationHashes.EMOTE_RESET : AnimationHashes.EMOTE);
            view.SetAnimatorBool(AnimationHashes.EMOTE_LOOP, emoteComponent.EmoteLoop);

            SetupPropAnimation(emoteReferences, emoteComponent.EmoteLoop);
        }

        private void PlayMaskedMecanimEmote(in IAvatarView view, ref CharacterMaskedEmoteComponent maskedEmote, EmoteReferences emoteReferences, bool isLooping)
        {
            if (emoteReferences.avatarClip != null)
            {
                view.ReplaceMaskedEmoteAnimation(emoteReferences.avatarClip);
                maskedEmote.EmoteLoop = isLooping;
                view.ResetArmatureInclination();
            }

            view.ResetAnimatorTrigger(AnimationHashes.MASKED_EMOTE_STOP);
            view.ResetAnimatorTrigger(AnimationHashes.MASKED_EMOTE);
            view.ResetAnimatorTrigger(AnimationHashes.MASKED_EMOTE_REFRESH);

            string emoteLayer = AnimatorEmoteLayers.GetFromEmoteMask(maskedEmote.Mask);
            view.SetLayerWeight(emoteLayer, 1);

            int targetLayerTag = view.GetAnimatorCurrentStateTag(emoteLayer);
            bool alreadyInEmote = targetLayerTag == AnimationHashes.MASKED_EMOTE || targetLayerTag == AnimationHashes.MASKED_EMOTE_LOOP;
            view.SetAnimatorTrigger(alreadyInEmote ? AnimationHashes.MASKED_EMOTE_REFRESH : AnimationHashes.MASKED_EMOTE);
            view.SetAnimatorBool(AnimationHashes.MASKED_EMOTE_LOOP, maskedEmote.EmoteLoop);

            SetupPropAnimation(emoteReferences, maskedEmote.EmoteLoop);
        }

        private static void SetupPropAnimation(EmoteReferences emoteReferences, bool isLooping)
        {
            if (emoteReferences.propClip == null) return;

            if (emoteReferences.animatorComp != null)
            {
                int propTriggerHash = IsAnimatorImportedLocally(emoteReferences.animatorComp) ? AnimationHashes.PROP_ANIMATION_TRIGGER : emoteReferences.propClipHash;

                emoteReferences.animatorComp.SetTrigger(propTriggerHash);
                emoteReferences.animatorComp.SetBool(AnimationHashes.LOOP, isLooping);
            }
            else if (emoteReferences.animationComp != null)
            {
                // Legacy prop animation lives on the emote prefab's own Animation component
                Animation propAnimationComp = emoteReferences.animationComp;
                string propClipName = emoteReferences.propClip.name;
                propAnimationComp[propClipName].wrapMode = isLooping ? WrapMode.Loop : WrapMode.Once;
                propAnimationComp.Play(propClipName);
            }

            return;

            bool IsAnimatorImportedLocally(Animator animator)
            {
                var parameters = animator.parameters;

                for (int i = 0; i < parameters.Length; i++)
                    if (parameters[i].nameHash == AnimationHashes.PROP_ANIMATION_TRIGGER)
                        return true;

                return false;
            }
        }

        private static void ExtractClips(
            IReadOnlyList<AnimationClip> animationClips,
            List<AnimationClip> uniqueClips,
            out AnimationClip? avatarClip,
            out AnimationClip? propClip,
            out int propClipHash,
            out bool legacy)
        {
            avatarClip = null;
            propClip = null;
            propClipHash = 0;

            foreach (AnimationClip clip in animationClips)
                if (IsValidUniqueClip(clip))
                    uniqueClips.Add(clip);

            if (uniqueClips.Count == 1)
                avatarClip = uniqueClips[0];
            else if (uniqueClips.Count > 1)
            {
                foreach (AnimationClip animationClip in uniqueClips)
                {
                    if (animationClip.name.Contains("_avatar", StringComparison.OrdinalIgnoreCase))
                        avatarClip = animationClip;

                    if (animationClip.name.Contains("_prop", StringComparison.OrdinalIgnoreCase))
                    {
                        propClip = animationClip;
                        propClipHash = Animator.StringToHash(animationClip.name);
                    }
                }
            }

            legacy = avatarClip != null && avatarClip.legacy;
            return;

            bool IsValidUniqueClip(AnimationClip clip) =>
                clip != null
                && !uniqueClips.Contains(clip)
                && clip.name != AVATAR_ANIMATION_PLACEHOLDER_NAME
                && clip.name != PROP_ANIMATION_PLACEHOLDER_NAME;
        }
    }
}
