using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using ECS.Unity.PrimitiveRenderer.MeshSetup;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    public partial class InstantiatePrimitiveRenderingSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<MeshRenderer> rendererPoolRegistry;
        private readonly IComponentPoolsRegistry poolRegistry;
        private readonly IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider;

        private static readonly Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> SETUP_MESH_LOGIC = new ()
        {
            { PBMeshRenderer.MeshOneofCase.Box, new MeshSetupBox() },
            { PBMeshRenderer.MeshOneofCase.Sphere, new MeshSetupSphere() },
            { PBMeshRenderer.MeshOneofCase.Cylinder, new MeshSetupCylinder() },
            { PBMeshRenderer.MeshOneofCase.Plane, new MeshSetupPlane() }
        };

        private readonly Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> setupMeshCases;

        internal InstantiatePrimitiveRenderingSystem(World world, IComponentPoolsRegistry poolsRegistry,
            IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider, Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> setupMeshCases = null) : base(world)
        {
            this.setupMeshCases = setupMeshCases ?? SETUP_MESH_LOGIC;
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            poolRegistry = poolsRegistry;

            rendererPoolRegistry = poolsRegistry.GetReferenceTypePool<MeshRenderer>();
        }

        public override void BeforeUpdate(in float t)
        {
            instantiationFrameTimeBudgetProvider.ReleaseBudget();
        }

        protected override void Update(float t)
        {
            InstantiateNonExistingRendererQuery(World);
            TrySetupExistingRendererQuery(World);
        }

        [Query]
        [All(typeof(PBMeshRenderer), typeof(SDKTransform), typeof(TransformComponent))]
        [None(typeof(PrimitiveMeshRendererComponent))]
        private void InstantiateNonExistingRenderer(in Entity entity, ref PBMeshRenderer sdkComponent, ref TransformComponent transform)
        {
            if (!setupMeshCases.TryGetValue(sdkComponent.MeshCase, out ISetupMesh setupMesh))
                return;

            if (!instantiationFrameTimeBudgetProvider.TrySpendBudget())
                return;

            var meshRendererComponent = new PrimitiveMeshRendererComponent();
            var meshRendererGo = rendererPoolRegistry.Get();
            meshRendererGo.sharedMaterial = DefaultMaterial.Shared;
            Instantiate(setupMesh, ref meshRendererGo, ref meshRendererComponent, sdkComponent, ref transform);
            World.Add(entity, meshRendererComponent);
        }

        [Query]
        [All(typeof(PBMeshRenderer), typeof(SDKTransform), typeof(TransformComponent), typeof(PrimitiveMeshRendererComponent))]
        private void TrySetupExistingRenderer(
            in Entity entity,
            ref PrimitiveMeshRendererComponent meshRendererComponent,
            ref PBMeshRenderer sdkComponent,
            ref TransformComponent transform)
        {
            if (!sdkComponent.IsDirty) return;

            if (!setupMeshCases.TryGetValue(sdkComponent.MeshCase, out ISetupMesh setupMesh))
            {
                // Remove the renderer component so it's not grabbed by other systems in the invalid state
                World.Remove<PrimitiveMeshRendererComponent>(entity);
                return;
            }

            if (!instantiationFrameTimeBudgetProvider.TrySpendBudget())
                return;

            // The model has changed entirely, so we need to reinstall the renderer
            if (ReferenceEquals(meshRendererComponent.PrimitiveMesh, null))
                Instantiate(setupMesh, ref meshRendererComponent.MeshRenderer, ref meshRendererComponent, sdkComponent,
                    ref transform);
            else

                // This means that the UVs have changed during runtime of a scene (should be an unusual case), so we update the mesh accordingly
                setupMesh.Execute(sdkComponent, meshRendererComponent.PrimitiveMesh.Mesh);
        }

        /// <summary>
        ///     It is either called when there is no mesh or mesh was invalidated before (set to null)
        /// </summary>
        private void Instantiate(ISetupMesh meshSetup, ref MeshRenderer meshRendererGo,
            ref PrimitiveMeshRendererComponent rendererComponent,
            PBMeshRenderer sdkComponent, ref TransformComponent transformComponent)
        {
            var primitiveMesh = (IPrimitiveMesh)poolRegistry.GetPool(meshSetup.MeshType).Rent();
            meshSetup.Execute(sdkComponent, primitiveMesh.Mesh);

            rendererComponent.PrimitiveMesh = primitiveMesh;
            rendererComponent.MeshRenderer = meshRendererGo;
            rendererComponent.SDKType = sdkComponent.MeshCase;

            meshRendererGo.GetComponent<MeshFilter>().mesh = primitiveMesh.Mesh;

            Transform rendererTransform = meshRendererGo.transform;
            rendererTransform.SetParent(transformComponent.Transform, false);
            rendererTransform.ResetLocalTRS();
        }
    }
}
