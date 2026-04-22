using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.StreamableLoading.GLTF;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Pool;
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

        public EmotePlayer(AudioSource audioSourcePrefab)
        {
            poolRoot = GameObject.Find("ROOT_POOL_CONTAINER")!.transform;

            audioSourcePool = new GameObjectPool<AudioSource>(poolRoot, () => Object.Instantiate(audioSourcePrefab));

            releaseEmoteReferences = references =>
            {
                if (references.audioSource != null)
                    audioSourcePool.Release(references.audioSource);

                references.audioSource = null;

                if (references.playableGraph.IsValid())
                {
                    Debug.Log("(Maurizio) EmotePlayer: destroying PlayableGraph on pool release");
                    references.playableGraph.Destroy();
                    references.playableGraph = default;
                    references.playableController = default;
                    references.playableClip = default;
                    references.playableSourceAnimator = null;
                    references.playableLoop = false;
                    references.playableClipLength = 0f;
                }
            };
        }

        public bool Play(GameObject mainAsset, AudioClip? audioAsset, bool isLooping, bool isSpatial, in IAvatarView view,
            ref CharacterEmoteComponent emoteComponent)
        {
            if (mainAsset.GetComponent<LegacyImportedAnimationsMarker>() != null)
            {
                Debug.Log($"(Maurizio) EmotePlayer.Play: legacy-imported scene emote '{mainAsset.name}' reached the non-masked path — ignoring. Scene emotes are expected to flow through PlayMasked.");
                return false;
            }

            EmoteReferences? emoteInUse = emoteComponent.CurrentEmoteReference;

            if (IsSameLoopingEmote(emoteInUse, mainAsset, emoteComponent.EmoteLoop, isLooping))
                return true;

            EmoteReferences? emoteReferences = AcquireEmoteReferences(mainAsset, audioAsset, isSpatial, in view, emoteInUse);
            if (emoteReferences == null) return false;

            PlayMecanimEmote(view, ref emoteComponent, emoteReferences, isLooping);

            emotesInUse.Add(emoteReferences, pools[mainAsset]);
            emoteComponent.CurrentEmoteReference = emoteReferences;
            return true;
        }

        public bool PlayMasked(GameObject mainAsset, AudioClip? audioAsset, bool isLooping, bool isSpatial, in IAvatarView view,
            ref CharacterMaskedEmoteComponent maskedEmote)
        {
            if (mainAsset.TryGetComponent<LegacyImportedAnimationsMarker>(out var marker))
                return PlayMaskedPlayableEmote(mainAsset, marker, audioAsset, isLooping, isSpatial, in view, ref maskedEmote);

            EmoteReferences? emoteInUse = maskedEmote.CurrentEmoteReference;

            if (IsSameLoopingEmote(emoteInUse, mainAsset, maskedEmote.EmoteLoop, isLooping))
                return true;

            EmoteReferences? emoteReferences = AcquireEmoteReferences(mainAsset, audioAsset, isSpatial, in view, emoteInUse);
            if (emoteReferences == null) return false;

            PlayMaskedMecanimEmote(view, ref maskedEmote, emoteReferences, isLooping);

            emotesInUse.Add(emoteReferences, pools[mainAsset]);
            maskedEmote.CurrentEmoteReference = emoteReferences;
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

            if (!shouldCancel && masked.IsPlaying)
            {
                string layer = AnimatorEmoteLayers.GetFromEmoteMask(masked.Mask);
                int currentTag = avatarView.GetAnimatorCurrentStateTag(layer);
                shouldCancel = currentTag != AnimationHashes.MASKED_EMOTE && currentTag != AnimationHashes.MASKED_EMOTE_LOOP;
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
            bool isSpatial,
            in IAvatarView view,
            EmoteReferences? emoteInUse)
        {
            if (emoteInUse != null)
                Stop(emoteInUse);

            if (!pools.ContainsKey(mainAsset))
            {
                bool hasAnimator = mainAsset.GetComponentInChildren<Animator>(true);
                bool hasLegacyMarker = mainAsset.GetComponent<LegacyImportedAnimationsMarker>() != null;

                if (hasAnimator || hasLegacyMarker)
                    pools.Add(mainAsset, new GameObjectPool<EmoteReferences>(poolRoot, () => CreateNewEmoteReference(mainAsset), onRelease: releaseEmoteReferences));
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
                audioSource.loop = true;
                audioSource.Play();
                emoteReferences.audioSource = audioSource;
            }

            return emoteReferences;
        }

        private static EmoteReferences CreateNewEmoteReference(GameObject mainAsset)
        {
            GameObject mainGameObject = Object.Instantiate(mainAsset);

            if (mainGameObject.TryGetComponent<LegacyImportedAnimationsMarker>(out var legacyMarker))
                return CreateLegacyPlayableEmoteReference(mainGameObject, legacyMarker);

            Animator animatorComp = mainGameObject.GetComponentInChildren<Animator>(true);
            AnimationClip[] animationClips = animatorComp.runtimeAnimatorController.animationClips;

            EmoteReferences references = mainGameObject.AddComponent<EmoteReferences>();
            IReadOnlyList<Renderer> renderers = mainGameObject.GetComponentsInChildren<Renderer>();
            List<AnimationClip> uniqueClips = ListPool<AnimationClip>.Get()!;

            ExtractClips(animationClips, uniqueClips, out AnimationClip? avatarClip, out AnimationClip? propClip, out int propClipHash);

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
                    }
                }
            }

            references.Initialize(avatarClip, propClip, animatorComp, propClipHash);

            ListPool<AnimationClip>.Release(uniqueClips);

            animatorComp.fireEvents = false;

            return references;
        }

        /// <summary>
        /// Instantiated emote GameObject for the scene-emote Playable fork. The avatar animation is
        /// driven via a PlayableGraph on the avatar's own Animator (built in PlayMaskedPlayableEmote),
        /// so the emote GameObject itself has no meaningful local animator or renderers to display.
        /// </summary>
        private static EmoteReferences CreateLegacyPlayableEmoteReference(GameObject mainGameObject, LegacyImportedAnimationsMarker marker)
        {
            EmoteReferences references = mainGameObject.AddComponent<EmoteReferences>();

            // Nothing to render from the emote GO itself — the avatar skeleton is driven remotely.
            foreach (Renderer renderer in mainGameObject.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
                renderer.forceRenderingOff = true;
            }

            references.Initialize(marker.AvatarClip, null, null, 0);
            return references;
        }

        private bool PlayMaskedPlayableEmote(GameObject mainAsset, LegacyImportedAnimationsMarker marker,
            AudioClip? audioAsset, bool isLooping, bool isSpatial,
            in IAvatarView view, ref CharacterMaskedEmoteComponent maskedEmote)
        {
            Debug.Log($"(Maurizio) PlayMaskedPlayableEmote: mainAsset='{mainAsset.name}' mask={maskedEmote.Mask} looping={isLooping} hasAvatarClip={(marker.AvatarClip != null)} hasPropClip={(marker.PropClip != null)}");

            if (marker.AvatarClip == null)
            {
                Debug.Log("(Maurizio) PlayMaskedPlayableEmote: no avatar clip on marker, aborting");
                return false;
            }

            if (marker.PropClip != null)
                Debug.Log("(Maurizio) PlayMaskedPlayableEmote: prop clip present but not supported in the Playable fork (v1)");

            EmoteReferences? prior = maskedEmote.CurrentEmoteReference;
            if (IsSameLoopingEmote(prior, mainAsset, maskedEmote.EmoteLoop, isLooping))
                return true;

            EmoteReferences? refs = AcquireEmoteReferences(mainAsset, audioAsset, isSpatial, in view, prior);
            if (refs == null)
            {
                Debug.Log("(Maurizio) PlayMaskedPlayableEmote: AcquireEmoteReferences returned null");
                return false;
            }

            Animator avatarAnimator = view.AvatarAnimator;
            if (avatarAnimator == null || avatarAnimator.runtimeAnimatorController == null)
            {
                Debug.Log("(Maurizio) PlayMaskedPlayableEmote: avatar Animator or its runtimeAnimatorController is null");
                return false;
            }

            var graph = PlayableGraph.Create($"SceneEmote_{mainAsset.name}");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            var output = AnimationPlayableOutput.Create(graph, "Animation", avatarAnimator);

            var controllerPlayable = AnimatorControllerPlayable.Create(graph, avatarAnimator.runtimeAnimatorController);
            var clipPlayable = AnimationClipPlayable.Create(graph, marker.AvatarClip);
            clipPlayable.SetApplyFootIK(false);
            // Looping is handled in EmoteReferences.LateUpdate via manual time wrap — Duration here is
            // only meaningful for the non-looping case so Unity knows when the Playable is "done".
            clipPlayable.SetDuration(isLooping ? double.PositiveInfinity : marker.AvatarClip.length);

            var mixer = AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(controllerPlayable, 0, mixer, 0);
            graph.Connect(clipPlayable,       0, mixer, 1);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 1f);

            AvatarMask? layerMask = GetMaskSet()?.Get(maskedEmote.Mask);
            if (layerMask != null)
                mixer.SetLayerMaskFromAvatarMask(1, layerMask);
            else
                Debug.Log($"(Maurizio) PlayMaskedPlayableEmote: no AvatarMask for {maskedEmote.Mask} in SceneEmoteMaskSet, emote will override full skeleton");

            output.SetSourcePlayable(mixer);
            graph.Play();

            refs.playableGraph = graph;
            refs.playableController = controllerPlayable;
            refs.playableClip = clipPlayable;
            refs.playableSourceAnimator = avatarAnimator;
            refs.playableLoop = isLooping;
            refs.playableClipLength = marker.AvatarClip.length;

            emotesInUse.Add(refs, pools[mainAsset]);
            maskedEmote.CurrentEmoteReference = refs;
            maskedEmote.EmoteLoop = isLooping;
            return true;
        }

        private static SceneEmoteMaskSet? cachedMaskSet;
        private static bool maskSetLoadAttempted;

        private static SceneEmoteMaskSet? GetMaskSet()
        {
            if (maskSetLoadAttempted) return cachedMaskSet;
            maskSetLoadAttempted = true;
            cachedMaskSet = Resources.Load<SceneEmoteMaskSet>("SceneEmoteMaskSet");
            if (cachedMaskSet == null)
                Debug.Log("(Maurizio) GetMaskSet: Resources.Load<SceneEmoteMaskSet>(\"SceneEmoteMaskSet\") returned null — drop the asset into a Resources folder named \"SceneEmoteMaskSet.asset\"");
            return cachedMaskSet;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetupPropAnimation(EmoteReferences emoteReferences, bool isLooping)
        {
            if (emoteReferences.propClip != null && emoteReferences.animatorComp != null)
            {
                int propTriggerHash = IsAnimatorImportedLocally(emoteReferences.animatorComp) ? AnimationHashes.PROP_ANIMATION_TRIGGER : emoteReferences.propClipHash;

                emoteReferences.animatorComp.SetTrigger(propTriggerHash);
                emoteReferences.animatorComp.SetBool(AnimationHashes.LOOP, isLooping);
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
            out int propClipHash)
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

            return;

            bool IsValidUniqueClip(AnimationClip clip) =>
                clip != null
                && !uniqueClips.Contains(clip)
                && clip.name != AVATAR_ANIMATION_PLACEHOLDER_NAME
                && clip.name != PROP_ANIMATION_PLACEHOLDER_NAME;
        }
    }
}
