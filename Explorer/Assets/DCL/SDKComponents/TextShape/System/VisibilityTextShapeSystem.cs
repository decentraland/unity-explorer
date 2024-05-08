using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Visibility;
using System.Runtime.CompilerServices;
using TMPro;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateBefore(typeof(UpdateTextShapeSystem))] // Update text Shape resets dirty to false
    public partial class VisibilityTextShapeSystem : BaseUnityLoopSystem
    {
        private readonly EntityEventBuffer<TextMeshPro> changedTextMeshes;
        private readonly EntityEventBuffer<TextMeshPro>.ForEachDelegate forEachUpdatedText;

        public VisibilityTextShapeSystem(World world, EntityEventBuffer<TextMeshPro> changedTextMeshes) : base(world)
        {
            this.changedTextMeshes = changedTextMeshes;
            forEachUpdatedText = ProcessUpdatedText;
        }

        protected override void Update(float t)
        {
            UpdateVisibilityQuery(World!);
            changedTextMeshes.ForEach(forEachUpdatedText);
        }

        private void ProcessUpdatedText(Entity entity, TextMeshPro @event)
        {
            if (World.TryGet(entity, out PBVisibilityComponent? visibilityComponent))
                UpdateVisibility(@event, visibilityComponent!);
        }

        /// <summary>
        ///     Updates visibility based on PBVisibilityComponent isDirty
        /// </summary>
        [Query]
        private void UpdateVisibility(in TextShapeComponent textComponent, in PBVisibilityComponent visibility)
        {
            if (visibility.IsDirty)
                UpdateVisibility(textComponent.TextMeshPro, visibility);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateVisibility(TextMeshPro textMeshPro, in PBVisibilityComponent visibility)
        {
            textMeshPro.enabled = visibility.GetVisible();
        }
    }
}
