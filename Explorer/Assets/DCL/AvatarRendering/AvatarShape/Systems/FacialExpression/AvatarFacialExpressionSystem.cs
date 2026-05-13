using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Assets;
using DCL.AvatarRendering.AvatarShape.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
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
                EyebrowsHasExpressionAtlas = avatarShape.EyebrowsHasExpressionAtlas,
                EyesHasExpressionAtlas = avatarShape.EyesHasExpressionAtlas,
                MouthHasExpressionAtlas = avatarShape.MouthHasExpressionAtlas,
                EyebrowsExpressionIndex = avatarShape.EyebrowsHasExpressionAtlas ? 0 : AvatarFacialExpressionConstants.NO_EYEBROWS_OVERRIDE,
                EyesExpressionIndex = avatarShape.EyesHasExpressionAtlas ? 0 : AvatarFacialExpressionConstants.NO_EYE_OVERRIDE,
                MouthExpressionIndex = avatarShape.MouthHasExpressionAtlas ? 0 : AvatarFacialExpressionConstants.NO_MOUTH_POSE,
                CurrentEyebrowsIndex = AvatarFacialExpressionConstants.NO_EYEBROWS_OVERRIDE,
                CurrentEyeIndex = AvatarFacialExpressionConstants.NO_EYE_OVERRIDE,
                CurrentMouthPoseIndex = AvatarFacialExpressionConstants.NO_MOUTH_POSE,
                NextBlinkTime = Random.Range(settings.MinBlinkInterval, settings.MaxBlinkInterval),
                IsDirty = true,
            };

            World.Add(entity, face, new AvatarMouthInputComponent());
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

            // Run every frame: the skinning material pool can swap the face material under us on a
            // wearable swap (same renderer instance, fresh shader-default _ExpressionIndex). Gating on
            // IsDirty would let those pool-reset materials render the full atlas until the next input.
            ApplyExpressionLayer(ref face);

            bool visible = avatarShape.IsVisible;

            // Blink and mouth animation drive atlas slice overrides via MaterialPropertyBlock.
            // Skip whole-channel when the wearable cannot atlas-slice, leaving the static face.
            if (face.EyesHasExpressionAtlas && face.EyeRenderer != null)
                StepBlink(t, ref face, visible);

            if (face.MouthHasExpressionAtlas && face.MouthRenderer != null)
                StepMouthAnimation(t, ref face, ref mouthInput, visible);
        }

        // Wearable swaps replace the face renderers (pool may reuse the same instance with a
        // fresh-default material). Detect any change in renderer ref or capability bool, then
        // reset Current* / resting indices so the next ApplyExpressionLayer pushes the correct value.
        private void ReInitRenderersIfNeeded(ref AvatarFaceComponent face, in AvatarShapeComponent avatarShape)
        {
            Renderer? eyebrows = AvatarFaceMaterialUtils.FindRendererWithSuffix(in avatarShape, "Mask_Eyebrows");
            Renderer? eyes = AvatarFaceMaterialUtils.FindRendererWithSuffix(in avatarShape, "Mask_Eyes");
            Renderer? mouth = AvatarFaceMaterialUtils.FindRendererWithSuffix(in avatarShape, "Mask_Mouth");

            bool rebuilt = false;

            if (eyebrows != null && (face.EyebrowsRenderer != eyebrows || face.EyebrowsHasExpressionAtlas != avatarShape.EyebrowsHasExpressionAtlas))
            {
                face.EyebrowsRenderer = eyebrows;
                face.EyebrowsHasExpressionAtlas = avatarShape.EyebrowsHasExpressionAtlas;
                face.EyebrowsExpressionIndex = avatarShape.EyebrowsHasExpressionAtlas ? 0 : AvatarFacialExpressionConstants.NO_EYEBROWS_OVERRIDE;
                face.CurrentEyebrowsIndex = AvatarFacialExpressionConstants.NO_EYEBROWS_OVERRIDE;
                rebuilt = true;
            }

            if (eyes != null && (face.EyeRenderer != eyes || face.EyesHasExpressionAtlas != avatarShape.EyesHasExpressionAtlas))
            {
                face.EyeRenderer = eyes;
                face.EyesHasExpressionAtlas = avatarShape.EyesHasExpressionAtlas;
                face.EyesExpressionIndex = avatarShape.EyesHasExpressionAtlas ? 0 : AvatarFacialExpressionConstants.NO_EYE_OVERRIDE;
                face.CurrentEyeIndex = AvatarFacialExpressionConstants.NO_EYE_OVERRIDE;
                face.IsBlinking = false;
                face.BlinkFrameIndex = 0;
                face.BlinkFrameTimer = 0f;
                face.TimeSinceLastBlink = 0f;
                face.NextBlinkTime = Random.Range(settings.MinBlinkInterval, settings.MaxBlinkInterval);
                rebuilt = true;
            }

            if (mouth != null && (face.MouthRenderer != mouth || face.MouthHasExpressionAtlas != avatarShape.MouthHasExpressionAtlas))
            {
                face.MouthRenderer = mouth;
                face.MouthHasExpressionAtlas = avatarShape.MouthHasExpressionAtlas;
                face.MouthExpressionIndex = avatarShape.MouthHasExpressionAtlas ? 0 : AvatarFacialExpressionConstants.NO_MOUTH_POSE;
                face.CurrentMouthPoseIndex = AvatarFacialExpressionConstants.NO_MOUTH_POSE;
                face.AnimatingText = null;
                face.CharacterIndex = 0;
                face.CharacterTimer = 0f;
                rebuilt = true;
            }

            if (rebuilt)
                face.IsDirty = true;
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
            if (face.EyebrowsHasExpressionAtlas)
                AvatarFaceMaterialUtils.ApplyEyebrowsFrame(ref face, face.EyebrowsExpressionIndex);

            if (face.EyesHasExpressionAtlas && !face.IsBlinking)
                AvatarFaceMaterialUtils.ApplyEyeFrame(ref face, face.EyesExpressionIndex);

            if (face.MouthHasExpressionAtlas && face.AnimatingText == null)
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
