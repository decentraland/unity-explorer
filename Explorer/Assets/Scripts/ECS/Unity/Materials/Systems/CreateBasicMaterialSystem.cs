using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Shaders;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.Materials.Components;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace ECS.Unity.Materials.Systems
{
    [UpdateInGroup(typeof(MaterialLoadingGroup))]
    [UpdateAfter(typeof(StartMaterialsLoadingSystem))]
    public partial class CreateBasicMaterialSystem : CreateMaterialSystemBase
    {
        private readonly IPerformanceBudget memoryBudgetProvider;
        private readonly IPerformanceBudget capFrameBudget;

        public CreateBasicMaterialSystem(World world, IObjectPool<Material> materialsPool, IPerformanceBudget capFrameBudget, IPerformanceBudget memoryBudgetProvider) : base(world, materialsPool)
        {
            this.capFrameBudget = capFrameBudget;
            this.memoryBudgetProvider = memoryBudgetProvider;
        }

        protected override void Update(float t)
        {
            HandleQuery(World);
        }

        [Query]
        private void Handle(ref MaterialComponent materialComponent)
        {
            if (materialComponent.Data.IsPbrMaterial)
                return;

            if (!capFrameBudget.TrySpendBudget() || !memoryBudgetProvider.TrySpendBudget())
                return;

            if (materialComponent.Status == StreamableLoading.LifeCycle.LoadingInProgress)
                ConstructMaterial(ref materialComponent);
        }

        private void ConstructMaterial(ref MaterialComponent materialComponent)
        {
            // Check if all promises are finished
            // Promises are finished if: all of their entities are invalid, no promises at all, or the result component exists

            if (TryGetTextureResult(ref materialComponent.AlbedoTexPromise, out StreamableLoadingResult<Texture2D> albedoResult))
            {
                materialComponent.Status = StreamableLoading.LifeCycle.LoadingFinished;

                materialComponent.Result ??= CreateNewMaterialInstance();

                SetUp(materialComponent.Result, materialComponent.Data.AlphaTest, materialComponent.Data.DiffuseColor);

                TrySetTexture(materialComponent.Result, ref albedoResult, ShaderUtils.BaseMap, materialComponent.Data.AlbedoTexture);

                DestroyEntityReference(ref materialComponent.AlbedoTexPromise);
            }
        }

        public static void SetUp(Material material, float alphaTest, Color diffuseColor)
        {
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetInt(ShaderUtils.ZWrite, 1);
            material.SetFloat(ShaderUtils.AlphaClip, 1);
            material.SetFloat(ShaderUtils.Cutoff, alphaTest);
            material.SetColor(ShaderUtils.BaseColor, diffuseColor);
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }
    }
}
