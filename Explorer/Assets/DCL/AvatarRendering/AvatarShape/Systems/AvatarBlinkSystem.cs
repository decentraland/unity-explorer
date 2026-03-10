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
    ///     Drives the eye-blink animation for all instantiated avatars.
    ///     Each avatar blinks independently at a random interval within the configured range.
    ///     Blinking is suppressed when the avatar (or its eye renderer) is not visible.
    ///     Uses a MaterialPropertyBlock to override eye texture per-renderer without touching
    ///     the shared pool material, preventing texture corruption on mouth/eyebrow renderers.
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class AvatarBlinkSystem : BaseUnityLoopSystem
    {
        private static readonly int MAINTEX_ARR_SHADER_INDEX = TextureArrayConstants.MAINTEX_ARR_SHADER_INDEX;
        private static readonly int MAINTEX_ARR_TEX_SHADER = TextureArrayConstants.MAINTEX_ARR_TEX_SHADER;

        // Reused every frame to avoid per-blink allocation.
        private static readonly MaterialPropertyBlock s_Mpb = new MaterialPropertyBlock();

        private readonly Texture2DArray blinkTextureArray;
        private readonly float minBlinkInterval;
        private readonly float maxBlinkInterval;
        private readonly float blinkDuration;

        internal AvatarBlinkSystem(
            World world,
            Texture2DArray blinkTextureArray,
            float minBlinkInterval,
            float maxBlinkInterval,
            float blinkDuration) : base(world)
        {
            this.blinkTextureArray = blinkTextureArray;
            this.minBlinkInterval = minBlinkInterval;
            this.maxBlinkInterval = maxBlinkInterval;
            this.blinkDuration = blinkDuration;
        }

        protected override void Update(float t)
        {
            SetupBlinkComponentQuery(World);
            UpdateBlinkQuery(World, t);
        }

        /// <summary>
        ///     Adds AvatarBlinkComponent to newly instantiated avatars that do not yet have one.
        /// </summary>
        [Query]
        [All(typeof(AvatarCustomSkinningComponent))]
        [None(typeof(AvatarBlinkComponent), typeof(DeleteEntityIntention))]
        private void SetupBlinkComponent(in Entity entity, ref AvatarShapeComponent avatarShape)
        {
            Renderer? eyeRenderer = FindEyeRenderer(ref avatarShape);

            if (eyeRenderer == null)
                return;

            var blinkComponent = new AvatarBlinkComponent
            {
                EyeRenderer = eyeRenderer,
                NextBlinkTime = Random.Range(minBlinkInterval, maxBlinkInterval),
            };

            World.Add(entity, blinkComponent);
        }

        /// <summary>
        ///     Updates the blink timer and swaps the eye texture when a blink is due.
        ///     Also handles re-initialisation when the eye renderer has been replaced
        ///     (e.g. after a wearable change triggers avatar re-instantiation).
        /// </summary>
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateBlink([Data] float t, ref AvatarBlinkComponent blinkComponent, ref AvatarShapeComponent avatarShape)
        {
            // Re-initialise when the renderer was destroyed by a re-instantiation.
            if (blinkComponent.EyeRenderer == null)
            {
                Renderer? eyeRenderer = FindEyeRenderer(ref avatarShape);

                if (eyeRenderer == null)
                    return;

                blinkComponent.EyeRenderer = eyeRenderer;
                blinkComponent.IsBlinking = false;
                blinkComponent.BlinkTimer = 0f;
                blinkComponent.TimeSinceLastBlink = 0f;
                blinkComponent.NextBlinkTime = Random.Range(minBlinkInterval, maxBlinkInterval);
            }

            // Suppress blinking when the avatar or its eye renderer is invisible.
            if (!avatarShape.IsVisible || !blinkComponent.EyeRenderer.enabled)
            {
                if (blinkComponent.IsBlinking)
                    EndBlink(ref blinkComponent);

                return;
            }

            if (blinkComponent.IsBlinking)
            {
                blinkComponent.BlinkTimer += t;

                if (blinkComponent.BlinkTimer >= blinkDuration)
                    EndBlink(ref blinkComponent);
            }
            else
            {
                blinkComponent.TimeSinceLastBlink += t;

                if (blinkComponent.TimeSinceLastBlink >= blinkComponent.NextBlinkTime)
                    StartBlink(ref blinkComponent);
            }
        }

        private void StartBlink(ref AvatarBlinkComponent blinkComponent)
        {
            blinkComponent.IsBlinking = true;
            blinkComponent.BlinkTimer = 0f;

            // Use a MaterialPropertyBlock so only this renderer is overridden.
            // The underlying shared pool material is never modified, preventing
            // texture bleed-through onto mouth/eyebrow renderers.
            s_Mpb.Clear();
            s_Mpb.SetTexture(MAINTEX_ARR_TEX_SHADER, blinkTextureArray);
            s_Mpb.SetInteger(MAINTEX_ARR_SHADER_INDEX, 0);
            blinkComponent.EyeRenderer.SetPropertyBlock(s_Mpb);
        }

        private void EndBlink(ref AvatarBlinkComponent blinkComponent)
        {
            blinkComponent.IsBlinking = false;
            blinkComponent.TimeSinceLastBlink = 0f;
            blinkComponent.NextBlinkTime = Random.Range(minBlinkInterval, maxBlinkInterval);

            // Clearing the property block reverts the renderer to its material's original values.
            blinkComponent.EyeRenderer.SetPropertyBlock(null);
        }

        private static Renderer? FindEyeRenderer(ref AvatarShapeComponent avatarShape)
        {
            for (var i = 0; i < avatarShape.InstantiatedWearables.Count; i++)
            {
                CachedAttachment wearable = avatarShape.InstantiatedWearables[i];

                for (var j = 0; j < wearable.Renderers.Count; j++)
                {
                    Renderer renderer = wearable.Renderers[j];

                    if (renderer.name.EndsWith("Mask_Eyes"))
                        return renderer;
                }
            }

            return null;
        }
    }
}
