using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Visibility;
using ECS.Unity.Visibility.Components;
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
            UpdateVisibilityDependingOnSceneBoundariesAndResolvedVisibilityQuery(World);
            UpdateVisibilityDependingOnSceneBoundariesOnlyQuery(World);
            UpdateVisibilityDependingOnSceneBoundariesAndVisibilityComponentQuery(World);
        }

        protected override void UpdateVisibilityInternal(in TextShapeComponent component, bool visible)
        {
            component.TextMeshPro.enabled = visible && component.IsContainedInScene;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateVisibilityDependingOnSceneBoundariesAndResolvedVisibility(in TextShapeComponent textShape, in ResolvedVisibilityComponent resolved)
        {
            UpdateVisibilityInternal(in textShape, resolved.IsVisible);
        }

        [Query]
        [None(typeof(ResolvedVisibilityComponent), typeof(DeleteEntityIntention))]
        private void UpdateVisibilityDependingOnSceneBoundariesAndVisibilityComponent(ref TextShapeComponent textShape, PBVisibilityComponent visibilityComponent)
        {
            textShape.TextMeshPro.enabled = visibilityComponent.GetVisible() && textShape.IsContainedInScene;
        }

        /// <summary>
        /// Enables or disables all TextMeshPro labels without Visibility component, depending on whether they are fully inside their scenes or not.
        /// </summary>
        /// <param name="textShape">The text shape whose TextMeshPro will be modified.</param>
        [Query]
        [None(typeof(PBVisibilityComponent), typeof(ResolvedVisibilityComponent), typeof(DeleteEntityIntention))]
        private void UpdateVisibilityDependingOnSceneBoundariesOnly(ref TextShapeComponent textShape)
        {
            textShape.TextMeshPro.enabled = textShape.IsContainedInScene;
        }
    }
}
