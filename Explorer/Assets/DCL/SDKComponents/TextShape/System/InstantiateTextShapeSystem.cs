using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using TMPro;
using UnityEngine;


namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    public partial class InstantiateTextShapeSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly IComponentPool<TextMeshPro> textMeshProPool;
        private readonly IFontsStorage fontsStorage;
        private readonly MaterialPropertyBlock materialPropertyBlock;
        private readonly ISceneData sceneData;
        private readonly IPartitionComponent scenePartition;

        private readonly EntityEventBuffer<TextShapeComponent> changedTextMeshes;

        public InstantiateTextShapeSystem(World world, IComponentPool<TextMeshPro> textMeshProPool, IFontsStorage fontsStorage, MaterialPropertyBlock materialPropertyBlock, IPerformanceBudget instantiationFrameTimeBudget,
            EntityEventBuffer<TextShapeComponent> changedTextMeshes, ISceneData sceneData, IPartitionComponent scenePartition) : base(world)
        {
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.changedTextMeshes = changedTextMeshes;
            this.textMeshProPool = textMeshProPool;
            this.fontsStorage = fontsStorage;
            this.materialPropertyBlock = materialPropertyBlock;
            this.sceneData = sceneData;
            this.scenePartition = scenePartition;
        }

        protected override void Update(float t)
        {
            InstantiateRemainingQuery(World!);
        }

        [Query]
        [None(typeof(TextShapeComponent), typeof(DeleteEntityIntention))]
        private void InstantiateRemaining(Entity entity, in TransformComponent transform, in PBTextShape textShape)
        {
            if (instantiationFrameTimeBudget.TrySpendBudget() == false)
                return;

            var textMeshPro = textMeshProPool.Get();
            textMeshPro.transform.SetParent(transform.Transform, worldPositionStays: false);

            var component = new TextShapeComponent(textMeshPro);

            component.FontRequest.Update(World, sceneData, textShape.FontSrc, scenePartition);
            TMPProSdkExtensions.Apply(ref component, textShape, fontsStorage, materialPropertyBlock);

            World.Add(entity, component);

            // IF there is a visibility component, it will set it invisible in the visibility system
            textMeshPro.enabled = true;
            changedTextMeshes.Add(entity, component);

            // It is necessary to store the first valid bounding box of the TMP in order to calculate its visibility later
            component.LastValidBoundingBoxSize = textMeshPro.renderer.bounds.size; // Note: Using Renderer because the bounds of the TMP does not return what we need
        }
    }
}
