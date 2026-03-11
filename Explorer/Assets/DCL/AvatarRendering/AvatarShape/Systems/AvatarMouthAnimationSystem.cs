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
    ///     Drives mouth phoneme animation for all instantiated avatars.
    ///     When an AvatarMouthTalkingComponent is present and dirty, cycles through phoneme
    ///     frames matching each character of the message for a configurable duration.
    ///     Uses a MaterialPropertyBlock to select the phoneme slice from a Texture2DArray
    ///     built from the mouth atlas, without touching the shared pool material.
    /// </summary>
    /// <remarks>
    ///     Phoneme atlas layout (1024×1024, 4×4 grid of 256px cells, top-to-bottom):
    ///     Row 0: Idle(0), a/e/i(1), b/m/p(2), f/v(3)
    ///     Row 1: d/th(4),  u(5),     c/g/h/k/n/s/t/x/y/z(6), o(7)
    ///     Row 2: l(8),   r(9),     ch/j/sh(10),               w/q(11)
    ///     Row 3: empty(12..15)
    /// </remarks>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class AvatarMouthAnimationSystem : BaseUnityLoopSystem
    {
        private static readonly int MAINTEX_ARR_SHADER_INDEX = TextureArrayConstants.MAINTEX_ARR_SHADER_INDEX;
        private static readonly int MAINTEX_ARR_TEX_SHADER = TextureArrayConstants.MAINTEX_ARR_TEX_SHADER;

        /// <summary>Sentinel value: no MaterialPropertyBlock override is active.</summary>
        private const int NO_OVERRIDE = -1;

        // Reused every frame to avoid per-call allocation.
        private static readonly MaterialPropertyBlock s_Mpb = new MaterialPropertyBlock();

        private readonly Texture2DArray phonemeTextureArray;
        private readonly float phonemeDuration;

        internal AvatarMouthAnimationSystem(
            World world,
            Texture2DArray phonemeTextureArray,
            float phonemeDuration) : base(world)
        {
            this.phonemeTextureArray = phonemeTextureArray;
            this.phonemeDuration = phonemeDuration;
        }

        protected override void Update(float t)
        {
            SetupMouthComponentQuery(World);
            UpdateMouthAnimationQuery(World, t);
        }

        /// <summary>
        ///     Adds AvatarMouthAnimationComponent to fully instantiated avatars that do not yet have one.
        /// </summary>
        [Query]
        [All(typeof(AvatarCustomSkinningComponent))]
        [None(typeof(AvatarMouthAnimationComponent), typeof(DeleteEntityIntention))]
        private void SetupMouthComponent(in Entity entity, ref AvatarShapeComponent avatarShape)
        {
            Renderer? mouthRenderer = FindMouthRenderer(ref avatarShape);

            if (mouthRenderer == null)
                return;

            World.Add(entity, new AvatarMouthAnimationComponent
            {
                MouthRenderer = mouthRenderer,
                CurrentPhonemeIndex = NO_OVERRIDE,
            });
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
                Renderer? mouthRenderer = FindMouthRenderer(ref avatarShape);

                if (mouthRenderer == null)
                    return;

                mouth.MouthRenderer = mouthRenderer;
                mouth.AnimatingText = null;
                mouth.CharacterIndex = 0;
                mouth.CharacterTimer = 0f;
                mouth.CurrentPhonemeIndex = NO_OVERRIDE;
            }

            // Suppress animation when the avatar or its mouth renderer is invisible.
            if (!avatarShape.IsVisible || !mouth.MouthRenderer.enabled)
            {
                StopAnimation(ref mouth);
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
                StopAnimation(ref mouth);
            }
        }

        private void StopAnimation(ref AvatarMouthAnimationComponent mouth)
        {
            mouth.AnimatingText = null;
            ApplyPhoneme(ref mouth, NO_OVERRIDE);
        }

        private void ApplyPhoneme(ref AvatarMouthAnimationComponent mouth, int phonemeIndex)
        {
            if (mouth.CurrentPhonemeIndex == phonemeIndex)
                return;

            mouth.CurrentPhonemeIndex = phonemeIndex;

            if (phonemeIndex == NO_OVERRIDE)
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

        private static Renderer? FindMouthRenderer(ref AvatarShapeComponent avatarShape)
        {
            for (var i = 0; i < avatarShape.InstantiatedWearables.Count; i++)
            {
                CachedAttachment wearable = avatarShape.InstantiatedWearables[i];

                for (var j = 0; j < wearable.Renderers.Count; j++)
                {
                    Renderer renderer = wearable.Renderers[j];

                    if (renderer.name.EndsWith("Mask_Mouth"))
                        return renderer;
                }
            }

            return null;
        }
    }
}
