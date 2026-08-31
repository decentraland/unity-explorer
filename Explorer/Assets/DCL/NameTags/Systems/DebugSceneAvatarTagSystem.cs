using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;

namespace DCL.Nametags
{
    /// <summary>
    ///     Drives <see cref="SceneAvatarTagComponent" /> from the "Nametags" debug widget
    ///     alongside the PBAvatarNametag SDK path, to exercise the plate without a scene.
    ///     Widget fields: tag text, text color, background color, border color (colors are html strings).
    /// </summary>
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [UpdateBefore(typeof(NametagPlacementSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class DebugSceneAvatarTagSystem : BaseUnityLoopSystem
    {
        private string pendingText = string.Empty;
        private Color pendingTextColor = SceneAvatarTagComponent.NATIVE_TEXT_COLOR;
        private Color pendingBackgroundColor = SceneAvatarTagComponent.NATIVE_BACKGROUND_COLOR;
        private Color pendingBorderColor = SceneAvatarTagComponent.NATIVE_BACKGROUND_COLOR;
        private bool applyRequested;
        private bool removeRequested;

        internal DebugSceneAvatarTagSystem(World world, IDebugContainerBuilder debugContainerBuilder) : base(world)
        {
            debugContainerBuilder.TryAddWidget(IDebugContainerBuilder.Categories.NAMETAGS)
                                ?.AddStringFieldsWithConfirmation(4, "Apply scene tag", OnApplyRequested)
                                 .AddSingleButton("Remove scene tags", () => removeRequested = true);
        }

        protected override void Update(float t)
        {
            if (applyRequested)
            {
                applyRequested = false;
                UpdateSceneAvatarTagQuery(World);
                AddSceneAvatarTagQuery(World);
            }

            if (removeRequested)
            {
                removeRequested = false;
                MarkSceneAvatarTagRemovingQuery(World);
            }
        }

        private void OnApplyRequested(string[] fields)
        {
            pendingText = fields[0] ?? string.Empty;

            if (string.IsNullOrEmpty(fields[1]) || !ColorUtility.TryParseHtmlString(fields[1], out pendingTextColor))
                pendingTextColor = SceneAvatarTagComponent.NATIVE_TEXT_COLOR;

            if (string.IsNullOrEmpty(fields[2]) || !ColorUtility.TryParseHtmlString(fields[2], out pendingBackgroundColor))
                pendingBackgroundColor = SceneAvatarTagComponent.NATIVE_BACKGROUND_COLOR;

            // Matches the SDK path: an unspecified border takes the background color.
            if (string.IsNullOrEmpty(fields[3]) || !ColorUtility.TryParseHtmlString(fields[3], out pendingBorderColor))
                pendingBorderColor = pendingBackgroundColor;

            applyRequested = true;
        }

        [Query]
        [None(typeof(SceneAvatarTagComponent), typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void AddSceneAvatarTag(Entity e) =>
            World.Add(e, new SceneAvatarTagComponent(pendingText, pendingTextColor, pendingBackgroundColor, pendingBorderColor));

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void UpdateSceneAvatarTag(ref SceneAvatarTagComponent sceneTag) =>
            sceneTag = new SceneAvatarTagComponent(pendingText, pendingTextColor, pendingBackgroundColor, pendingBorderColor);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void MarkSceneAvatarTagRemoving(ref SceneAvatarTagComponent sceneTag) =>
            sceneTag.IsRemoving = true;
    }
}
