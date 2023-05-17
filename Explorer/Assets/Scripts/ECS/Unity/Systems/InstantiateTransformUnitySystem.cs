using Arch.Core;
using Arch.Core.Extensions;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using ECS.ComponentsPooling;
using UnityEngine;

namespace ECS.Unity.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateTransformUnitySystem))]
    public partial class InstantiateTransformUnitySystem : BaseUnityLoopSystem
    {
        private readonly QueryDescription queryDescription = new QueryDescription().WithAll<SDKTransform>().WithNone<Transform>();
        private TransformInstantiator instantiateTransform;

        protected override void Update(float _)
        {
            World.InlineEntityQuery<TransformInstantiator, SDKTransform>(in queryDescription, ref instantiateTransform);
        }

        public InstantiateTransformUnitySystem(World world, IComponentPoolsRegistry componentPools) : base(world)
        {
            instantiateTransform = new TransformInstantiator(componentPools.GetReferenceTypePool<GameObject>());
        }

        private readonly struct TransformInstantiator : IForEachWithEntity<SDKTransform>
        {
            private readonly IComponentPool gameObjectPool;

            public TransformInstantiator(IComponentPool gameObjectPool)
            {
                this.gameObjectPool = gameObjectPool;
            }

            public void Update(in Entity entity, ref SDKTransform sdkTransform)
            {
                Transform newTransform = ((GameObject)gameObjectPool.Rent()).transform;

                //TODO: Parent to the scene root
                newTransform.SetParent(null);
                newTransform.name = "Entity " + entity.Id;
                newTransform.position = sdkTransform.Position;
                newTransform.rotation = sdkTransform.Rotation;
                newTransform.localScale = sdkTransform.Scale;
                entity.Add(newTransform);
            }
        }
    }
}
