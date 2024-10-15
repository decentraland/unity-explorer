using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Groups;
using ECS.Unity.Visibility;
using ECS.Unity.Visibility.Systems;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    public partial class VisibilityTextShapeSystem : VisibilitySystemBase<TextShapeComponent>
    {
        public VisibilityTextShapeSystem(World world, EntityEventBuffer<TextShapeComponent> changedTextMeshes) : base(world, changedTextMeshes) { }

        protected override void Update(float t)
        {
            base.Update(t);
            UpdateVisibilityDependingOnSceneBoundariesOnlyQuery(World);
            UpdateVisibilityDependingOnSceneBoundariesAndVisibilityComponentQuery(World);
        }

        protected override void UpdateVisibilityInternal(in TextShapeComponent component, bool visible)
        {
            component.TextMeshPro.enabled = visible && component.IsContainedInScene;
        }

        /// <summary>
        /// Enables or disables all TextMeshPro labels with Visibility component, depending on whether they are fully inside their scenes or not,
        /// and whether the Visibility component was originally visible or not.
        /// </summary>
        /// <param name="textShape">The text shape whose TextMeshPro will be modified.</param>
        /// <param name="visibilityComponent">The component that stores whether the text should be visible or not.</param>
        [Query]
        [All(typeof(TextShapeComponent), typeof(PBVisibilityComponent))]
        private void UpdateVisibilityDependingOnSceneBoundariesAndVisibilityComponent(ref TextShapeComponent textShape, PBVisibilityComponent visibilityComponent)
        {
            textShape.TextMeshPro.enabled = visibilityComponent.GetVisible() && textShape.IsContainedInScene;
        }

        /// <summary>
        /// Enables or disables all TextMeshPro labels without Visibility component, depending on whether they are fully inside their scenes or not.
        /// </summary>
        /// <param name="textShape">The text shape whose TextMeshPro will be modified.</param>
        [Query]
        [All(typeof(TextShapeComponent))]
        [None(typeof(PBVisibilityComponent))]
        private void UpdateVisibilityDependingOnSceneBoundariesOnly(ref TextShapeComponent textShape)
        {
            textShape.TextMeshPro.enabled = textShape.IsContainedInScene;
        }
    }
}
