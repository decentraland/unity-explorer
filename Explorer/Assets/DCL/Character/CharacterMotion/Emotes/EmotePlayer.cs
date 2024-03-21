using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Character.CharacterMotion.Emotes
{
    public class EmotePlayer
    {
        private readonly string reportCategory;
        private readonly Dictionary<GameObject, GameObjectPool<EmoteReferences>> pools = new ();
        private readonly Dictionary<EmoteReferences, GameObjectPool<EmoteReferences>> emotesInUse = new ();
        private readonly Transform poolRoot;

        public EmotePlayer(string reportCategory)
        {
            this.reportCategory = reportCategory;
            poolRoot = GameObject.Find("ROOT_POOL_CONTAINER").transform;
        }

        public bool Play(GameObject mainAsset, bool isLooping, in IAvatarView avatarBase, ref CharacterEmoteComponent animationComponent)
        {
            Animator animator = mainAsset.GetComponent<Animator>();

            if (animator == null)
                return false;

            EmoteReferences? emoteInUse = animationComponent.CurrentEmoteReference;

            if (emoteInUse != null)
                Stop(emoteInUse);

            Transform avatarTransform = avatarBase.GetTransform();

            if (!pools.ContainsKey(mainAsset))
                pools.Add(mainAsset, new GameObjectPool<EmoteReferences>(poolRoot, () => CreateNewEmoteReference(mainAsset, isLooping, avatarTransform.gameObject.layer)));

            EmoteReferences? emoteReferences = pools[mainAsset].Get();
            if (!emoteReferences) return false;

            Transform emoteTransform = emoteReferences.transform;
            emoteTransform.SetParent(avatarTransform, false);
            emoteTransform.localPosition = Vector3.zero;
            emoteTransform.localRotation = Quaternion.identity;
            emoteTransform.gameObject.layer = avatarTransform.gameObject.layer;

            if (emoteReferences.avatarClip != null)
            {
                animationComponent.WasEmoteJustTriggered = true;
                animationComponent.EmoteClip = emoteReferences.avatarClip;
                animationComponent.EmoteLoop = isLooping;
            }

            if (emoteReferences.propClip != null)
                emoteReferences.animator.SetTrigger(emoteReferences.propClipHash);

            emotesInUse.Add(emoteReferences, pools[mainAsset]);
            animationComponent.CurrentEmoteReference = emoteReferences;
            return true;
        }

        private EmoteReferences CreateNewEmoteReference(GameObject mainAsset, bool isLooping, int layer)
        {
            GameObject? mainGameObject = Object.Instantiate(mainAsset);
            mainGameObject.layer = layer;

            PoolExtensions.Scope<List<Transform>> children = mainGameObject.GetComponentsInChildrenIntoPooledList<Transform>(true);

            foreach (Transform? child in children.Value)
                if (child != null)
                    child.gameObject.layer = layer;

            Animator? animator = mainGameObject.GetComponent<Animator>();
            EmoteReferences? references = mainGameObject.AddComponent<EmoteReferences>();

            references.animator = animator;

            RuntimeAnimatorController? rac = animator.runtimeAnimatorController;
            AnimationClip[]? clips = rac.animationClips;

            if (clips.Length == 1)
                references.avatarClip = clips[0];
            else
                foreach (AnimationClip animationClip in clips)
                {
                    if (isLooping)
                    {
                        animationClip.wrapMode = WrapMode.Loop;
                        AnimationClipSettings? settings = AnimationUtility.GetAnimationClipSettings(animationClip);
                        settings.loopTime = true;
                        AnimationUtility.SetAnimationClipSettings(animationClip, settings);
                    }

                    if (animationClip.name.Contains("_avatar", StringComparison.OrdinalIgnoreCase))
                        references.avatarClip = animationClip;

                    if (animationClip.name.Contains("_prop", StringComparison.OrdinalIgnoreCase))
                    {
                        references.propClip = animationClip;
                        references.propClipHash = Animator.StringToHash(animationClip.name);
                    }
                }

            // some of our legacy emotes have unity events that we are not handling, so we disable that system to avoid further errors
            animator.fireEvents = false;
            return references;
        }

        public void Stop(EmoteReferences emoteReference)
        {
            if (!emotesInUse.TryGetValue(emoteReference, out GameObjectPool<EmoteReferences>? pool)) return;
            pool.Release(emoteReference);
            emotesInUse.Remove(emoteReference);
        }
    }
}
