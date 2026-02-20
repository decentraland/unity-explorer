using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Diagnostics;
using DCL.Interaction.Raycast.Components;
using DCL.Rendering.RenderGraphs.RenderFeatures.ObjectHighlight;
using ECS.Abstract;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    [UpdateInGroup(typeof(AvatarGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarHighlightSystem : BaseUnityLoopSystem
    {
        private readonly IAvatarHighlightData settings;

        internal AvatarHighlightSystem(World world, IAvatarHighlightData settings) : base(world)
        {
            this.settings = settings;
        }

        protected override void Update(float t)
        {
            HighlightAvatarQuery(World, t);
            RemoveHighlightAvatarQuery(World, t);
        }

        /// <summary>
        /// Fades in avatar outline when HoveredComponent is present.
        /// Applies the highlight to all renderers and smoothly increases opacity over FadeInTimeSeconds.
        /// </summary>
        [Query]
        [All(typeof(HoveredComponent))]
        private void HighlightAvatar([Data] float t, ref AvatarHighlightComponent highlight, ref AvatarShapeComponent avatarShape)
        {
            if (!Mathf.Approximately(highlight.Opacity, settings.OutlineVfxOpacity))
            {
                var step = settings.OutlineVfxOpacity / settings.FadeInTimeSeconds;
                var newValue = Mathf.MoveTowards(highlight.Opacity, settings.OutlineVfxOpacity, step * t);
                highlight.Opacity = newValue;
            }
            RenderFeature_ObjectHighlight.HighlightedObjects_Avatar.Highlight(avatarShape.OutlineCompatibleRenderers, BuildColor(highlight.Opacity), settings.OutlineThickness);
        }

        /// <summary>
        /// Fades out avatar outline when HoveredComponent is not present.
        /// Smoothly decreases opacity to 0 over FadeOutTimeSeconds.
        /// </summary>
        [Query]
        [None(typeof(HoveredComponent))]
        private void RemoveHighlightAvatar([Data] float t, ref AvatarHighlightComponent highlight, ref AvatarShapeComponent avatarShape)
        {
            if (highlight.Opacity <= 0)
                return;

            var step = settings.OutlineVfxOpacity / settings.FadeOutTimeSeconds;
            highlight.Opacity = Mathf.MoveTowards(highlight.Opacity, 0, step * t);
            RenderFeature_ObjectHighlight.HighlightedObjects_Avatar.Highlight(avatarShape.OutlineCompatibleRenderers, BuildColor(highlight.Opacity), settings.OutlineThickness);
        }

        private Color BuildColor(float opacity) =>
            new (settings.OutlineColor.r, settings.OutlineColor.g, settings.OutlineColor.b, opacity);
    }
}
