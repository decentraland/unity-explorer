﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
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
        public const string MATERIAL_PATH = "BasicShapeMaterial";

        internal CreateBasicMaterialSystem(World world, IObjectPool<Material> materialsPool) : base(world, materialsPool) { }

        protected override void Update(float t)
        {
            HandleQuery(World);
        }

        [Query]
        private void Handle(ref MaterialComponent materialComponent)
        {
            if (materialComponent.Data.IsPbrMaterial)
                return;

            if (materialComponent.Status == MaterialComponent.LifeCycle.LoadingInProgress)
                ConstructMaterial(ref materialComponent);
        }

        private void ConstructMaterial(ref MaterialComponent materialComponent)
        {
            // Check if all promises are finished
            // Promises are finished if: all of their entities are invalid, no promises at all, or the result component exists

            if (TryGetTextureResult(ref materialComponent.AlbedoTexPromise, out StreamableLoadingResult<Texture2D> albedoResult))
            {
                materialComponent.Status = MaterialComponent.LifeCycle.LoadingFinished;

                materialComponent.Result ??= CreateNewMaterialInstance();

                SetUp(materialComponent.Result, materialComponent.Data.AlphaTest, materialComponent.Data.DiffuseColor);

                TrySetTexture(materialComponent.Result, ref albedoResult, ShaderUtils.BaseMap);

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
