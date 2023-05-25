using ECS.ComponentsPooling;
using System;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer.Components
{
    public struct PrimitiveRendererComponent : IPoolableComponentProvider
    {
        public MeshRenderer MeshRenderer;
        public MeshFilter MeshFilter;

        public object PoolableComponent => MeshRenderer;
        public Type PoolableComponentType => typeof(MeshRenderer);

        public void Dispose() { }
    }
}
