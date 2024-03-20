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

        public bool Play(GameObject mainAsset, bool isLooping, in AvatarBase avatarBase, ref CharacterAnimationComponent animationComponent)
        {
            Animator animator = mainAsset.GetComponent<Animator>();

            if (animator == null)
                return false;

            EmoteReferences? emoteInUse = animationComponent.States.CurrentEmoteReference;

            if (emoteInUse != null)
            {
                if (emotesInUse.TryGetValue(emoteInUse, out GameObjectPool<EmoteReferences>? pool))
                {
                    pool.Release(emoteInUse);
                    emotesInUse.Remove(emoteInUse);
                }
            }

            if (!pools.ContainsKey(mainAsset))
                pools.Add(mainAsset, new GameObjectPool<EmoteReferences>(poolRoot, () => CreateNewEmoteReference(mainAsset, isLooping)));

            EmoteReferences? emoteReferences = pools[mainAsset].Get();
            if (!emoteReferences) return false;

            Transform emoteTransform = emoteReferences.transform;
            emoteTransform.SetParent(avatarBase.transform, false);
            emoteTransform.localPosition = Vector3.zero;
            emoteTransform.localRotation = Quaternion.identity;

            if (emoteReferences.avatarClip != null)
            {
                animationComponent.States.WasEmoteJustTriggered = true;
                animationComponent.States.EmoteClip = emoteReferences.avatarClip;
                animationComponent.States.EmoteLoop = isLooping;
            }

            if (emoteReferences.propClip != null)
                emoteReferences.animator.SetTrigger(emoteReferences.propClipHash);

            emotesInUse.Add(emoteReferences, pools[mainAsset]);
            animationComponent.States.CurrentEmoteReference = emoteReferences;
            return true;
        }

        private EmoteReferences CreateNewEmoteReference(GameObject mainAsset, bool isLooping)
        {
            GameObject? mainGameObject = Object.Instantiate(mainAsset);

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
    }
}
