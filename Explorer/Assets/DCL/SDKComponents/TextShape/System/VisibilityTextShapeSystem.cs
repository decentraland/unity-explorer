using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.SDKComponents.TextShape.Component;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Groups;
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
            UpdateVisibilityDependingOnSceneBoundariesQuery(World);
        }

        protected override void UpdateVisibilityInternal(in TextShapeComponent component, bool visible)
        {
            component.TextMeshPro.enabled = visible;
        }

        /// <summary>
        /// Enables or disables all TextMeshPro labels depending on whether they are fully inside their scenes or not.
        /// </summary>
        /// <param name="textShape">The text shape whose TextMeshPro will be modified.</param>
        [Query]
        [All(typeof(TextShapeComponent))]
        private void UpdateVisibilityDependingOnSceneBoundaries(ref TextShapeComponent textShape)
        {
            textShape.TextMeshPro.enabled = textShape.IsContainedInScene;
        }
    }
}
