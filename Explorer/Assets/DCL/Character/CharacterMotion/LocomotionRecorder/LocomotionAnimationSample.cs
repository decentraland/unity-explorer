using System.Collections.Generic;
using UnityEngine;
using Utility.Animations;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>One clip of the base layer's pose: <c>extra.anim</c> in the trace.</summary>
    public struct LocomotionAnimClip
    {
        /// <summary><c>AnimationClip.name</c>.</summary>
        public string Clip;

        /// <summary>The clip's weight as the Animator reports it (<c>AnimatorClipInfo.weight</c>), which already includes the state's share of a running transition.</summary>
        public float W;

        /// <summary>The state's normalized time as a phase: its fractional part for a looping clip, held just below 1 once a non-looping clip has ended.</summary>
        public float T;

        /// <summary>The state's playback speed: <c>speed × speedMultiplier</c>.</summary>
        public float Speed;
    }

    /// <summary>
    ///     The own avatar's Animator, base layer, as it stands after this frame's animation update: the state and the
    ///     transition, the clips and their weights, the locomotion parameters and the weights of the point-at and rotation
    ///     layers. Written as <c>extra.anim</c>, <c>extra.anim_params</c>, <c>extra.anim_state</c> and
    ///     <c>extra.anim_layers</c> on runs with animations on.
    /// </summary>
    public struct LocomotionAnimationSample
    {
        private const int BASE_LAYER = 0;

        /// <summary>Every state of CharacterAnimator.controller, so the trace names the state a short name hash stands for.</summary>
        internal static readonly string[] STATE_NAMES =
        {
            // Base Layer
            "Movement", "Emote", "Emote Loop", "Jog_Stop", "Hit_Wall_Walk", "AFK_Action",
            "Gliding_Start", "Gliding Loop", "Gliding_End",
            "Jump", "Up", "Up To Down", "Down", "Grounded", "Long Fall", "Hard Landing", "Hard Landing Loop", "Hard Landing Exit", "Double Jump",
            "Turn_90_Right", "Turn_90_Left", "PreviousState",

            // Rotation, Upper Body Layer, RightPointAtHand
            "Empty", "RotateLeft", "RotateRight", "Masked Emote", "Masked Emote Loop", "PointAt",
        };

        private static Dictionary<int, string>? stateNames;

        /// <summary>False when the Animator was missing, disabled or not initialized this frame: the frame writes <c>"anim":null</c>.</summary>
        public bool Present;

        /// <summary>The current state's clips, then the next state's; one entry per clip name, weights summed.</summary>
        public LocomotionAnimClip[] Clips;

        public int State;
        public float StateTime;

        /// <summary>0 outside a transition.</summary>
        public int Next;

        public float NextTime;

        /// <summary>The transition's normalized time; NaN outside a transition.</summary>
        public float Progress;

        /// <summary>The sums of <c>AnimatorClipInfo.weight</c> over the current and the next state as Unity reports them.</summary>
        public float CurrentClipWeight;

        public float NextClipWeight;

        /// <summary>
        ///     The current state's clip query reported no weight (an interrupted transition, whose frozen pose Unity does not
        ///     report as clips, or a state without a motion): the clips' weights sum to less than 1.
        /// </summary>
        public bool CurrentClipsMissing;

        public float MovementBlend;
        public float SlideBlend;
        public int JumpCount;
        public bool IsGrounded;
        public bool IsFalling;
        public bool IsLongFall;
        public bool IsStunned;
        public float GlideBlend;

        /// <summary>NaN when the controller has no such layer.</summary>
        public float PointLayerWeight;

        public float RotationLayerWeight;

        /// <param name="clipInfos">Reused buffer for the clip queries.</param>
        /// <param name="clips">Reused buffer for the merged clips.</param>
        internal static LocomotionAnimationSample Read(Animator? animator, List<AnimatorClipInfo> clipInfos, List<LocomotionAnimClip> clips)
        {
            if (animator == null || !animator.isActiveAndEnabled || !animator.isInitialized || animator.runtimeAnimatorController == null)
                return default(LocomotionAnimationSample);

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(BASE_LAYER);
            bool inTransition = animator.IsInTransition(BASE_LAYER);
            float progress = inTransition ? Mathf.Min(animator.GetAnimatorTransitionInfo(BASE_LAYER).normalizedTime, 1f) : float.NaN;

            clips.Clear();
            animator.GetCurrentAnimatorClipInfo(BASE_LAYER, clipInfos);
            float currentClipWeight = AddClips(clipInfos, current, clips);

            AnimatorStateInfo next = default;
            var nextClipWeight = 0f;

            if (inTransition)
            {
                next = animator.GetNextAnimatorStateInfo(BASE_LAYER);
                animator.GetNextAnimatorClipInfo(BASE_LAYER, clipInfos);
                nextClipWeight = AddClips(clipInfos, next, clips);
            }

            return new LocomotionAnimationSample
            {
                Present = true,
                Clips = clips.ToArray(),
                State = current.shortNameHash,
                StateTime = current.normalizedTime,
                Next = inTransition ? next.shortNameHash : 0,
                NextTime = inTransition ? next.normalizedTime : float.NaN,
                Progress = progress,
                CurrentClipWeight = currentClipWeight,
                NextClipWeight = inTransition ? nextClipWeight : float.NaN,
                CurrentClipsMissing = currentClipWeight <= 0f,
                MovementBlend = animator.GetFloat(AnimationHashes.MOVEMENT_BLEND),
                SlideBlend = animator.GetFloat(AnimationHashes.SLIDE_BLEND),
                JumpCount = animator.GetInteger(AnimationHashes.JUMP_COUNT),
                IsGrounded = animator.GetBool(AnimationHashes.GROUNDED),
                IsFalling = animator.GetBool(AnimationHashes.FALLING),
                IsLongFall = animator.GetBool(AnimationHashes.LONG_FALL),
                IsStunned = animator.GetBool(AnimationHashes.STUNNED),
                GlideBlend = animator.GetFloat(AnimationHashes.GLIDE_BLEND),
                PointLayerWeight = LayerWeight(animator, "RightPointAtHand"),
                RotationLayerWeight = LayerWeight(animator, "Rotation"),
            };
        }

        /// <returns>The sum of the reported clip weights.</returns>
        private static float AddClips(List<AnimatorClipInfo> clipInfos, AnimatorStateInfo state, List<LocomotionAnimClip> clips)
        {
            var sum = 0f;
            float speed = state.speed * state.speedMultiplier;

            foreach (AnimatorClipInfo info in clipInfos)
            {
                float w = info.weight;
                sum += w;

                if (info.clip == null || w <= 1e-6f)
                    continue;

                float time = state.normalizedTime;
                float phase = info.clip.isLooping ? time - Mathf.Floor(time) : Mathf.Min(time, 0.9999f);
                AddClip(clips, new LocomotionAnimClip { Clip = info.clip.name, W = w, T = phase, Speed = speed });
            }

            return sum;
        }

        /// <summary>A clip in both states (or twice in one blend tree) is one entry: weights add up, phase and speed follow the heavier part.</summary>
        internal static void AddClip(List<LocomotionAnimClip> clips, LocomotionAnimClip clip)
        {
            for (var i = 0; i < clips.Count; i++)
            {
                LocomotionAnimClip existing = clips[i];

                if (existing.Clip != clip.Clip)
                    continue;

                if (clip.W > existing.W)
                {
                    existing.T = clip.T;
                    existing.Speed = clip.Speed;
                }

                existing.W += clip.W;
                clips[i] = existing;
                return;
            }

            clips.Add(clip);
        }

        private static float LayerWeight(Animator animator, string layer)
        {
            int index = animator.GetLayerIndex(layer);
            return index < 0 ? float.NaN : animator.GetLayerWeight(index);
        }

        /// <summary>The state's name, or <c>#hash</c> for a state outside <see cref="STATE_NAMES" />.</summary>
        internal static string StateName(int shortNameHash)
        {
            if (stateNames == null)
            {
                stateNames = new Dictionary<int, string>(STATE_NAMES.Length);

                foreach (string name in STATE_NAMES)
                    stateNames[Animator.StringToHash(name)] = name;
            }

            return stateNames.TryGetValue(shortNameHash, out string? stateName) ? stateName : $"#{shortNameHash}";
        }
    }
}
