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
        private static readonly List<AnimationClip> ANIMATION_CLIPS = new List<AnimationClip>(4);

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

            // Early return if the same looping emote is already playing
            if (emoteInUse != null &&
                emotesInUse.ContainsKey(emoteInUse) &&
                pools.ContainsKey(mainAsset) &&
                emotesInUse[emoteInUse] == pools[mainAsset] &&
                emoteComponent.EmoteLoop &&
                isLooping)
                return true;

            if (emoteInUse != null)
                Stop(emoteInUse);

            if (!pools.ContainsKey(mainAsset))
            {
                EmoteDTO.EmoteMetadataDto emoteMetadata = emoteComponent.Metadata;

                if (IsValid(mainAsset))
                    pools.Add(mainAsset, new GameObjectPool<EmoteReferences>(poolRoot, () => CreateNewEmoteReference(mainAsset, emoteMetadata), onRelease: releaseEmoteReferences));
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

        private static EmoteReferences CreateNewEmoteReference(GameObject mainAsset, EmoteDTO.EmoteMetadataDto emoteMetadata)
        {
            GameObject mainGameObject = Object.Instantiate(mainAsset);

            Animator? animatorComp = null;
            Animation? animationComp = null;

            ANIMATION_CLIPS.Clear();

            // Check for Animator first (Mecanim emotes)
            animatorComp = mainGameObject.GetComponent<Animator>();
            if (animatorComp)
            {
                ANIMATION_CLIPS.AddRange(animatorComp.runtimeAnimatorController.animationClips);
            }
            else
            {
                // Legacy emotes - there's always only one Animation component
                animationComp = mainGameObject.GetComponentInChildren<Animation>(true);

                int clipCount = 0;
                foreach (AnimationState state in animationComp)
                    ANIMATION_CLIPS.Add(state.clip);
            }

            EmoteReferences references = mainGameObject.AddComponent<EmoteReferences>();
            IReadOnlyList<Renderer> renderers = mainGameObject.GetComponentsInChildren<Renderer>();
            List<AnimationClip> uniqueClips = ListPool<AnimationClip>.Get()!;
            EmoteReferences.EmoteOutcome[]? outcomes;

            ExtractClips(ANIMATION_CLIPS, uniqueClips, emoteMetadata, out AnimationClip? avatarClip, out AnimationClip? propClip, out outcomes, out int propClipHash);

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

            references.Initialize(avatarClip, propClip, outcomes, animatorComp, animationComp, propClipHash);

            ListPool<AnimationClip>.Release(uniqueClips);

            // some of our legacy emotes have unity events that we are not handling, so we disable that system to avoid further errors
            if (animatorComp != null)
                animatorComp.fireEvents = false;

            return references;
        }

        private void PlayLegacyEmote(GameObject avatarAnimatorGameObject, ref CharacterEmoteComponent emoteComponent, EmoteReferences emoteReferences, bool loop)
        {
// TODO: Adapt this to social emtes like PlayMecanimEmote
            Animation animationComp;
            if (!(animationComp = avatarAnimatorGameObject.GetComponent<Animation>()))
                animationComp = avatarAnimatorGameObject.AddComponent<Animation>();
            animationComp.playAutomatically = false;
            animationComp.Stop();

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

        private void PlayMecanimEmote(in IAvatarView view, ref CharacterEmoteComponent emoteComponent, EmoteReferences emoteReferences, bool isLooping)
        {
            AnimationClip? avatarClip;

            if (emoteComponent.Metadata.IsSocialEmote)
            {
                if (emoteComponent.IsPlayingSocialEmoteOutcome)
                {
                    if (emoteComponent.IsReactingToSocialEmote)
                    {
                        avatarClip = emoteReferences.socialEmoteOutcomes![emoteComponent.CurrentSocialEmoteOutcome].OtherAvatarAnimation;
                        isLooping = emoteComponent.Metadata.emoteDataADR74.outcomes![emoteComponent.CurrentSocialEmoteOutcome].clips!.Armature_Other!.loop;
                    }
                    else
                    {
                        avatarClip = emoteReferences.socialEmoteOutcomes![emoteComponent.CurrentSocialEmoteOutcome].LocalAvatarAnimation;
                        isLooping = emoteComponent.Metadata.emoteDataADR74.outcomes![emoteComponent.CurrentSocialEmoteOutcome].clips!.Armature!.loop;
                    }
                }
                else
                {
                    avatarClip = emoteReferences.avatarClip;
                    isLooping = emoteComponent.Metadata.emoteDataADR74.startAnimation!.Armature!.loop;
                }
            }
            else
            {
                avatarClip = emoteReferences.avatarClip;
            }

            if (avatarClip != null)
            {
                view.ReplaceEmoteAnimation(avatarClip);
                emoteComponent.EmoteLoop = isLooping;
            }

            // Create a clean slate for the animator before setting the play trigger
            view.ResetTrigger(AnimationHashes.EMOTE_STOP);
            view.ResetTrigger(AnimationHashes.EMOTE);
            view.ResetTrigger(AnimationHashes.EMOTE_RESET);

            view.SetAnimatorTrigger(view.IsAnimatorInTag(AnimationHashes.EMOTE) || view.IsAnimatorInTag(AnimationHashes.EMOTE_LOOP) ? AnimationHashes.EMOTE_RESET : AnimationHashes.EMOTE);
            view.SetAnimatorBool(AnimationHashes.EMOTE_LOOP, emoteComponent.EmoteLoop);

            AnimationClip? propClip = null;
            bool isPropLooping = false;
            int propClipHash = 0;

            if (emoteComponent.Metadata.IsSocialEmote)
            {
                if (emoteComponent.IsPlayingSocialEmoteOutcome)
                {
                    propClip = emoteReferences.socialEmoteOutcomes![emoteComponent.CurrentSocialEmoteOutcome].PropAnimation;

                    if (propClip != null)
                    {
                        isPropLooping = emoteComponent.Metadata.emoteDataADR74.outcomes![emoteComponent.CurrentSocialEmoteOutcome].clips!.Armature_Prop!.loop;
                        propClipHash = emoteReferences.socialEmoteOutcomes[emoteComponent.CurrentSocialEmoteOutcome].PropAnimationHash;
                    }
                }
                else
                {
                    propClip = emoteReferences.propClip;

                    if (propClip != null)
                    {
                        isPropLooping = emoteComponent.Metadata.emoteDataADR74.startAnimation!.Armature_Prop!.loop;
                        propClipHash = emoteReferences.propClipHash;
                    }
                }
            }
            else
            {
                propClip = emoteReferences.propClip;
                isPropLooping = emoteComponent.EmoteLoop;
                propClipHash = emoteReferences.propClipHash;
            }

            if (propClip != null && emoteReferences.animatorComp != null)
            {
                emoteReferences.animatorComp.SetTrigger(propClipHash);
                emoteReferences.animatorComp.SetBool(AnimationHashes.LOOP, isPropLooping);
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
            EmoteDTO.EmoteMetadataDto emoteMetadata,
            out AnimationClip? avatarClip,
            out AnimationClip? propClip,
            out EmoteReferences.EmoteOutcome[]? outcomeClips,
            out int propClipHash)
        {
            avatarClip = null;
            propClip = null;
            propClipHash = 0;

            foreach (AnimationClip clip in animationClips)
                if (clip != null && !uniqueClips.Contains(clip))
                    uniqueClips.Add(clip);

            outcomeClips = null;

            if (emoteMetadata.IsSocialEmote)
            {
                outcomeClips = new EmoteReferences.EmoteOutcome[emoteMetadata.data.outcomes.Length]; // TODO: Make static List

                foreach (AnimationClip animationClip in uniqueClips)
                {
                    if (emoteMetadata.emoteDataADR74.startAnimation != null &&
                        emoteMetadata.emoteDataADR74.startAnimation.Armature != null &&
                        animationClip.name == emoteMetadata.emoteDataADR74.startAnimation.Armature.animation)
                    {
                        avatarClip = animationClip;
                    }
                    else if (emoteMetadata.emoteDataADR74.startAnimation != null &&
                             emoteMetadata.emoteDataADR74.startAnimation.Armature_Prop != null &&
                             animationClip.name == emoteMetadata.emoteDataADR74.startAnimation.Armature_Prop.animation)
                    {
                        propClip = animationClip;
                        propClipHash = Animator.StringToHash(animationClip.name);
                    }
                    else // outcomes
                    {
                        for (int i = 0; i < emoteMetadata.data.outcomes.Length; ++i)
                        {
                            if (emoteMetadata.data.outcomes[i].clips.Armature_Other != null &&
                                animationClip.name == emoteMetadata.data.outcomes[i].clips.Armature_Other.animation)
                            {
                                outcomeClips[i].OtherAvatarAnimation = animationClip;
                                // In order to apply the animation to the "reacting" avatar, the name of the armature object in the clip must match the name in the avatar model
                               /* ReplaceBoundObjectNameInAnimationClip(animationClip, "Armature_Other", "Armature");*/
                            }
                            else if (emoteMetadata.data.outcomes[i].clips.Armature != null &&
                                     animationClip.name == emoteMetadata.data.outcomes[i].clips.Armature.animation)
                            {
                                outcomeClips[i].LocalAvatarAnimation = animationClip;
                            }
                            else if (emoteMetadata.data.outcomes[i].clips.Armature_Prop != null &&
                                     animationClip.name == emoteMetadata.data.outcomes[i].clips.Armature_Prop.animation)
                            {
                                outcomeClips[i].PropAnimation = animationClip;
                                outcomeClips[i].PropAnimationHash = Animator.StringToHash(animationClip.name);
                            }
                        }
                    }
                }
            }
            else // Non-social emotes
            {
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

        /*private static void ReplaceBoundObjectNameInAnimationClip(AnimationClip animationClip, string patternToSearch, string replacement)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(animationClip);

            for (int i = 0; i < bindings.Length; ++i)
                if (bindings[i].propertyName.Contains(patternToSearch, StringComparison.InvariantCultureIgnoreCase))
                    bindings[i].propertyName = replacement;
        }*/
    }
}
