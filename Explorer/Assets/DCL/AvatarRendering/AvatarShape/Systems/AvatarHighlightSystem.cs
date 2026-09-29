using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.CharacterPreview.Components;
using DCL.Diagnostics;
using DCL.Interaction.Raycast.Components;
using DCL.Rendering.RenderGraphs.RenderFeatures.ObjectHighlight;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    [UpdateInGroup(typeof(AvatarGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarHighlightSystem : BaseUnityLoopSystem
    {
        private readonly ReadOnlyAvatarHighlightData settings;

        internal AvatarHighlightSystem(World world, ReadOnlyAvatarHighlightData settings) : base(world)
        {
            this.settings = settings;
        }

        protected override void Update(float t)
        {
            HighlightAvatarQuery(World, t);
            RemoveHighlightAvatarQuery(World, t);
            HighlightPreviewQuery(World, t);
            ClearHighlightQuery(World);
        }

        /// <summary>
        /// Fades in avatar outline when HoveredComponent is present.
        /// Applies the highlight to all renderers and smoothly increases opacity over FadeInTimeSeconds.
        /// </summary>
        [Query]
        [All(typeof(HoveredComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void HighlightAvatar([Data] float t, ref AvatarHighlightComponent highlight, in AvatarShapeComponent avatarShape) =>
            FadeIn(t, ref highlight, in avatarShape);

        /// <summary>
        /// Fades out avatar outline when HoveredComponent is not present.
        /// Smoothly decreases opacity to 0 over FadeOutTimeSeconds.
        /// </summary>
        [Query]
        [None(typeof(HoveredComponent), typeof(CharacterPreviewComponent), typeof(DeleteEntityIntention))]
        private void RemoveHighlightAvatar([Data] float t, ref AvatarHighlightComponent highlight, in AvatarShapeComponent avatarShape) =>
            FadeOut(t, ref highlight, in avatarShape);

        // A preview is never raycast, so its hover comes from the UI hosting it through the component flag
        [Query]
        [None(typeof(HoveredComponent), typeof(DeleteEntityIntention))]
        private void HighlightPreview([Data] float t, in CharacterPreviewComponent preview, ref AvatarHighlightComponent highlight, in AvatarShapeComponent avatarShape)
        {
            if (preview.IsHovered)
                FadeIn(t, ref highlight, in avatarShape);
            else
                FadeOut(t, ref highlight, in avatarShape);
        }

        // The render feature keeps outlining the renderers, so an avatar released mid-fade would outline whichever avatar reuses them
        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void ClearHighlight(ref AvatarHighlightComponent highlight, in AvatarShapeComponent avatarShape)
        {
            if (highlight.Opacity <= 0)
                return;

            highlight.Opacity = 0;
            RenderFeature_ObjectHighlight.HighlightedObjects_Avatar.Disparage(avatarShape.OutlineCompatibleRenderers);
        }

        private void FadeIn(float t, ref AvatarHighlightComponent highlight, in AvatarShapeComponent avatarShape)
        {
            if (!Mathf.Approximately(highlight.Opacity, settings.OutlineVfxOpacity))
            {
                float step = settings.OutlineVfxOpacity / settings.FadeInTimeSeconds;
                highlight.Opacity = Mathf.MoveTowards(highlight.Opacity, settings.OutlineVfxOpacity, step * t);
            }

            RenderFeature_ObjectHighlight.HighlightedObjects_Avatar.Highlight(avatarShape.OutlineCompatibleRenderers, BuildColor(highlight.Opacity), settings.OutlineThickness);
        }

        private void FadeOut(float t, ref AvatarHighlightComponent highlight, in AvatarShapeComponent avatarShape)
        {
            if (highlight.Opacity <= 0)
                return;

            float step = settings.OutlineVfxOpacity / settings.FadeOutTimeSeconds;
            highlight.Opacity = Mathf.MoveTowards(highlight.Opacity, 0, step * t);
            RenderFeature_ObjectHighlight.HighlightedObjects_Avatar.Highlight(avatarShape.OutlineCompatibleRenderers, BuildColor(highlight.Opacity), settings.OutlineThickness);
        }

        private Color BuildColor(float opacity) =>
            new (settings.OutlineColor.r, settings.OutlineColor.g, settings.OutlineColor.b, opacity);
    }
}
