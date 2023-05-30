using DCL.ECSComponents;
using ECS.ComponentsPooling;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using System;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer.Components
{
    public struct PrimitiveMeshRendererComponent : IPoolableComponentProvider<IPrimitiveMesh>,
        IPoolableComponentProvider<MeshRenderer>
    {
        public IPrimitiveMesh PrimitiveMesh;
        public MeshRenderer MeshRenderer;
        public PBMeshRenderer.MeshOneofCase SDKType;

        Type IPoolableComponentProvider<IPrimitiveMesh>.PoolableComponentType => PrimitiveMesh.GetType();

        Type IPoolableComponentProvider<MeshRenderer>.PoolableComponentType => typeof(MeshRenderer);

        IPrimitiveMesh IPoolableComponentProvider<IPrimitiveMesh>.PoolableComponent => PrimitiveMesh;

        MeshRenderer IPoolableComponentProvider<MeshRenderer>.PoolableComponent => MeshRenderer;

        public void Dispose() { }
    }
}
