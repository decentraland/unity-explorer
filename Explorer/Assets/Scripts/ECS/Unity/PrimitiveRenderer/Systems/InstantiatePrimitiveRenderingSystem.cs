using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    public partial class InstantiatePrimitiveRenderingSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<MeshRenderer> rendererPoolRegistry;
        private readonly IComponentPool<Mesh> meshPoolRegistry;

        private readonly Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> SETUP_MESH_LOGIC = new ()
        {
            { PBMeshRenderer.MeshOneofCase.Box, new SetupBoxMesh() },
            { PBMeshRenderer.MeshOneofCase.Sphere, new SetupSphereMesh() },
            { PBMeshRenderer.MeshOneofCase.Cylinder, new SetupCylinder() },
            { PBMeshRenderer.MeshOneofCase.Plane, new SetupPlaneMesh() },
        };

        private readonly Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> setupMeshCases;

        internal InstantiatePrimitiveRenderingSystem(World world, IComponentPoolsRegistry poolsRegistry,
            Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> setupMeshCases = null) : base(world)
        {
            this.setupMeshCases = setupMeshCases ?? SETUP_MESH_LOGIC;

            rendererPoolRegistry = poolsRegistry.GetReferenceTypePool<MeshRenderer>();
            meshPoolRegistry = poolsRegistry.GetReferenceTypePool<Mesh>();
        }

        protected override void Update(float t)
        {
            InstantiateNonExistingRendererQuery(World);
            TrySetupExistingRendererQuery(World);
        }

        [Query]
        [All(typeof(PBMeshRenderer), typeof(SDKTransform), typeof(TransformComponent))]
        [None(typeof(PrimitiveRendererComponent))]
        private void InstantiateNonExistingRenderer(in Entity entity, ref PBMeshRenderer sdkComponent, ref TransformComponent transform)
        {
            var rendererComponent = new PrimitiveRendererComponent();
            var meshComponent = new PrimitiveMeshComponent();
            Instantiate(setupMeshCases[sdkComponent.MeshCase], ref rendererComponent, ref meshComponent, sdkComponent, ref transform);
            World.Add(entity, rendererComponent, meshComponent);
        }

        [Query]
        [All(typeof(PBMeshRenderer), typeof(SDKTransform), typeof(TransformComponent),
            typeof(PrimitiveRendererComponent), typeof(PrimitiveMeshComponent))]
        private void TrySetupExistingRenderer(
            ref PrimitiveRendererComponent primitiveRendererComponent,
            ref PrimitiveMeshComponent meshComponent,
            ref PBMeshRenderer sdkComponent,
            ref TransformComponent transform)
        {
            if (!sdkComponent.IsDirty) return;

            ISetupMesh setupCollider = setupMeshCases[sdkComponent.MeshCase];

            // The model has changed entirely, so we need to reinstall the renderer
            if (ReferenceEquals(meshComponent.Mesh, null))
                Instantiate(setupCollider, ref primitiveRendererComponent, ref meshComponent, sdkComponent, ref transform);

            sdkComponent.IsDirty = false;
        }


        /// <summary>
        ///     It is either called when there is no collider or collider was invalidated before (set to null)
        /// </summary>
        private void Instantiate(ISetupMesh meshSetup, ref PrimitiveRendererComponent rendererComponent, ref PrimitiveMeshComponent meshComponent,
            PBMeshRenderer sdkComponent, ref TransformComponent transformComponent)
        {
            MeshRenderer meshRendererGo = rendererPoolRegistry.Get();
            meshRendererGo.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            Mesh mesh = meshPoolRegistry.Get();

            meshSetup.Execute(sdkComponent, mesh);

            meshComponent.Mesh = mesh;
            meshComponent.SDKType = sdkComponent.MeshCase;

            rendererComponent.MeshRenderer = meshRendererGo;
            rendererComponent.MeshFilter = meshRendererGo.GetComponent<MeshFilter>();
            rendererComponent.MeshFilter.sharedMesh = mesh;

            Transform rendererTransform = meshRendererGo.transform;
            rendererTransform.SetParent(transformComponent.Transform, false);
            rendererTransform.localPosition = Vector3.zero;
            rendererTransform.localRotation = Quaternion.identity;
            rendererTransform.localScale = Vector3.one;
        }
    }

}
