using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.Diagnostics;
using DCL.Interaction.Settings;
using DCL.Rendering.RenderGraphs.RenderFeatures.ObjectHighlight;
using ECS.Abstract;
using ECS.Groups;
using UnityEngine;

namespace DCL.Interaction.Systems
{
    /// <summary>
    ///     It controls the outline VFX that appears around avatars.
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class AvatarHighlightSystem : BaseUnityLoopSystem
    {
        private readonly InteractionSettingsData settings;

        public AvatarHighlightSystem(World world, InteractionSettingsData settings) : base(world)
        {
            this.settings = settings;
        }

        protected override void Update(float t)
        {
            if (!settings.EnableAvatarOutline)
                return;

            UpdateAvatarHighlightVanishingQuery(World, t);
            UpdateAvatarHighlightShowingQuery(World, t);
            UpdateAvatarHighlightBlinkingAnimationQuery(World); // This animation overrides the normal fx params
            UpdateAvatarHighlightQuery(World);
        }

        [Query]
        [None(typeof(ShowAvatarHighlightIntent), typeof(PlayAvatarHighlightBlinkingAnimationIntent))]
        private void UpdateAvatarHighlightVanishing([Data] float t, ref AvatarShapeComponent avatarShapeComponent)
        {
            if (avatarShapeComponent.OutlineVfxOpacity <= 0.0f)
                return;

            // While there is no intent, the opacity decreases
            avatarShapeComponent.OutlineVfxOpacity = Mathf.Clamp01(avatarShapeComponent.OutlineVfxOpacity - t * settings.AvatarOutlineFadingSpeed);
        }

        [Query]
        private void UpdateAvatarHighlightShowing([Data] float t, Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref ShowAvatarHighlightIntent animationIntent)
        {
            // Sets the params and increases the opacity
            avatarShapeComponent.OutlineColor = animationIntent.CanInteract ? settings.InteractableAvatarOutlineColor : settings.NonInteractableAvatarOutlineColor;
            avatarShapeComponent.OutlineThickness = settings.AvatarOutlineThickness;
            avatarShapeComponent.OutlineVfxOpacity = Mathf.Clamp01(avatarShapeComponent.OutlineVfxOpacity + t * settings.AvatarOutlineFadingSpeed);

            World.Remove<ShowAvatarHighlightIntent>(entity);
        }

        [Query]
        private void UpdateAvatarHighlightBlinkingAnimation(Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref PlayAvatarHighlightBlinkingAnimationIntent animationIntent)
        {
            animationIntent.Progress = (UnityEngine.Time.time - animationIntent.StartTime) / animationIntent.Duration;
            float loopLength = 1.0f / animationIntent.LoopCount;
            float progressInIteration = animationIntent.Progress % loopLength / loopLength;
            float alpha = progressInIteration < 0.5f ? progressInIteration * 2.0f : 1.0f - (progressInIteration - 0.5f) * 2.0f;

            avatarShapeComponent.OutlineVfxOpacity = Mathf.Clamp01(alpha);
            avatarShapeComponent.OutlineColor = animationIntent.OutlineColor;
            avatarShapeComponent.OutlineThickness = animationIntent.Thickness;

            if (animationIntent.Progress >= 1.0f)
                World.Remove<PlayAvatarHighlightBlinkingAnimationIntent>(entity);
        }

        [Query]
        private void UpdateAvatarHighlight(ref AvatarShapeComponent avatarShapeComponent)
        {
            float previousOpacity = avatarShapeComponent.PreviousOutlineVfxOpacity;
            avatarShapeComponent.PreviousOutlineVfxOpacity = avatarShapeComponent.OutlineVfxOpacity;

            if (avatarShapeComponent.OutlineVfxOpacity == previousOpacity && avatarShapeComponent.OutlineVfxOpacity == 0.0f)
                return;

            var color = avatarShapeComponent.OutlineColor;
            color.a *= avatarShapeComponent.OutlineVfxOpacity;

            // Just applies the effect to renderers of avatars
            foreach (var rend in avatarShapeComponent.OutlineCompatibleRenderers)
                if (rend.gameObject.activeSelf && rend.enabled && rend.sharedMaterial.renderQueue >= 2000 && rend.sharedMaterial.renderQueue < 3000)
                    RenderFeature_ObjectHighlight.HighlightedObjects_Avatar.Highlight(rend, color, avatarShapeComponent.OutlineThickness);
        }
    }
}