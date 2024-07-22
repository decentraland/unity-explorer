using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.Components
{
    public struct PrimitiveMeshRendererComponent :
        IPoolableComponentProvider<IPrimitiveMesh>,
        IPoolableComponentProvider<MeshRenderer>
    {
        /// <summary>
        ///     Set for tests only
        /// </summary>
        public MeshRenderer MeshRenderer { get; set; }

        /// <summary>
        ///     Set for tests only
        /// </summary>
        public IPrimitiveMesh? PrimitiveMesh { get; set; }

        /// <summary>
        ///     Set for tests only
        /// </summary>
        public PBMeshRenderer.MeshOneofCase SDKType { get; set; }

        private bool defaultMaterialIsUsed;

        public void UseDefaultMaterial(Material defaultMaterial)
        {
            MeshRenderer.sharedMaterial = defaultMaterial;
            defaultMaterialIsUsed = true;
        }

        [SuppressMessage("ReSharper", "ParameterHidesMember")]
        public void Reinstall(IPrimitiveMesh primitiveMesh, MeshRenderer meshRenderer, PBMeshRenderer.MeshOneofCase sdkType)
        {
            this.PrimitiveMesh = primitiveMesh;
            this.MeshRenderer = meshRenderer;
            this.SDKType = sdkType;
        }

        // The model has changed entirely, so we need to reinstall the renderer
        public readonly bool IsReadyToReinstall() =>
            ReferenceEquals(PrimitiveMesh!, null!);

        public readonly bool ShouldInvalidate(PBMeshRenderer pbMeshRenderer) =>
            pbMeshRenderer.IsDirty && pbMeshRenderer.MeshCase != SDKType && PrimitiveMesh != null;

        public void PrepareToReinstall(IComponentPoolsRegistry poolsRegistry)
        {
            Release(poolsRegistry);
            PrimitiveMesh = null; // it will be a signal to instantiate a new renderer
        }

        public void TryReleaseDefaultMaterial()
        {
            if (!defaultMaterialIsUsed) return;

            DefaultMaterial.Release(MeshRenderer.sharedMaterial);
            defaultMaterialIsUsed = false;
        }

        public void Release(IComponentPoolsRegistry poolsRegistry)
        {
            if (PrimitiveMesh == null)
                throw new InvalidOperationException("PrimitiveMeshRendererComponent.Release called with null primitiveMesh!");

            TryReleaseDefaultMaterial();

            if (poolsRegistry.TryGetPool(PrimitiveMesh.GetType(), out IComponentPool componentPool))
                componentPool!.Release(PrimitiveMesh);
        }

        readonly Type IPoolableComponentProvider<IPrimitiveMesh>.PoolableComponentType => PrimitiveMesh!.GetType();

        readonly Type IPoolableComponentProvider<MeshRenderer>.PoolableComponentType => typeof(MeshRenderer);

        readonly IPrimitiveMesh IPoolableComponentProvider<IPrimitiveMesh>.PoolableComponent => PrimitiveMesh;

        readonly MeshRenderer IPoolableComponentProvider<MeshRenderer>.PoolableComponent => MeshRenderer;

        public void Dispose() { }

        public static PrimitiveMeshRendererComponent NewBoxMeshRendererComponent() =>
            new ()
            {
                PrimitiveMesh = new BoxPrimitive(),
                SDKType = PBMeshRenderer.MeshOneofCase.Box,
            };
    }
}
