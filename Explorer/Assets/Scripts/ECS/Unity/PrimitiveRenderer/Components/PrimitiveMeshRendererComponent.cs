using System;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
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

        IPrimitiveMesh IPoolableComponentProvider<IPrimitiveMesh>.PoolableComponent => PrimitiveMesh;

        MeshRenderer IPoolableComponentProvider<MeshRenderer>.PoolableComponent => MeshRenderer;

        public void Dispose()
        {
        }
    }
}