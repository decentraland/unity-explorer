using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using ECS.Unity.PrimitiveRenderer.MeshSetup;
using ECS.Unity.SceneBoundsChecker;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;


namespace ECS.Unity.PrimitiveRenderer.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    public partial class InstantiatePrimitiveRenderingSystem : BaseUnityLoopSystem
    {
        private static readonly Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> SETUP_MESH_LOGIC = new ()
        {
            { PBMeshRenderer.MeshOneofCase.Box, new MeshSetupBox() },
            { PBMeshRenderer.MeshOneofCase.Sphere, new MeshSetupSphere() },
            { PBMeshRenderer.MeshOneofCase.Cylinder, new MeshSetupCylinder() },
            { PBMeshRenderer.MeshOneofCase.Plane, new MeshSetupPlane() },
        };
        private readonly IComponentPool<MeshRenderer> rendererPoolRegistry;
        private readonly IComponentPoolsRegistry poolRegistry;
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly ISceneData sceneData;
        private readonly EntityEventBuffer<PrimitiveMeshRendererComponent> changedMeshes;

        private readonly Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> setupMeshCases;

        public InstantiatePrimitiveRenderingSystem(World world, IComponentPoolsRegistry poolsRegistry,
            IPerformanceBudget instantiationFrameTimeBudget, ISceneData sceneData, EntityEventBuffer<PrimitiveMeshRendererComponent> changedMeshes, Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh>? setupMeshCases = null) : base(world)
        {
            this.setupMeshCases = setupMeshCases ?? SETUP_MESH_LOGIC;
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.sceneData = sceneData;
            this.changedMeshes = changedMeshes;
            poolRegistry = poolsRegistry;

            if (!poolsRegistry.TryGetReferenceTypePool(out IComponentPool<MeshRenderer>? pool))
                throw new InvalidOperationException($"No component pool registered for {nameof(MeshRenderer)}");

            rendererPoolRegistry = pool;
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

            if (!instantiationFrameTimeBudget.TrySpendBudget())
                return;

            var meshRendererComponent = new PrimitiveMeshRendererComponent();
            MeshRenderer meshRendererGo = rendererPoolRegistry.Get();

            if (!Instantiate(entity, setupMesh, ref meshRendererGo, ref meshRendererComponent, sdkComponent, ref transform))
            {
                rendererPoolRegistry.Release(meshRendererGo);
                return;
            }

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

            if (!instantiationFrameTimeBudget.TrySpendBudget())
                return;

            // The model has changed entirely, so we need to reinstall the renderer
            if (ReferenceEquals(meshRendererComponent.PrimitiveMesh, null))
                Instantiate(entity, setupMesh, ref meshRendererComponent.MeshRenderer, ref meshRendererComponent, sdkComponent,
                    ref transform);
            else
            {
                // This means that the UVs have changed during runtime of a scene (should be an unusual case), so we update the mesh accordingly
                setupMesh.Execute(sdkComponent, meshRendererComponent.PrimitiveMesh);
                meshRendererComponent.MeshRenderer.GetComponent<MeshFilter>().mesh = meshRendererComponent.PrimitiveMesh.Mesh;
            }
        }

        /// <summary>
        ///     It is either called when there is no mesh or mesh was invalidated before (set to null)
        /// </summary>
        private bool Instantiate(Entity entity, ISetupMesh meshSetup, ref MeshRenderer meshRendererGo,
            ref PrimitiveMeshRendererComponent rendererComponent,
            PBMeshRenderer sdkComponent, ref TransformComponent transformComponent)
        {
            if (!poolRegistry.TryGetPool(meshSetup.MeshType, out IComponentPool meshPool))
            {
                ReportHub.LogError(GetReportData(), $"No component pool registered for {meshSetup.MeshType}");
                return false;
            }

            var primitiveMesh = (IPrimitiveMesh)meshPool.Rent();
            meshSetup.Execute(sdkComponent, primitiveMesh);

            rendererComponent.PrimitiveMesh = primitiveMesh;
            rendererComponent.MeshRenderer = meshRendererGo;
            rendererComponent.SDKType = sdkComponent.MeshCase;

            meshRendererGo.GetComponent<MeshFilter>().mesh = primitiveMesh.Mesh;

            Transform rendererTransform = meshRendererGo.transform;
            rendererTransform.SetParent(transformComponent.Transform, false);
            rendererTransform.ResetLocalTRS();

            // ensure that meshes that come from the pool are enabled
            rendererComponent.MeshRenderer.enabled = true;
            rendererComponent.MeshRenderer.forceRenderingOff = false;

            rendererComponent.SetDefaultMaterial(sceneData.Geometry.CircumscribedPlanes, sceneData.Geometry.Height);

            changedMeshes.Add(entity, rendererComponent);
            return true;
        }
    }
}
