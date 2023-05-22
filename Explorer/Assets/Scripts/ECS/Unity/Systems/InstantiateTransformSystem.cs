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
    [UpdateBefore(typeof(UpdateTransformSystem))]
    public partial class InstantiateTransformSystem : BaseUnityLoopSystem
    {
        private readonly QueryDescription queryDescription = new QueryDescription().WithAll<SDKTransform>().WithNone<Transform>();
        private TransformInstantiator instantiateTransform;

        protected override void Update(float _)
        {
            World.InlineEntityQuery<TransformInstantiator, SDKTransform>(in queryDescription, ref instantiateTransform);
        }

        public InstantiateTransformSystem(World world, IComponentPoolsRegistry componentPools, Transform sceneRootTransform) : base(world)
        {
            IComponentPool transformPool = componentPools.GetReferenceTypePool(typeof(Transform));
            instantiateTransform = new TransformInstantiator(componentPools.GetReferenceTypePool(typeof(Transform)), sceneRootTransform);
        }

        private readonly struct TransformInstantiator : IForEachWithEntity<SDKTransform>
        {
            private readonly IComponentPool transformPool;
            private readonly Transform sceneRoot;

            public TransformInstantiator(IComponentPool transformPool, Transform sceneRoot)
            {
                this.transformPool = transformPool;
                this.sceneRoot = sceneRoot;
            }

            public void Update(in Entity entity, ref SDKTransform sdkTransform)
            {
                var newTransform = (Transform)transformPool.Rent();
                newTransform.name = "Entity " + entity.Id;
                entity.Add(newTransform);
            }
        }


    }
}
