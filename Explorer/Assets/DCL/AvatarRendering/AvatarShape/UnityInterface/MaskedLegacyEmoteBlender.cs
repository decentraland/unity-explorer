using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.UnityInterface
{
    /// <summary>
    /// Manually blends a legacy <see cref="AnimationClip"/> with the Animator-driven locomotion
    /// pose, so that masked emotes can be played in local scene development.
    /// </summary>
    public class MaskedLegacyEmoteBlender : MonoBehaviour
    {
        private const int MAX_NON_MASKED_BONES = 100;

        private readonly Dictionary<AvatarMask, Transform[]> nonMaskedBonesByMask = new ();
        private readonly Vector3[] capturedPositions = new Vector3[MAX_NON_MASKED_BONES];
        private readonly Quaternion[] capturedRotations = new Quaternion[MAX_NON_MASKED_BONES];

        private GameObject sampleRoot = null!;
        private AnimationClip? currentClip;
        private Transform[] nonMaskedBones = Array.Empty<Transform>();

        private float time;
        private bool loop;
        private bool isPlaying;
        private bool hasFinished;

        public bool IsPlaying => isPlaying;
        public bool HasFinished => hasFinished;

        public void Initialize(GameObject sampleRoot) =>
            this.sampleRoot = sampleRoot;

        public void Play(AnimationClip clip, AvatarMask mask, bool loop)
        {
            if (sampleRoot == null)
            {
                ReportHub.LogError(ReportCategory.EMOTE, $"{nameof(MaskedLegacyEmoteBlender)} played before Initialize.");
                return;
            }

            currentClip = clip;
            this.loop = loop;
            time = 0f;
            isPlaying = true;
            hasFinished = false;

            ResolveNonMaskedBones(mask);
        }

        public void Stop()
        {
            isPlaying = false;
            hasFinished = false;
            currentClip = null;
            time = 0f;
        }

        private void ResolveNonMaskedBones(AvatarMask mask)
        {
            // Only calculate non-masked bones for new mask
            if (!nonMaskedBonesByMask.TryGetValue(mask, out Transform[] bones))
            {
                var resolved = new List<Transform>();
                Transform root = sampleRoot.transform;

                for (var i = 0; i < mask.transformCount; i++)
                {
                    if (mask.GetTransformActive(i)) continue;

                    string path = mask.GetTransformPath(i);
                    if (string.IsNullOrEmpty(path)) continue;

                    Transform bone = root.Find(path);
                    if (bone != null) resolved.Add(bone);
                }

                bones = resolved.ToArray();
                nonMaskedBonesByMask[mask] = bones;
            }

            if (bones.Length > MAX_NON_MASKED_BONES)
            {
                ReportHub.LogError(ReportCategory.EMOTE,
                    $"{nameof(MaskedLegacyEmoteBlender)}: mask resolved {bones.Length} inactive bones, exceeds MAX_NON_MASKED_BONES={MAX_NON_MASKED_BONES}. Bump the constant.");
                nonMaskedBones = Array.Empty<Transform>();
                return;
            }

            nonMaskedBones = bones;
        }

        private void LateUpdate()
        {
            if (!isPlaying || currentClip == null) return;

            // Snapshot bones, this happens after the Animator has updated the base locomotion
            for (var i = 0; i < nonMaskedBones.Length; i++)
            {
                Transform bone = nonMaskedBones[i];
                capturedPositions[i] = bone.localPosition;
                capturedRotations[i] = bone.localRotation;
            }

            // Manually play the legacy emote animation to be masked
            currentClip.SampleAnimation(sampleRoot, time);

            // Restore snapshot non-masked bones, this ends up blending the animator + animation
            for (var i = 0; i < nonMaskedBones.Length; i++)
            {
                Transform bone = nonMaskedBones[i];
                bone.localPosition = capturedPositions[i];
                bone.localRotation = capturedRotations[i];
            }

            time += Time.deltaTime;

            float length = currentClip.length;
            if (length <= 0f) return;
            if (time < length) return;

            if (loop)
                time %= length;
            else
            {
                isPlaying = false;
                hasFinished = true;
            }
        }
    }
}
