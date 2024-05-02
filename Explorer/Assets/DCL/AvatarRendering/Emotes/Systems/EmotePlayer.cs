using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Emotes
{
    public class EmotePlayer
    {
        private readonly GameObjectPool<AudioSource> audioSourcePool;
        private readonly Dictionary<GameObject, GameObjectPool<EmoteReferences>> pools = new ();
        private readonly Dictionary<EmoteReferences, GameObjectPool<EmoteReferences>> emotesInUse = new ();
        private readonly Transform poolRoot;

        public EmotePlayer(AudioSource audioSourcePrefab)
        {
            poolRoot = GameObject.Find("ROOT_POOL_CONTAINER").transform;

            audioSourcePool = new GameObjectPool<AudioSource>(poolRoot, () => Object.Instantiate(audioSourcePrefab));
        }

        public bool Play(GameObject mainAsset, AudioClip? audioAsset, bool isLooping, in IAvatarView view, ref CharacterEmoteComponent emoteComponent)
        {
            Animator animator = mainAsset.GetComponent<Animator>();

            if (animator == null)
                return false;

            EmoteReferences? emoteInUse = emoteComponent.CurrentEmoteReference;

            if (emoteInUse != null)
                Stop(emoteInUse);

            if (!pools.ContainsKey(mainAsset))
                pools.Add(mainAsset, new GameObjectPool<EmoteReferences>(poolRoot, () => CreateNewEmoteReference(mainAsset, isLooping)));

            EmoteReferences? emoteReferences = pools[mainAsset].Get();
            if (!emoteReferences) return false;

            Transform avatarTransform = view.GetTransform();
            Transform emoteTransform = emoteReferences.transform;
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
                AudioSource? audioSource = audioSourcePool.Get();
                audioSource.clip = audioAsset;
                audioSource.Play();
                emoteReferences.audioSource = audioSource;
            }

            emotesInUse.Add(emoteReferences, pools[mainAsset]);
            emoteComponent.CurrentEmoteReference = emoteReferences;
            return true;
        }

        private EmoteReferences CreateNewEmoteReference(GameObject mainAsset, bool isLooping)
        {
            GameObject? mainGameObject = Object.Instantiate(mainAsset);

            Animator? animator = mainGameObject.GetComponent<Animator>();
            EmoteReferences? references = mainGameObject.AddComponent<EmoteReferences>();
            Renderer[] renderers = mainGameObject.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                // Some old emotes contain references to the avatar for easier animation, since emotes 2.0 those meshes can be shown, so in order to avoid having to update those emotes,
                // we hide renderers this specific conditions in order to avoid hiding unintentional stuff
                bool endsWithReference = renderer.name.EndsWith("_reference", StringComparison.InvariantCultureIgnoreCase);
                bool endsWithBaseMesh = renderer.name.EndsWith("_basemesh", StringComparison.InvariantCultureIgnoreCase);
                bool startsWithMask = renderer.name.StartsWith("m_mask_", StringComparison.InvariantCultureIgnoreCase);

                if (endsWithReference || endsWithBaseMesh || startsWithMask)
                    renderer.forceRenderingOff = true;
            }

            references.animator = animator;

            RuntimeAnimatorController? rac = animator.runtimeAnimatorController;
            List<AnimationClip> uniqueClips = ListPool<AnimationClip>.Get();

            foreach (AnimationClip clip in rac.animationClips)
                if (!uniqueClips.Contains(clip))
                    uniqueClips.Add(clip);

            if (uniqueClips.Count == 1)
                references.avatarClip = uniqueClips[0];
            else
            {
                foreach (AnimationClip animationClip in uniqueClips)
                {
                    if (animationClip.name.Contains("_avatar", StringComparison.OrdinalIgnoreCase))
                        references.avatarClip = animationClip;

                    if (animationClip.name.Contains("_prop", StringComparison.OrdinalIgnoreCase))
                    {
                        references.propClip = animationClip;
                        references.propClipHash = Animator.StringToHash(animationClip.name);
                    }
                }
            }

            ListPool<AnimationClip>.Release(uniqueClips);

            // some of our legacy emotes have unity events that we are not handling, so we disable that system to avoid further errors
            animator.fireEvents = false;
            return references;
        }

        public void Stop(EmoteReferences emoteReference)
        {
            if (!emotesInUse.TryGetValue(emoteReference, out GameObjectPool<EmoteReferences>? pool))
                return;

            pool.Release(emoteReference);
            emotesInUse.Remove(emoteReference);

            if (emoteReference.audioSource != null)
                audioSourcePool.Release(emoteReference.audioSource);
        }
    }
}
