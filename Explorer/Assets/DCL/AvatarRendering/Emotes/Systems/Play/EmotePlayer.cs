using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Emotes.Play
{
    public class EmotePlayer
    {
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
            };
        }

        public bool Play(GameObject mainAsset, IReadOnlyList<AnimationClip>? allClips, AudioClip? audioAsset,
            bool isLooping, bool isSpatial,
            in IAvatarView view,
            ref CharacterEmoteComponent emoteComponent)
        {
            EmoteReferences? emoteInUse = emoteComponent.CurrentEmoteReference;

            if (emoteInUse != null)
                Stop(emoteInUse);

            if (!pools.ContainsKey(mainAsset))
            {
                if (IsValid(mainAsset))
                    pools.Add(mainAsset, new GameObjectPool<EmoteReferences>(poolRoot, () => CreateNewEmoteReference(mainAsset, allClips), onRelease: releaseEmoteReferences));
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

            if (emoteReferences.avatarClip != null)
            {
                view.ReplaceEmoteAnimation(emoteReferences.avatarClip);
                emoteComponent.EmoteLoop = isLooping;
            }

            view.SetAnimatorTrigger(view.IsAnimatorInTag(AnimationHashes.EMOTE) || view.IsAnimatorInTag(AnimationHashes.EMOTE_LOOP) ? AnimationHashes.EMOTE_RESET : AnimationHashes.EMOTE);
            view.SetAnimatorBool(AnimationHashes.EMOTE_LOOP, emoteComponent.EmoteLoop);

            if (emoteReferences.propClip != null)
            {
                emoteReferences.animator.SetTrigger(emoteReferences.propClipHash);
                emoteReferences.animator.SetBool(AnimationHashes.LOOP, emoteComponent.EmoteLoop);
            }

            if (audioAsset != null)
            {
                AudioSource audioSource = audioSourcePool.Get()!;
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

        private static bool IsValid(GameObject mainAsset) =>
            mainAsset.GetComponent<Animator>();

        private static EmoteReferences CreateNewEmoteReference(GameObject mainAsset, IReadOnlyList<AnimationClip>? allClips)
        {
            GameObject mainGameObject = Object.Instantiate(mainAsset)!;
            Animator animator = mainGameObject.GetComponent<Animator>().EnsureNotNull();
            EmoteReferences references = mainGameObject.AddComponent<EmoteReferences>()!;
            IReadOnlyList<Renderer> renderers = mainGameObject.GetComponentsInChildren<Renderer>()!;
            List<AnimationClip> uniqueClips = ListPool<AnimationClip>.Get()!;

            ExtractClips(allClips ?? animator.runtimeAnimatorController.animationClips, uniqueClips,
                out AnimationClip? avatarClip, out AnimationClip? propClip, out int propClipHash);

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

            references.Initialize(avatarClip, propClip, animator, propClipHash);

            ListPool<AnimationClip>.Release(uniqueClips);

            // some of our legacy emotes have unity events that we are not handling, so we disable that system to avoid further errors
            animator.fireEvents = false;

            return references;
        }

        public void Stop(EmoteReferences emoteReference)
        {
            if (!emotesInUse.Remove(emoteReference, out GameObjectPool<EmoteReferences>? pool))
                return;

            pool!.Release(emoteReference);
        }

        private static void ExtractClips(IEnumerable<AnimationClip> allClips,
            List<AnimationClip> uniqueClips,
            out AnimationClip? avatarClip,
            out AnimationClip? propClip,
            out int propClipHash)
        {
            avatarClip = null;
            propClip = null;
            propClipHash = 0;

            foreach (AnimationClip clip in allClips)
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
