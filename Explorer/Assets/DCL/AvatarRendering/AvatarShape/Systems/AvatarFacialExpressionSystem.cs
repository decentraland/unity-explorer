using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.Loading.Assets;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Drives all 2D facial animations for instantiated avatars: eyebrow expressions,
    ///     eye blinking, and mouth mouth pose animation.
    ///     Expressions act as the base (resting) layer. Blink and mouth pose systems temporarily
    ///     override the eyes and mouth respectively, then restore the expression when they end.
    ///     Uses MaterialPropertyBlock overrides per-renderer to avoid modifying shared pool materials.
    /// </summary>
    /// <remarks>
    ///     Eyebrows atlas layout (1024×1024, 4×4 grid of 256px cells, top-to-bottom):
    ///     Row 0: Idle(0),     Up(1),        Down(2),      Angry(3)
    ///     Row 1: Sad(4),      Surprised(5), Unused(6),    Unused(7)
    ///     Row 2-3: Unused(8..15)
    ///
    ///     Eye atlas layout (1024×1024, 4×4 grid of 256px cells, top-to-bottom):
    ///     Row 0: Idle(0),    HalfClosed(1), Closed(2), WideOpen(3)
    ///     Row 1: LookUp(4),  LookDown(5),   LookLeft(6), LookRight(7)
    ///     Row 2-3: Unused(8..15)
    ///
    ///     Mouth atlas layout (1024×1024, 4×4 grid of 256px cells, top-to-bottom):
    ///     Row 0: Idle(0), a/e/i(1), b/m/p(2), f/v(3)
    ///     Row 1: d/th(4),  u(5),     c/g/h/k/n/s/t/x/y/z(6), o(7)
    ///     Row 2: l(8),   r(9),     ch/j/sh(10),               w/q(11)
    ///     Row 3: Sad(12), Happy(13), Smile(14), Worried(15)
    /// </remarks>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class AvatarFacialExpressionSystem : BaseUnityLoopSystem
    {
        /// <summary>Sentinel value: no eye MaterialPropertyBlock override is active (material default = open eyes).</summary>
        private const int NO_EYE_OVERRIDE = -1;

        /// <summary>Sentinel value: no mouth pose MaterialPropertyBlock override is active.</summary>
        private const int NO_MOUTH_POSE = -1;

        /// <summary>Uppercase letters hold their mouth pose for this multiple of <see cref="mouthPoseDuration"/>.</summary>
        private const float UPPERCASE_DURATION_MULTIPLIER = 2f;

        /// <summary>Sentinel value: no eyebrows MaterialPropertyBlock override is active.</summary>
        private const int NO_EYEBROWS_OVERRIDE = -1;

        /// <summary>
        ///     Mouth-pose-rich text looped while an avatar is actively speaking in voice chat.
        ///     Covers lip-closure (m/p), open vowels (a/i), L-shape (l), rounded (o/u), and
        ///     labiodental (f) so the animation looks naturally varied.
        /// </summary>
        private const string VOICE_CHAT_LOOP_TEXT = "el murcielago hindu comia feliz cardillo y kiwi";

        // Eye atlas slice indices.
        private const int EYE_HALF_CLOSED = 1;
        private const int EYE_CLOSED = 2;

        /// <summary>
        ///     Atlas slice sequence played during a blink: closing (HalfClosed → Closed) then opening (Closed → HalfClosed).
        ///     The final return to fully open clears the override (or restores expression eye index).
        /// </summary>
        private static readonly int[] BLINK_SEQUENCE = { EYE_HALF_CLOSED, EYE_CLOSED, EYE_HALF_CLOSED };

        private static readonly int MAINTEX_ARR_SHADER_INDEX = TextureArrayConstants.MAINTEX_ARR_SHADER_INDEX;
        private static readonly int MAINTEX_ARR_TEX_SHADER = TextureArrayConstants.MAINTEX_ARR_TEX_SHADER;

        // Reused every frame to avoid per-call allocation.
        private static readonly MaterialPropertyBlock s_Mpb = new MaterialPropertyBlock();

        private readonly Texture2DArray? eyebrowsTextureArray;
        private readonly Texture2DArray? eyeTextureArray;
        private readonly float minBlinkInterval;
        private readonly float maxBlinkInterval;
        private readonly float blinkFrameDuration;
        private readonly Texture2DArray? mouthPoseTextureArray;
        private readonly float mouthPoseDuration;
        private readonly AvatarFaceDebugData? debugData;

        internal AvatarFacialExpressionSystem(
            World world,
            Texture2DArray? eyebrowsTextureArray,
            Texture2DArray? eyeTextureArray,
            float minBlinkInterval,
            float maxBlinkInterval,
            float blinkFrameDuration,
            Texture2DArray? mouthPoseTextureArray,
            float mouthPoseDuration,
            AvatarFaceDebugData? debugData = null) : base(world)
        {
            this.eyebrowsTextureArray = eyebrowsTextureArray;
            this.eyeTextureArray = eyeTextureArray;
            this.minBlinkInterval = minBlinkInterval;
            this.maxBlinkInterval = maxBlinkInterval;
            this.blinkFrameDuration = blinkFrameDuration;
            this.mouthPoseTextureArray = mouthPoseTextureArray;
            this.mouthPoseDuration = mouthPoseDuration;
            this.debugData = debugData;
        }

        protected override void Update(float t)
        {
            // Setup pass — adds per-avatar face components to newly instantiated avatars.
            SetupExpressionComponentQuery(World);

            if (eyeTextureArray != null)
                SetupBlinkComponentQuery(World);

            if (mouthPoseTextureArray != null)
                SetupMouthComponentQuery(World);

            // When the debug widget changed the expression, propagate it to all avatar expression components.
            if (debugData != null && debugData.IsDirty)
            {
                ApplyDebugExpressionQuery(World);
                debugData.IsDirty = false;
            }

            // When the debug widget requested a manual blink, start one on all avatars.
            if (debugData != null && debugData.TriggerBlink)
            {
                TriggerBlinkDebugQuery(World);
                debugData.TriggerBlink = false;
            }

            // Expression layer — applies eyebrows and syncs base indices into blink/mouth components.
            UpdateFaceExpressionQuery(World);

            // Blink overrides eyes temporarily.
            if (eyeTextureArray != null)
                UpdateBlinkQuery(World, t);

            // mouth pose animation overrides mouth temporarily.
            if (mouthPoseTextureArray != null)
                UpdateMouthAnimationQuery(World, t);
        }

        // ─── Setup queries ────────────────────────────────────────────────────

        /// <summary>
        ///     Adds AvatarFaceExpressionComponent to newly instantiated avatars.
        /// </summary>
        [Query]
        [All(typeof(AvatarCustomSkinningComponent))]
        [None(typeof(AvatarFaceExpressionComponent), typeof(DeleteEntityIntention))]
        private void SetupExpressionComponent(in Entity entity, ref AvatarShapeComponent avatarShape)
        {
            Renderer? eyebrowsRenderer = FindRendererWithSuffix(ref avatarShape, "Mask_Eyebrows");

            World.Add(entity, new AvatarFaceExpressionComponent
            {
                EyebrowsRenderer = eyebrowsRenderer,
                EyebrowsExpressionIndex = 0,
                EyesExpressionIndex = NO_EYE_OVERRIDE,
                MouthExpressionIndex = NO_MOUTH_POSE,
                CurrentEyebrowsIndex = NO_EYEBROWS_OVERRIDE,
                IsDirty = false,
            });
        }

        /// <summary>
        ///     Adds AvatarBlinkComponent to newly instantiated avatars that do not yet have one.
        /// </summary>
        [Query]
        [All(typeof(AvatarCustomSkinningComponent))]
        [None(typeof(AvatarBlinkComponent), typeof(DeleteEntityIntention))]
        private void SetupBlinkComponent(in Entity entity, ref AvatarShapeComponent avatarShape)
        {
            Renderer? eyeRenderer = FindRendererWithSuffix(ref avatarShape, "Mask_Eyes");

            if (eyeRenderer == null)
                return;

            World.Add(entity, new AvatarBlinkComponent
            {
                EyeRenderer = eyeRenderer,
                NextBlinkTime = Random.Range(minBlinkInterval, maxBlinkInterval),
                CurrentEyeIndex = NO_EYE_OVERRIDE,
                EyesExpressionIndex = NO_EYE_OVERRIDE,
            });
        }

        /// <summary>
        ///     Adds AvatarMouthAnimationComponent to newly instantiated avatars that do not yet have one.
        /// </summary>
        [Query]
        [All(typeof(AvatarCustomSkinningComponent))]
        [None(typeof(AvatarMouthAnimationComponent), typeof(DeleteEntityIntention))]
        private void SetupMouthComponent(in Entity entity, ref AvatarShapeComponent avatarShape)
        {
            Renderer? mouthRenderer = FindRendererWithSuffix(ref avatarShape, "Mask_Mouth");

            if (mouthRenderer == null)
                return;

            World.Add(entity, new AvatarMouthAnimationComponent
            {
                MouthRenderer = mouthRenderer,
                CurrentMouthPoseIndex = NO_MOUTH_POSE,
                MouthExpressionIndex = NO_MOUTH_POSE,
            });
        }

        // ─── Debug expression propagation ─────────────────────────────────────

        /// <summary>
        ///     When the debug widget is dirty, overwrites the expression on every avatar and marks it dirty.
        /// </summary>
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ApplyDebugExpression(ref AvatarFaceExpressionComponent expression)
        {
            expression.EyebrowsExpressionIndex = debugData!.EyebrowsIndex;
            expression.EyesExpressionIndex = debugData.EyesIndex;
            expression.MouthExpressionIndex = debugData.MouthIndex;
            expression.IsDirty = true;
        }

        /// <summary>
        ///     Starts a blink on every avatar that is not already blinking.
        /// </summary>
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void TriggerBlinkDebug(ref AvatarBlinkComponent blink)
        {
            if (!blink.IsBlinking)
                StartBlink(ref blink);
        }

        // ─── Expression layer ─────────────────────────────────────────────────

        /// <summary>
        ///     Applies a dirty expression to the eyebrows renderer and propagates the eye/mouth
        ///     base indices into the blink and mouth components so they restore correctly after
        ///     their temporary overrides finish.
        ///     Also handles re-initialisation of the eyebrows renderer after avatar re-instantiation.
        /// </summary>
        [Query]
        [All(typeof(AvatarBlinkComponent), typeof(AvatarMouthAnimationComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateFaceExpression(
            ref AvatarFaceExpressionComponent expression,
            ref AvatarBlinkComponent blink,
            ref AvatarMouthAnimationComponent mouth,
            ref AvatarShapeComponent avatarShape)
        {
            // Re-initialise eyebrows renderer if it was destroyed during avatar re-instantiation.
            if (expression.EyebrowsRenderer == null)
            {
                Renderer? eyebrowsRenderer = FindRendererWithSuffix(ref avatarShape, "Mask_Eyebrows");

                if (eyebrowsRenderer != null)
                {
                    expression.EyebrowsRenderer = eyebrowsRenderer;
                    expression.CurrentEyebrowsIndex = NO_EYEBROWS_OVERRIDE;
                    expression.IsDirty = true;
                }
            }

            if (!expression.IsDirty)
                return;

            expression.IsDirty = false;

            // Apply eyebrows base layer directly (nothing else overrides eyebrows).
            if (expression.EyebrowsRenderer != null && eyebrowsTextureArray != null)
                ApplyEyebrowsFrame(ref expression, expression.EyebrowsExpressionIndex);

            // Sync the resting eye state into the blink component so EndBlink restores it.
            blink.EyesExpressionIndex = expression.EyesExpressionIndex;

            // If not currently blinking, apply the expression eye immediately.
            if (!blink.IsBlinking)
                ApplyEyeFrame(ref blink, expression.EyesExpressionIndex);

            // Sync the resting mouth state into the mouth component so StopMouth restores it.
            mouth.MouthExpressionIndex = expression.MouthExpressionIndex;

            // If not currently animating mouth poses, apply the expression mouth immediately.
            if (mouth.AnimatingText == null)
                ApplyMouthPose(ref mouth, expression.MouthExpressionIndex);
        }

        // ─── Blink ────────────────────────────────────────────────────────────

        /// <summary>
        ///     Advances the blink animation sequence for each avatar.
        ///     Also handles re-initialisation when the eye renderer has been replaced
        ///     (e.g. after a wearable change triggers avatar re-instantiation).
        /// </summary>
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateBlink([Data] float t, ref AvatarBlinkComponent blink, ref AvatarShapeComponent avatarShape)
        {
            // Re-initialise when the renderer was destroyed by a re-instantiation.
            if (blink.EyeRenderer == null)
            {
                Renderer? eyeRenderer = FindRendererWithSuffix(ref avatarShape, "Mask_Eyes");

                if (eyeRenderer == null)
                    return;

                blink.EyeRenderer = eyeRenderer;
                blink.IsBlinking = false;
                blink.FrameIndex = 0;
                blink.FrameTimer = 0f;
                blink.TimeSinceLastBlink = 0f;
                blink.CurrentEyeIndex = NO_EYE_OVERRIDE;
                blink.NextBlinkTime = Random.Range(minBlinkInterval, maxBlinkInterval);
            }

            // Suppress blinking when the avatar or its eye renderer is invisible.
            if (!avatarShape.IsVisible || !blink.EyeRenderer.enabled)
            {
                if (blink.IsBlinking)
                    EndBlink(ref blink);

                return;
            }

            if (blink.IsBlinking)
            {
                blink.FrameTimer += t;

                if (blink.FrameTimer >= blinkFrameDuration)
                {
                    blink.FrameTimer = 0f;
                    blink.FrameIndex++;

                    if (blink.FrameIndex >= BLINK_SEQUENCE.Length)
                        EndBlink(ref blink);
                    else
                        ApplyEyeFrame(ref blink, BLINK_SEQUENCE[blink.FrameIndex]);
                }
            }
            else
            {
                blink.TimeSinceLastBlink += t;

                if (blink.TimeSinceLastBlink >= blink.NextBlinkTime)
                    StartBlink(ref blink);
            }
        }

        // ─── Mouth mouth pose animation ──────────────────────────────────────────

        /// <summary>
        ///     Advances the mouth pose animation for each avatar.
        ///     Re-initialises the renderer reference if the avatar was re-instantiated.
        ///     Reads <see cref="AvatarMouthInputComponent"/> for both pending chat messages and
        ///     voice-chat speaking state. Chat messages take priority over the voice loop;
        ///     the loop resumes automatically when the chat message finishes.
        /// </summary>
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateMouthAnimation([Data] float t, in Entity entity, ref AvatarMouthAnimationComponent mouth, ref AvatarShapeComponent avatarShape)
        {
            // Re-init when the renderer was destroyed by a re-instantiation.
            if (mouth.MouthRenderer == null)
            {
                Renderer? mouthRenderer = FindRendererWithSuffix(ref avatarShape, "Mask_Mouth");

                if (mouthRenderer == null)
                    return;

                mouth.MouthRenderer = mouthRenderer;
                mouth.AnimatingText = null;
                mouth.CharacterIndex = 0;
                mouth.CharacterTimer = 0f;
                mouth.CurrentMouthPoseIndex = NO_MOUTH_POSE;
            }

            // Suppress animation when the avatar or its mouth renderer is invisible.
            if (!avatarShape.IsVisible || !mouth.MouthRenderer.enabled)
            {
                StopMouthAnimation(ref mouth);
                return;
            }

            // Read all mouth input: chat message and voice-chat speaking state.
            bool isSpeaking = false;

            if (World.Has<AvatarMouthInputComponent>(entity))
            {
                ref var input = ref World.Get<AvatarMouthInputComponent>(entity);
                isSpeaking = input.IsVoiceChatSpeaking;

                // Consume a pending chat message — takes priority over the voice-chat loop.
                if (input.MessageIsDirty)
                {
                    mouth.AnimatingText = input.PendingMessage;
                    mouth.CharacterIndex = 0;
                    mouth.CharacterTimer = 0f;
                    input.MessageIsDirty = false;
                }
            }

            // Advance the mouth pose animation.
            if (mouth.AnimatingText != null && mouth.CharacterIndex < mouth.AnimatingText.Length)
            {
                // Stop the voice loop immediately if speaking ended mid-animation.
                if (!isSpeaking && mouth.AnimatingText == VOICE_CHAT_LOOP_TEXT)
                {
                    StopMouthAnimation(ref mouth);
                    return;
                }

                int mouthPose = MapCharToMouthPose(mouth.AnimatingText, mouth.CharacterIndex);
                ApplyMouthPose(ref mouth, mouthPose == NO_MOUTH_POSE ? mouth.MouthExpressionIndex : mouthPose);

                mouth.CharacterTimer += t;

                float duration = char.IsUpper(mouth.AnimatingText[mouth.CharacterIndex])
                    ? mouthPoseDuration * UPPERCASE_DURATION_MULTIPLIER
                    : mouthPoseDuration;

                if (mouth.CharacterTimer >= duration)
                {
                    mouth.CharacterTimer = 0f;
                    mouth.CharacterIndex++;
                }
            }
            else if (isSpeaking)
            {
                // Idle or text ended — start or continue the voice loop.
                mouth.AnimatingText = VOICE_CHAT_LOOP_TEXT;
                mouth.CharacterIndex = 0;
                mouth.CharacterTimer = 0f;
            }
            else
            {
                StopMouthAnimation(ref mouth);
            }
        }

        // ─── Blink helpers ────────────────────────────────────────────────────

        private void StartBlink(ref AvatarBlinkComponent blink)
        {
            blink.IsBlinking = true;
            blink.FrameIndex = 0;
            blink.FrameTimer = 0f;
            ApplyEyeFrame(ref blink, BLINK_SEQUENCE[0]);
        }

        private void EndBlink(ref AvatarBlinkComponent blink)
        {
            blink.IsBlinking = false;
            blink.TimeSinceLastBlink = 0f;
            blink.NextBlinkTime = Random.Range(minBlinkInterval, maxBlinkInterval);

            // Restore the expression resting eye state (or clear to material default if no expression).
            ApplyEyeFrame(ref blink, blink.EyesExpressionIndex);
        }

        private void ApplyEyeFrame(ref AvatarBlinkComponent blink, int eyeIndex)
        {
            if (blink.CurrentEyeIndex == eyeIndex)
                return;

            blink.CurrentEyeIndex = eyeIndex;

            if (eyeIndex == NO_EYE_OVERRIDE)
            {
                // Revert to the renderer's default material texture (open eyes).
                blink.EyeRenderer.SetPropertyBlock(null);
                return;
            }

            s_Mpb.Clear();
            s_Mpb.SetTexture(MAINTEX_ARR_TEX_SHADER, eyeTextureArray);
            s_Mpb.SetInteger(MAINTEX_ARR_SHADER_INDEX, eyeIndex);
            blink.EyeRenderer.SetPropertyBlock(s_Mpb);
        }

        // ─── Mouth helpers ────────────────────────────────────────────────────

        private void StopMouthAnimation(ref AvatarMouthAnimationComponent mouth)
        {
            mouth.AnimatingText = null;

            // Restore the expression resting mouth state (or clear to material default if no expression).
            ApplyMouthPose(ref mouth, mouth.MouthExpressionIndex);
        }

        private void ApplyMouthPose(ref AvatarMouthAnimationComponent mouth, int mouthPoseIndex)
        {
            if (mouth.CurrentMouthPoseIndex == mouthPoseIndex)
                return;

            mouth.CurrentMouthPoseIndex = mouthPoseIndex;

            if (mouthPoseIndex == NO_MOUTH_POSE)
            {
                // Revert to the renderer's default material texture.
                mouth.MouthRenderer.SetPropertyBlock(null);
                return;
            }

            s_Mpb.Clear();
            s_Mpb.SetTexture(MAINTEX_ARR_TEX_SHADER, mouthPoseTextureArray);
            s_Mpb.SetInteger(MAINTEX_ARR_SHADER_INDEX, mouthPoseIndex);
            mouth.MouthRenderer.SetPropertyBlock(s_Mpb);
        }

        // ─── Eyebrows helpers ─────────────────────────────────────────────────

        private void ApplyEyebrowsFrame(ref AvatarFaceExpressionComponent expression, int eyebrowsIndex)
        {
            if (expression.CurrentEyebrowsIndex == eyebrowsIndex)
                return;

            expression.CurrentEyebrowsIndex = eyebrowsIndex;

            s_Mpb.Clear();
            s_Mpb.SetTexture(MAINTEX_ARR_TEX_SHADER, eyebrowsTextureArray);
            s_Mpb.SetInteger(MAINTEX_ARR_SHADER_INDEX, eyebrowsIndex);
            expression.EyebrowsRenderer.SetPropertyBlock(s_Mpb);
        }

        // ─── Mouth pose mapping ──────────────────────────────────────────────────

        /// <summary>
        ///     Maps a character at <paramref name="index"/> in <paramref name="text"/> to a mouth pose
        ///     slice index. Digraphs (th, ch, sh) are detected by peeking at the next character.
        /// </summary>
        private static int MapCharToMouthPose(string text, int index)
        {
            char c = char.ToLowerInvariant(text[index]);
            char next = index + 1 < text.Length ? char.ToLowerInvariant(text[index + 1]) : '\0';

            switch (c)
            {
                case 'a': case 'e': case 'i':  return 1;
                case 'b': case 'm': case 'p':  return 2;
                case 'f': case 'v':            return 3;
                case 't':                      return next == 'h' ? 4 : 6;
                case 'u':                      return 5;
                case 'd':                      return 4;
                case 'g': case 'h':
                case 'k': case 'n': case 'x':
                case 'y': case 'z':            return 6;
                case 'c':                      return next == 'h' ? 10 : 6;
                case 's':                      return next == 'h' ? 10 : 6;
                case 'o':                      return 7;
                case 'l':                      return 8;
                case 'r':                      return 9;
                case 'j':                      return 10;
                case 'w': case 'q':            return 11;
                default:                       return NO_MOUTH_POSE; // spaces, punctuation, digits — fall back to expression mouth
            }
        }

        // ─── Renderer lookup ──────────────────────────────────────────────────

        /// <summary>
        ///     Searches the avatar's instantiated wearable renderers for one whose name ends with <paramref name="suffix"/>.
        /// </summary>
        private static Renderer? FindRendererWithSuffix(ref AvatarShapeComponent avatarShape, string suffix)
        {
            for (var i = 0; i < avatarShape.InstantiatedWearables.Count; i++)
            {
                CachedAttachment wearable = avatarShape.InstantiatedWearables[i];

                for (var j = 0; j < wearable.Renderers.Count; j++)
                {
                    Renderer renderer = wearable.Renderers[j];

                    if (renderer.name.EndsWith(suffix))
                        return renderer;
                }
            }

            return null;
        }
    }
}
