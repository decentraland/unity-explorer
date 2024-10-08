using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.SDKComponents.TextShape.Component;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Groups;
using ECS.Unity.Visibility.Systems;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    public partial class VisibilityTextShapeSystem : VisibilitySystemBase<TextShapeComponent>
    {
        private World world;
        private ParcelMathHelper.SceneGeometry sceneGeometry;

        public VisibilityTextShapeSystem(World world, EntityEventBuffer<TextShapeComponent> changedTextMeshes, ISceneData sceneData) : base(world, changedTextMeshes)
        {
            this.world = world;
            this.sceneGeometry = sceneData.Geometry;
        }

        protected override void Update(float t)
        {
            base.Update(t);
            UpdateVisibilityDependingOnSceneQuery(world);
        }

        protected override void UpdateVisibilityInternal(in TextShapeComponent component, bool visible)
        {
            component.TextMeshPro.enabled = visible;
        }

        /// <summary>
        /// Enables or disables all TextMeshPro labels depending on whether they belong to the current scene or not.
        /// </summary>
        /// <param name="textShape">The text shape whose TextMeshPro will be modified.</param>
        [Query]
        [All(typeof(TextShapeComponent))]
        private void UpdateVisibilityDependingOnScene(ref TextShapeComponent textShape)
        {
            Vector3 textPosition = textShape.TextMeshPro.transform.position;
            bool textVisibility = textPosition.y <= sceneGeometry.Height &&
                                  sceneGeometry.CircumscribedPlanes.Intersects(textPosition);

            if (textShape.TextMeshPro.gameObject.activeInHierarchy != textVisibility)
            {
                textShape.TextMeshPro.gameObject.SetActive(textVisibility);
            }
        }
    }
}
