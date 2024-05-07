using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using TMPro;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    public partial class InstantiateTextShapeSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly IComponentPool<TextMeshPro> textMeshProPool;
        private readonly IFontsStorage fontsStorage;
        private readonly MaterialPropertyBlock materialPropertyBlock;

        public InstantiateTextShapeSystem(World world, IComponentPool<TextMeshPro> textMeshProPool, IFontsStorage fontsStorage, MaterialPropertyBlock materialPropertyBlock, IPerformanceBudget instantiationFrameTimeBudget) : base(world)
        {
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.textMeshProPool = textMeshProPool;
            this.fontsStorage = fontsStorage;
            this.materialPropertyBlock = materialPropertyBlock;
        }

        protected override void Update(float t)
        {
            InstantiateRemainingQuery(World!);
        }

        [Query]
        [None(typeof(TextShapeComponent))]
        private void InstantiateRemaining(in Entity entity, in TransformComponent transform, in PBTextShape textShape)
        {
            if (instantiationFrameTimeBudget.TrySpendBudget() == false)
                return;

            var textMeshPro = textMeshProPool.Get();
            textMeshPro.transform.SetParent(transform.Transform, worldPositionStays: false);

            textMeshPro.Apply(textShape, fontsStorage, materialPropertyBlock);

            World.Add(entity, new TextShapeComponent(textMeshPro));
        }
    }
}
