using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts;
using ECS.Abstract;
using ECS.Unity.Groups;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(InstantiateTextShapeSystem))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    public partial class UpdateTextShapeSystem : BaseUnityLoopSystem
    {
        private readonly IFontsStorage fontsStorage;
        private readonly MaterialPropertyBlock materialPropertyBlock;
        private readonly ParcelMathHelper.SceneGeometry sceneGeometry;

        private readonly EntityEventBuffer<TextShapeComponent> changedTextMeshes;

        public UpdateTextShapeSystem(World world, IFontsStorage fontsStorage, MaterialPropertyBlock materialPropertyBlock,
            EntityEventBuffer<TextShapeComponent> changedTextMeshes, ISceneData sceneData) : base(world)
        {
            this.fontsStorage = fontsStorage;
            this.materialPropertyBlock = materialPropertyBlock;
            this.changedTextMeshes = changedTextMeshes;
            this.sceneGeometry = sceneData.Geometry;
        }

        protected override void Update(float t)
        {
            UpdateTextsQuery(World!);
            // Note: It must occur after UpdateTextsQuery in order to properly calculate the bounds of the text with the latest state,
            // and the incoming value of IsDirty flag of the PBTextShape must be available, that's why it is reset in a separate
            // query as a final step
            CalculateIfTextShapesAreInsideSceneBoundariesQuery(World);
            ResetDirtyFlagQuery(World);
        }

        [Query]
        [All(typeof(TextShapeComponent), typeof(PBTextShape))]
        private void UpdateTexts(Entity entity, ref TextShapeComponent textShapeComponent, in PBTextShape textShape)
        {
            if (textShape.IsDirty)
            {
                textShapeComponent.TextMeshPro.Apply(textShape, fontsStorage, materialPropertyBlock);
                changedTextMeshes.Add(entity, textShapeComponent);
            }
        }

        [Query]
        [All(typeof(PBTextShape))]
        private void ResetDirtyFlag(PBTextShape textShape)
        {
            textShape.IsDirty = false;
        }

        /// <summary>
        /// Calculates whether the TextMeshPro labels are inside their scenes or not, according to the bounding box of the
        /// label and the boundaries of the scene. It stores the result in the TextShapeComponent.
        /// This is checked when the transformations of the label change and when the text of the label changes.
        /// </summary>
        /// <param name="textShapeComponent">The text shape that contains the TextMeshPro to check.</param>
        /// <param name="pbTextShape">The latest state of the text shape in the scene.</param>
        [Query]
        [All(typeof(TextShapeComponent), typeof(PBTextShape))]
        private void CalculateIfTextShapesAreInsideSceneBoundaries(ref TextShapeComponent textShapeComponent, PBTextShape pbTextShape)
        {
            if (!textShapeComponent.TextMeshPro.transform.hasChanged && !pbTextShape.IsDirty)
                return;

            // Resets the transform changed flag
            textShapeComponent.TextMeshPro.transform.hasChanged = false;

            // It has to be immediately rebuilt after its text changes, otherwise it will be updated after this frame and the bounding box will be obsolete
            if (pbTextShape.IsDirty)
            {
                textShapeComponent.TextMeshPro.enabled = true; // It must be enabled, otherwise the bounds will be invalid
                textShapeComponent.TextMeshPro.ForceMeshUpdate(true, true);
            }

            Bounds textWorldBounds = textShapeComponent.TextMeshPro.renderer.bounds; // Note: Using Renderer because the bounds of the TMP does not return what we need

            // When the TMP is disabled, the size of its bounds equals zero, so we need to use the latest valid size it had instead
            if (!textShapeComponent.TextMeshPro.enabled)
                textWorldBounds.size = textShapeComponent.LastValidBoundingBoxSize;

            textShapeComponent.IsContainedInScene = textShapeComponent.TextMeshPro.transform.position.y <= sceneGeometry.Height &&
                                                    sceneGeometry.CircumscribedPlanes.Contains(textWorldBounds);

            // Stores the size of the current bounding box of the TMP while it is enabled
            if (textShapeComponent.TextMeshPro.enabled)
                textShapeComponent.LastValidBoundingBoxSize = textWorldBounds.size;
        }
    }
}
