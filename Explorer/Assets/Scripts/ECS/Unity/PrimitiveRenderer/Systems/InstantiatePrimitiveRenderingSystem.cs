using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using ECS.Unity.PrimitiveRenderer.MeshSetup;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    public partial class InstantiatePrimitiveRenderingSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<MeshRenderer> rendererPoolRegistry;
        private readonly IComponentPoolsRegistry poolRegistry;
        private readonly Material urpLitMaterial;

        private static readonly Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> SETUP_MESH_LOGIC = new()
        {
            { PBMeshRenderer.MeshOneofCase.Box, new MeshSetupBox() },
            { PBMeshRenderer.MeshOneofCase.Sphere, new MeshSetupSphere() },
            { PBMeshRenderer.MeshOneofCase.Cylinder, new MeshSetupCylinder() },
            { PBMeshRenderer.MeshOneofCase.Plane, new MeshSetupPlane() }
        };

        private readonly Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> setupMeshCases;

        internal InstantiatePrimitiveRenderingSystem(World world, IComponentPoolsRegistry poolsRegistry,
            Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> setupMeshCases = null) : base(world)
        {
            this.setupMeshCases = setupMeshCases ?? SETUP_MESH_LOGIC;
            poolRegistry = poolsRegistry;

            rendererPoolRegistry = poolsRegistry.GetReferenceTypePool<MeshRenderer>();
            urpLitMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
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
            var meshRendererComponent = new PrimitiveMeshRendererComponent();
            var setupMesh = setupMeshCases[sdkComponent.MeshCase];
            var meshRendererGo = rendererPoolRegistry.Get();
            meshRendererGo.sharedMaterial = urpLitMaterial;
            Instantiate(setupMesh, ref meshRendererGo, ref meshRendererComponent, sdkComponent, ref transform);
            World.Add(entity, meshRendererComponent);
        }

        [Query]
        [All(typeof(PBMeshRenderer), typeof(SDKTransform), typeof(TransformComponent),
            typeof(PrimitiveMeshRendererComponent))]
        private void TrySetupExistingRenderer(
            ref PrimitiveMeshRendererComponent meshRendererComponent,
            ref PBMeshRenderer sdkComponent,
            ref TransformComponent transform)
        {
            if (!sdkComponent.IsDirty) return;

            var setupMesh = setupMeshCases[sdkComponent.MeshCase];

            // The model has changed entirely, so we need to reinstall the renderer
            if (ReferenceEquals(meshRendererComponent.PrimitiveMesh, null))
                Instantiate(setupMesh, ref meshRendererComponent.MeshRenderer, ref meshRendererComponent, sdkComponent,
                    ref transform);
            else
                // This means that the UVs have changed during runtime of a scene (should be an unusual case), so we update the mesh accordingly
                setupMesh.Execute(sdkComponent, meshRendererComponent.PrimitiveMesh.PrimitiveMesh);
  

            sdkComponent.IsDirty = false;
        }


        /// <summary>
        ///     It is either called when there is no collider or collider was invalidated before (set to null)
        /// </summary>
        private void Instantiate(ISetupMesh meshSetup, ref MeshRenderer meshRendererGo,
            ref PrimitiveMeshRendererComponent rendererComponent,
            PBMeshRenderer sdkComponent, ref TransformComponent transformComponent)
        {
            var primitiveMesh = (IPrimitiveMesh)poolRegistry.GetPool(meshSetup.MeshType).Rent();
            meshSetup.Execute(sdkComponent, primitiveMesh.PrimitiveMesh);

            rendererComponent.PrimitiveMesh = primitiveMesh;
            rendererComponent.MeshRenderer = meshRendererGo;
            rendererComponent.SDKType = sdkComponent.MeshCase;

            meshRendererGo.GetComponent<MeshFilter>().mesh = primitiveMesh.PrimitiveMesh;

            Transform rendererTransform = meshRendererGo.transform;
            rendererTransform.SetParent(transformComponent.Transform, false);
            rendererTransform.localPosition = Vector3.zero;
            rendererTransform.localRotation = Quaternion.identity;
            rendererTransform.localScale = Vector3.one;
        }
    }

}
