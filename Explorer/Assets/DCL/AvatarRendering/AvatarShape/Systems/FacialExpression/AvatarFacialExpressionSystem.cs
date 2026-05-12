using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Assets;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common.Components;
using Runtime.Wearables;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Drives 2D facial animation for instantiated avatars: eyebrow expression (base layer),
    ///     blink (overrides eyes temporarily), and mouth pose (overrides mouth temporarily).
    ///     Pause characters in animated text restore the expression mouth instead of an articulated pose.
    ///     Material binding is delegated to <see cref="AvatarFaceMaterialUtils"/> and
    ///     all atlas / pose constants live in <see cref="AvatarFacialExpressionConstants"/>.
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class AvatarFacialExpressionSystem : BaseUnityLoopSystem
    {
        private readonly AvatarFaceAnimationSettings settings;

        internal AvatarFacialExpressionSystem(
            World world,
            AvatarFaceAnimationSettings settings) : base(world)
        {
            this.settings = settings;
        }

        protected override void Update(float t)
        {
            SetupFaceComponentsQuery(World);
            UpdateFaceQuery(World, t);
        }

        // ─── Setup ────────────────────────────────────────────────────────────

        /// <summary>
        ///     Adds <see cref="AvatarFaceComponent"/> + <see cref="AvatarMouthInputComponent"/> together
        ///     so external services (chat / voice) can write inputs without checking the face
        ///     component is present, and so the per-frame query can require both.
        /// </summary>
        [Query]
        [All(typeof(AvatarCustomSkinningComponent))]
        [None(typeof(AvatarFaceComponent), typeof(DeleteEntityIntention))]
        private void SetupFaceComponents(in Entity entity, ref AvatarShapeComponent avatarShape)
        {
            var face = new AvatarFaceComponent
            {
                EyebrowsRenderer = AvatarFaceMaterialUtils.FindRendererWithSuffix(in avatarShape, "Mask_Eyebrows"),
                EyeRenderer = AvatarFaceMaterialUtils.FindRendererWithSuffix(in avatarShape, "Mask_Eyes"),
                MouthRenderer = AvatarFaceMaterialUtils.FindRendererWithSuffix(in avatarShape, "Mask_Mouth"),
                EyebrowsExpressionIndex = AvatarFacialExpressionConstants.NO_EYEBROWS_OVERRIDE,
                EyesExpressionIndex = AvatarFacialExpressionConstants.NO_EYE_OVERRIDE,
                MouthExpressionIndex = AvatarFacialExpressionConstants.NO_MOUTH_POSE,
                CurrentEyebrowsIndex = AvatarFacialExpressionConstants.NO_EYEBROWS_OVERRIDE,
                CurrentEyeIndex = AvatarFacialExpressionConstants.NO_EYE_OVERRIDE,
                CurrentMouthPoseIndex = AvatarFacialExpressionConstants.NO_MOUTH_POSE,
                NextBlinkTime = Random.Range(settings.MinBlinkInterval, settings.MaxBlinkInterval),
                IsDirty = true,
            };

            ResolveFaceExpressionFlags(in avatarShape, ref face);

            // Atlas-bound channels must start at a valid cell (-1 disables slicing in the shader,
            // which would expose the whole atlas). Idle (0) is the safe resting cell.
            if (face.EyebrowsHasExpressions) face.EyebrowsExpressionIndex = 0;
            if (face.EyesHasExpressions) face.EyesExpressionIndex = 0;
            if (face.MouthHasExpressions) face.MouthExpressionIndex = 0;

            World.Add(entity, face, new AvatarMouthInputComponent());
        }

        /// <summary>
        ///     Reads the worn facial-feature wearables and stamps per-channel expression capability
        ///     onto the face component. Wearables whose load picked up a `*_expressions.png` atlas
        ///     enable the corresponding atlas-frame overrides; others stay static.
        /// </summary>
        private static void ResolveFaceExpressionFlags(in AvatarShapeComponent avatarShape, ref AvatarFaceComponent face)
        {
            face.EyebrowsHasExpressions = false;
            face.EyesHasExpressions = false;
            face.MouthHasExpressions = false;

            StreamableLoadingResult<WearablesResolution>? result = avatarShape.WearablePromise.Result;

            if (!result.HasValue || !result.Value.Succeeded) return;

            IList<IWearable> wearables = result.Value.Asset.Wearables;

            for (var i = 0; i < wearables.Count; i++)
            {
                IWearable wearable = wearables[i];

                if (wearable.Type != WearableType.FacialFeature) continue;
                if (!wearable.HasFacialExpressionsTexture) continue;

                string category = wearable.GetCategory();

                if (category == WearableCategories.Categories.EYEBROWS)
                    face.EyebrowsHasExpressions = true;
                else if (category == WearableCategories.Categories.EYES)
                    face.EyesHasExpressions = true;
                else if (category == WearableCategories.Categories.MOUTH)
                    face.MouthHasExpressions = true;
            }
        }

        // ─── Per-frame update ─────────────────────────────────────────────────

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateFace(
            [Data] float t,
            ref AvatarFaceComponent face,
            ref AvatarMouthInputComponent mouthInput,
            ref AvatarShapeComponent avatarShape)
        {
            ReInitRenderersIfNeeded(ref face, in avatarShape);

            if (face.IsDirty)
                ApplyExpressionLayer(ref face);

            bool visible = avatarShape.IsVisible;

            // Blink and mouth animation drive atlas slice overrides via MaterialPropertyBlock.
            // Skip whole-channel when the wearable cannot atlas-slice, leaving the static face.
            if (face.EyesHasExpressions && face.EyeRenderer != null)
                StepBlink(t, ref face, visible);

            if (face.MouthHasExpressions && face.MouthRenderer != null)
                StepMouthAnimation(t, ref face, ref mouthInput, visible);
        }

        private void ReInitRenderersIfNeeded(ref AvatarFaceComponent face, in AvatarShapeComponent avatarShape)
        {
            bool rebuilt = false;

            if (face.EyebrowsRenderer == null)
            {
                Renderer? r = AvatarFaceMaterialUtils.FindRendererWithSuffix(in avatarShape, "Mask_Eyebrows");

                if (r != null)
                {
                    face.EyebrowsRenderer = r;
                    face.CurrentEyebrowsIndex = AvatarFacialExpressionConstants.NO_EYEBROWS_OVERRIDE;
                    face.IsDirty = true;
                    rebuilt = true;
                }
            }

            if (face.EyeRenderer == null)
            {
                Renderer? r = AvatarFaceMaterialUtils.FindRendererWithSuffix(in avatarShape, "Mask_Eyes");

                if (r != null)
                {
                    face.EyeRenderer = r;
                    face.IsBlinking = false;
                    face.BlinkFrameIndex = 0;
                    face.BlinkFrameTimer = 0f;
                    face.TimeSinceLastBlink = 0f;
                    face.CurrentEyeIndex = AvatarFacialExpressionConstants.NO_EYE_OVERRIDE;
                    face.NextBlinkTime = Random.Range(settings.MinBlinkInterval, settings.MaxBlinkInterval);
                    rebuilt = true;
                }
            }

            if (face.MouthRenderer == null)
            {
                Renderer? r = AvatarFaceMaterialUtils.FindRendererWithSuffix(in avatarShape, "Mask_Mouth");

                if (r != null)
                {
                    face.MouthRenderer = r;
                    face.AnimatingText = null;
                    face.CharacterIndex = 0;
                    face.CharacterTimer = 0f;
                    face.CurrentMouthPoseIndex = AvatarFacialExpressionConstants.NO_MOUTH_POSE;
                    rebuilt = true;
                }
            }

            // Wearable swap rebound at least one renderer: refresh per-channel capability flags
            // from the new set of worn facial features.
            if (rebuilt)
                ResolveFaceExpressionFlags(in avatarShape, ref face);
        }

        /// <summary>
        ///     Pushes the expression layer to renderers. Eyebrows always apply; eyes/mouth apply
        ///     only when no override layer is currently animating — the override layer will
        ///     restore to the new resting indices when it ends.
        /// </summary>
        private void ApplyExpressionLayer(ref AvatarFaceComponent face)
        {
            face.IsDirty = false;

            // Channels whose wearable lacks an `*_expressions.png` atlas keep the wearable's static
            // texture: skip the MaterialPropertyBlock override so we don't paint global atlas slices
            // onto a single-frame face texture.
            if (face.EyebrowsHasExpressions)
                AvatarFaceMaterialUtils.ApplyEyebrowsFrame(ref face, face.EyebrowsExpressionIndex);

            if (face.EyesHasExpressions && !face.IsBlinking)
                AvatarFaceMaterialUtils.ApplyEyeFrame(ref face, face.EyesExpressionIndex);

            if (face.MouthHasExpressions && face.AnimatingText == null)
                AvatarFaceMaterialUtils.ApplyMouthPose(ref face, face.MouthExpressionIndex);
        }

        // ─── Blink layer ──────────────────────────────────────────────────────

        private void StepBlink(float t, ref AvatarFaceComponent face, bool isVisible)
        {
            if (!isVisible || !face.EyeRenderer.enabled)
            {
                if (face.IsBlinking)
                    AvatarFaceMaterialUtils.EndBlink(ref face, settings.MinBlinkInterval, settings.MaxBlinkInterval);

                return;
            }

            if (face.IsBlinking)
            {
                face.BlinkFrameTimer += t;

                if (face.BlinkFrameTimer < settings.BlinkFrameDuration) return;

                face.BlinkFrameTimer = 0f;
                face.BlinkFrameIndex++;

                if (face.BlinkFrameIndex >= AvatarFacialExpressionConstants.BLINK_SEQUENCE.Length)
                    AvatarFaceMaterialUtils.EndBlink(ref face, settings.MinBlinkInterval, settings.MaxBlinkInterval);
                else
                    AvatarFaceMaterialUtils.ApplyEyeFrame(ref face, AvatarFacialExpressionConstants.BLINK_SEQUENCE[face.BlinkFrameIndex]);
            }
            else
            {
                face.TimeSinceLastBlink += t;

                if (face.TimeSinceLastBlink >= face.NextBlinkTime)
                    AvatarFaceMaterialUtils.StartBlink(ref face);
            }
        }

        // ─── Mouth animation layer ────────────────────────────────────────────

        private void StepMouthAnimation(float t, ref AvatarFaceComponent face, ref AvatarMouthInputComponent input, bool isVisible)
        {
            if (!isVisible || !face.MouthRenderer.enabled)
            {
                AvatarFaceMaterialUtils.StopMouthAnimation(ref face);
                return;
            }

            // Chat message overrides the voice-chat loop; consume it.
            if (input.MessageIsDirty)
            {
                face.AnimatingText = input.PendingMessage;
                face.CharacterIndex = 0;
                face.CharacterTimer = 0f;
                input.MessageIsDirty = false;
            }

            bool isSpeaking = input.IsVoiceChatSpeaking;

            if (face.AnimatingText != null && face.CharacterIndex < face.AnimatingText.Length)
            {
                // Stop the voice loop if speaking ended mid-animation.
                if (!isSpeaking && face.AnimatingText == AvatarFacialExpressionConstants.VOICE_CHAT_LOOP_TEXT)
                {
                    AvatarFaceMaterialUtils.StopMouthAnimation(ref face);
                    return;
                }

                int mouthPose = AvatarFacialExpressionConstants.MapCharToMouthPose(face.AnimatingText, face.CharacterIndex);
                AvatarFaceMaterialUtils.ApplyMouthPose(
                    ref face,
                    mouthPose == AvatarFacialExpressionConstants.NO_MOUTH_POSE ? face.MouthExpressionIndex : mouthPose);

                face.CharacterTimer += t;

                char currentChar = face.AnimatingText[face.CharacterIndex];
                float baseDuration = AvatarFacialExpressionConstants.IsVowel(currentChar) ? settings.VowelMouthPoseDuration : settings.MouthPoseDuration;
                float duration = char.IsUpper(currentChar) ? baseDuration * AvatarFacialExpressionConstants.UPPERCASE_DURATION_MULTIPLIER : baseDuration;

                if (face.CharacterTimer >= duration)
                {
                    face.CharacterTimer = 0f;
                    face.CharacterIndex++;
                }
            }
            else if (isSpeaking)
            {
                face.AnimatingText = AvatarFacialExpressionConstants.VOICE_CHAT_LOOP_TEXT;
                face.CharacterIndex = 0;
                face.CharacterTimer = 0f;
            }
            else
            {
                AvatarFaceMaterialUtils.StopMouthAnimation(ref face);
            }
        }
    }
}