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
    ///     Drives all 2D facial animations for instantiated avatars: eye blinking and mouth phoneme animation.
    ///     This system is the single entry point for facial animation and is designed to be extended
    ///     with facial expressions and voice interpretation in the future.
    ///     Uses MaterialPropertyBlock overrides per-renderer to avoid modifying shared pool materials.
    /// </summary>
    /// <remarks>
    ///     Eye atlas layout (1024×1024, 4×4 grid of 256px cells, top-to-bottom):
    ///     Row 0: Idle(0),    HalfClosed(1), Closed(2), WideOpen(3)
    ///     Row 1: LookUp(4),  LookDown(5),   LookLeft(6), LookRight(7)
    ///     Row 2-3: Unused(8..15)
    ///
    ///     Phoneme atlas layout (1024×1024, 4×4 grid of 256px cells, top-to-bottom):
    ///     Row 0: Idle(0), a/e/i(1), b/m/p(2), f/v(3)
    ///     Row 1: d/th(4),  u(5),     c/g/h/k/n/s/t/x/y/z(6), o(7)
    ///     Row 2: l(8),   r(9),     ch/j/sh(10),               w/q(11)
    ///     Row 3: empty(12..15)
    /// </remarks>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class AvatarFacialAnimationSystem : BaseUnityLoopSystem
    {
        /// <summary>Sentinel value: no eye MaterialPropertyBlock override is active (material default = open eyes).</summary>
        private const int NO_EYE_OVERRIDE = -1;

        /// <summary>Sentinel value: no phoneme MaterialPropertyBlock override is active.</summary>
        private const int NO_PHONEME_OVERRIDE = -1;

        // Eye atlas slice indices.
        private const int EYE_HALF_CLOSED = 1;
        private const int EYE_CLOSED = 2;

        /// <summary>
        ///     Atlas slice sequence played during a blink: closing (HalfClosed → Closed) then opening (Closed → HalfClosed).
        ///     The final return to fully open clears the override, reverting to the material's default texture.
        /// </summary>
        private static readonly int[] BLINK_SEQUENCE = { EYE_HALF_CLOSED, EYE_CLOSED, EYE_HALF_CLOSED };

        private static readonly int MAINTEX_ARR_SHADER_INDEX = TextureArrayConstants.MAINTEX_ARR_SHADER_INDEX;
        private static readonly int MAINTEX_ARR_TEX_SHADER = TextureArrayConstants.MAINTEX_ARR_TEX_SHADER;

        // Reused every frame to avoid per-call allocation.
        private static readonly MaterialPropertyBlock s_Mpb = new MaterialPropertyBlock();

        private readonly Texture2DArray? eyeTextureArray;
        private readonly float minBlinkInterval;
        private readonly float maxBlinkInterval;
        private readonly float blinkFrameDuration;

        private readonly Texture2DArray? phonemeTextureArray;
        private readonly float phonemeDuration;

        internal AvatarFacialAnimationSystem(
            World world,
            Texture2DArray? eyeTextureArray,
            float minBlinkInterval,
            float maxBlinkInterval,
            float blinkFrameDuration,
            Texture2DArray? phonemeTextureArray,
            float phonemeDuration) : base(world)
        {
            this.eyeTextureArray = eyeTextureArray;
            this.minBlinkInterval = minBlinkInterval;
            this.maxBlinkInterval = maxBlinkInterval;
            this.blinkFrameDuration = blinkFrameDuration;
            this.phonemeTextureArray = phonemeTextureArray;
            this.phonemeDuration = phonemeDuration;
        }

        protected override void Update(float t)
        {
            if (eyeTextureArray != null)
            {
                SetupBlinkComponentQuery(World);
                UpdateBlinkQuery(World, t);
            }

            if (phonemeTextureArray != null)
            {
                SetupMouthComponentQuery(World);
                UpdateMouthAnimationQuery(World, t);
            }
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
                CurrentPhonemeIndex = NO_PHONEME_OVERRIDE,
            });
        }

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

        /// <summary>
        ///     Advances the phoneme animation for each avatar.
        ///     Re-initialises the renderer reference if the avatar was re-instantiated.
        ///     Reads AvatarMouthTalkingComponent to detect new chat messages.
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
                mouth.CurrentPhonemeIndex = NO_PHONEME_OVERRIDE;
            }

            // Suppress animation when the avatar or its mouth renderer is invisible.
            if (!avatarShape.IsVisible || !mouth.MouthRenderer.enabled)
            {
                StopMouthAnimation(ref mouth);
                return;
            }

            // Detect a new chat message. Getting a ref does not trigger an archetype move.
            if (World.Has<AvatarMouthTalkingComponent>(entity))
            {
                ref var talking = ref World.Get<AvatarMouthTalkingComponent>(entity);

                if (talking.IsDirty)
                {
                    mouth.AnimatingText = talking.Message;
                    mouth.CharacterIndex = 0;
                    mouth.CharacterTimer = 0f;
                    talking.IsDirty = false;
                }
            }

            // Advance the phoneme animation.
            if (mouth.AnimatingText != null && mouth.CharacterIndex < mouth.AnimatingText.Length)
            {
                int phoneme = MapCharToPhoneme(mouth.AnimatingText, mouth.CharacterIndex);
                ApplyPhoneme(ref mouth, phoneme);

                mouth.CharacterTimer += t;

                if (mouth.CharacterTimer >= phonemeDuration)
                {
                    mouth.CharacterTimer = 0f;
                    mouth.CharacterIndex++;
                }
            }
            else
            {
                StopMouthAnimation(ref mouth);
            }
        }

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

            // Clearing the property block reverts the renderer to its material's original values (open eyes).
            blink.CurrentEyeIndex = NO_EYE_OVERRIDE;
            blink.EyeRenderer.SetPropertyBlock(null);
        }

        private void ApplyEyeFrame(ref AvatarBlinkComponent blink, int eyeIndex)
        {
            if (blink.CurrentEyeIndex == eyeIndex)
                return;

            blink.CurrentEyeIndex = eyeIndex;

            // Use a MaterialPropertyBlock so only this renderer is overridden.
            // The underlying shared pool material is never modified, preventing
            // texture bleed-through onto mouth/eyebrow renderers.
            s_Mpb.Clear();
            s_Mpb.SetTexture(MAINTEX_ARR_TEX_SHADER, eyeTextureArray);
            s_Mpb.SetInteger(MAINTEX_ARR_SHADER_INDEX, eyeIndex);
            blink.EyeRenderer.SetPropertyBlock(s_Mpb);
        }

        private void StopMouthAnimation(ref AvatarMouthAnimationComponent mouth)
        {
            mouth.AnimatingText = null;
            ApplyPhoneme(ref mouth, NO_PHONEME_OVERRIDE);
        }

        private void ApplyPhoneme(ref AvatarMouthAnimationComponent mouth, int phonemeIndex)
        {
            if (mouth.CurrentPhonemeIndex == phonemeIndex)
                return;

            mouth.CurrentPhonemeIndex = phonemeIndex;

            if (phonemeIndex == NO_PHONEME_OVERRIDE)
            {
                // Revert to the renderer's default material texture.
                mouth.MouthRenderer.SetPropertyBlock(null);
                return;
            }

            s_Mpb.Clear();
            s_Mpb.SetTexture(MAINTEX_ARR_TEX_SHADER, phonemeTextureArray);
            s_Mpb.SetInteger(MAINTEX_ARR_SHADER_INDEX, phonemeIndex);
            mouth.MouthRenderer.SetPropertyBlock(s_Mpb);
        }

        /// <summary>
        ///     Maps a character at <paramref name="index"/> in <paramref name="text"/> to a phoneme
        ///     slice index. Digraphs (th, ch, sh) are detected by peeking at the next character.
        /// </summary>
        private static int MapCharToPhoneme(string text, int index)
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
                default:                       return 0; // Idle: spaces, punctuation, digits, etc.
            }
        }

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
