using DCL.ECSComponents;
using ECS.ComponentsPooling;
using System;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer.Components
{
    public struct PrimitiveMeshComponent : IPoolableComponentProvider
    {
        public Mesh Mesh;
        public PBMeshRenderer.MeshOneofCase SDKType;
        public object PoolableComponent => Mesh;
        public Type PoolableComponentType => typeof(Mesh);

        public void Dispose() { }
    }
}
