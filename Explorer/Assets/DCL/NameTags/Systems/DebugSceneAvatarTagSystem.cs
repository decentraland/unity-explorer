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
    ///     until the PBNametag SDK component provides the production data path.
    ///     Widget fields: tag text, text color (html string), background color (html string).
    /// </summary>
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [UpdateBefore(typeof(NametagPlacementSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class DebugSceneAvatarTagSystem : BaseUnityLoopSystem
    {
        private static readonly Color DEFAULT_BACKGROUND_COLOR = new (0.086f, 0.082f, 0.102f);

        private string pendingText = string.Empty;
        private Color pendingTextColor = Color.white;
        private Color pendingBackgroundColor = DEFAULT_BACKGROUND_COLOR;
        private bool applyRequested;
        private bool removeRequested;

        internal DebugSceneAvatarTagSystem(World world, IDebugContainerBuilder debugContainerBuilder) : base(world)
        {
            debugContainerBuilder.TryAddWidget(IDebugContainerBuilder.Categories.NAMETAGS)
                                ?.AddStringFieldsWithConfirmation(3, "Apply scene tag", OnApplyRequested)
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
                pendingTextColor = Color.white;

            if (string.IsNullOrEmpty(fields[2]) || !ColorUtility.TryParseHtmlString(fields[2], out pendingBackgroundColor))
                pendingBackgroundColor = DEFAULT_BACKGROUND_COLOR;

            applyRequested = true;
        }

        [Query]
        [None(typeof(SceneAvatarTagComponent), typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void AddSceneAvatarTag(Entity e) =>
            World.Add(e, new SceneAvatarTagComponent(pendingText, pendingTextColor, pendingBackgroundColor));

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void UpdateSceneAvatarTag(ref SceneAvatarTagComponent sceneTag) =>
            sceneTag = new SceneAvatarTagComponent(pendingText, pendingTextColor, pendingBackgroundColor);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void MarkSceneAvatarTagRemoving(ref SceneAvatarTagComponent sceneTag) =>
            sceneTag.IsRemoving = true;
    }
}
