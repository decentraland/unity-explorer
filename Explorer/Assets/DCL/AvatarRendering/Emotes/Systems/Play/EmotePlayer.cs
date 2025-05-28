using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility.Animations;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Emotes.Play
{
    public class EmotePlayer
    {
        // Emotes can have up to 2 clips (avatar + prop)
        private static readonly AnimationClip[] LEGACY_ANIMATION_CLIPS = new AnimationClip[2];

        private readonly GameObjectPool<AudioSource> audioSourcePool;
        private readonly Action<EmoteReferences> releaseEmoteReferences;
        private readonly Dictionary<GameObject, GameObjectPool<EmoteReferences>> pools = new ();
        private readonly Dictionary<EmoteReferences, GameObjectPool<EmoteReferences>> emotesInUse = new ();
        private readonly Transform poolRoot;
        private readonly bool legacyAnimationsEnabled;

        public EmotePlayer(AudioSource audioSourcePrefab, bool legacyAnimationsEnabled)
        {
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

            if (emoteInUse != null)
                Stop(emoteInUse);

            if (!pools.ContainsKey(mainAsset))
            {
                if (IsValid(mainAsset))
                    pools.Add(mainAsset, new GameObjectPool<EmoteReferences>(poolRoot, () => CreateNewEmoteReference(mainAsset), onRelease: releaseEmoteReferences));
                else
                    return false;
            }

            EmoteReferences? emoteReferences = pools[mainAsset]!.Get();
            if (!emoteReferences) return false;

            Transform avatarTransform = view.GetTransform();
            Transform emoteTransform = emoteReferences!.transform;
            emoteTransform.SetParent(avatarTransform, false);
            emoteTransform.localPosition = Vector3.zero;
            emoteTransform.localRotation = Quaternion.identity;

            // Set the layer of the objects & children everytime since the emote can be created and stored in the pool elsewhere, like the avatar preview
            // In that case, the hierarchy will keep the wrong layer in the future usages
            emoteTransform.gameObject.layer = avatarTransform.gameObject.layer;

            using PoolExtensions.Scope<List<Transform>> children = avatarTransform.gameObject.GetComponentsInChildrenIntoPooledList<Transform>(true);

            foreach (Transform? child in children.Value)
                if (child != null)
                    child.gameObject.layer = avatarTransform.gameObject.layer;

            // Scene Emotes in Local Scene Development are loaded as legacy animations
            // (there's no other way to load them in runtime from a GLB)
            if (emoteReferences.avatarClip is { legacy: true })
            {
                // For consistency with processed scene assets in the AB converter (and performance), we only
                // play legacy animations in Local Scene Dev mode (and only if they follow the naming requirements
                // but that is checked higher up in the execution flow)
                if (!legacyAnimationsEnabled)
                    return false;

                // Animator gets re-enabled later when its properties get manipulated in AvatarBase
                view.AvatarAnimator.enabled = false;

                PlayLegacyEmote(view.AvatarAnimator.gameObject, ref emoteComponent, emoteReferences, emoteComponent.EmoteLoop || isLooping);
            }
            else
            {
                PlayMecanimEmote(view, ref emoteComponent, emoteReferences, isLooping);
            }

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

            emotesInUse.Add(emoteReferences, pools[mainAsset]);
            emoteComponent.CurrentEmoteReference = emoteReferences;
            return true;
        }

        private bool IsValid(GameObject mainAsset) => mainAsset.GetComponent<Animator>()
            || (legacyAnimationsEnabled && mainAsset.GetComponentInChildren<Animation>(true));

        private static EmoteReferences CreateNewEmoteReference(GameObject mainAsset)
        {
            GameObject mainGameObject = Object.Instantiate(mainAsset);

            AnimationClip[] animationClips;
            Animator? animatorComp = null;
            Animation? animationComp = null;

            // Check for Animator first (Mecanim emotes)
            animatorComp = mainGameObject.GetComponent<Animator>();
            if (animatorComp)
            {
                animationClips = animatorComp.runtimeAnimatorController.animationClips;
            }
            else
            {
                // Legacy emotes - there's always only one Animation component
                animationComp = mainGameObject.GetComponentInChildren<Animation>(true);

                // Clear the pre-allocated array
                Array.Clear(LEGACY_ANIMATION_CLIPS, 0, LEGACY_ANIMATION_CLIPS.Length);

                int clipCount = 0;
                foreach (AnimationState state in animationComp)
                {
                    if (state.clip != null && clipCount < LEGACY_ANIMATION_CLIPS.Length)
                        LEGACY_ANIMATION_CLIPS[clipCount++] = state.clip;
                }
                animationClips = LEGACY_ANIMATION_CLIPS;
            }

            EmoteReferences references = mainGameObject.AddComponent<EmoteReferences>();
            IReadOnlyList<Renderer> renderers = mainGameObject.GetComponentsInChildren<Renderer>();
            List<AnimationClip> uniqueClips = ListPool<AnimationClip>.Get()!;

            ExtractClips(animationClips, uniqueClips, out AnimationClip? avatarClip, out AnimationClip? propClip, out int propClipHash);

            if (uniqueClips.Count == 1)
            {
                // We assume that only one animation means that there are no props in the emote, as stated in the docs:
                // "The emote must have one animation for the avatar and one animation for the prop. Currently multiple animations are not allowed."
                // We could also check if (propClip != null), but currently we have problems with many emotes that are not following naming conventions
                foreach (Renderer renderer in renderers)
                {
                    // Disable the renderer too for possible performance optimizations such as shadow casting or material changes
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

            references.Initialize(avatarClip, propClip, animatorComp, animationComp, propClipHash);

            ListPool<AnimationClip>.Release(uniqueClips);

            // some of our legacy emotes have unity events that we are not handling, so we disable that system to avoid further errors
            if (animatorComp != null)
                animatorComp.fireEvents = false;

            return references;
        }

        private void PlayLegacyEmote(GameObject avatarAnimatorGameObject, ref CharacterEmoteComponent emoteComponent, EmoteReferences emoteReferences, bool loop)
        {
            Animation animationComp;
            if (!(animationComp = avatarAnimatorGameObject.GetComponent<Animation>()))
                animationComp = avatarAnimatorGameObject.AddComponent<Animation>();
            ClearLegacyAnimationClips(animationComp);
            animationComp.playAutomatically = false;

            if (emoteReferences.avatarClip != null)
            {
                emoteComponent.EmoteLoop = loop;

                var avatarClipName = emoteReferences.avatarClip.name;
                animationComp.AddClip(emoteReferences.avatarClip, avatarClipName);
                animationComp[avatarClipName].wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
                animationComp.Play(avatarClipName);
            }

            if (emoteReferences.propClip != null && emoteReferences.animationComp != null)
            {
                var propAnimationComp = emoteReferences.animationComp;
                var propClipName = emoteReferences.propClip.name;
                propAnimationComp[propClipName].wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
                propAnimationComp.Play(propClipName);
            }
        }

        private void ClearLegacyAnimationClips(Animation animationComp)
        {
            HashSet<string> animationsToRemove = new HashSet<string>();
            foreach (AnimationState state in animationComp)
            {
                animationsToRemove.Add(state.clip.name);
            }
            foreach (string clipName in animationsToRemove)
            {
                animationComp.RemoveClip(clipName);
            }
        }

        private void PlayMecanimEmote(in IAvatarView view, ref CharacterEmoteComponent emoteComponent, EmoteReferences emoteReferences, bool isLooping)
        {
            if (emoteReferences.avatarClip != null)
            {
                view.ReplaceEmoteAnimation(emoteReferences.avatarClip);
                emoteComponent.EmoteLoop = isLooping;
            }

            view.SetAnimatorTrigger(view.IsAnimatorInTag(AnimationHashes.EMOTE) || view.IsAnimatorInTag(AnimationHashes.EMOTE_LOOP) ? AnimationHashes.EMOTE_RESET : AnimationHashes.EMOTE);
            view.SetAnimatorBool(AnimationHashes.EMOTE_LOOP, emoteComponent.EmoteLoop);

            if (emoteReferences.propClip != null && emoteReferences.animatorComp != null)
            {
                emoteReferences.animatorComp.SetTrigger(emoteReferences.propClipHash);
                emoteReferences.animatorComp.SetBool(AnimationHashes.LOOP, emoteComponent.EmoteLoop);
            }
        }

        public void Stop(EmoteReferences emoteReference)
        {
            if (!emotesInUse.Remove(emoteReference, out GameObjectPool<EmoteReferences>? pool))
                return;

            pool!.Release(emoteReference);
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
                if (!uniqueClips.Contains(clip))
                    uniqueClips.Add(clip);

            if (uniqueClips.Count == 1)
                avatarClip = uniqueClips[0];
            else if (uniqueClips.Count > 1)
            {
                foreach (AnimationClip animationClip in uniqueClips)
                {
                    // Many 2.0 emotes are not following naming conventions: https://docs.decentraland.org/creator/emotes/props-and-sounds/#naming-conventions
                    // Some examples:
                    // urn:decentraland:matic:collections-v2:0xca53b9436be1d663e050eb9ce523decbc656365c:1
                    // urn:decentraland:matic:collections-v2:0xfcc2c46c83a9faa5c639e81d0ad19e27b5517e57:0
                    // So they won't work because of the naming checks
                    // Creators need to either fix the emotes, or we need to apply a fallback based on sorting rule
                    if (animationClip.name.Contains("_avatar", StringComparison.OrdinalIgnoreCase))
                        avatarClip = animationClip;

                    if (animationClip.name.Contains("_prop", StringComparison.OrdinalIgnoreCase))
                    {
                        propClip = animationClip;
                        propClipHash = Animator.StringToHash(animationClip.name);
                    }
                }
            }
        }
    }
}
