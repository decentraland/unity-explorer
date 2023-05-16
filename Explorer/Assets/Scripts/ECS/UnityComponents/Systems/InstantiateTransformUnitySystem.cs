using Arch.Core;
using Arch.Core.Extensions;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using ECS.ComponentsPooling;
using UnityEngine;

namespace ECS.UnitySystems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateTransformUnitySystem))]
    public partial class InstantiateTransformUnitySystem : BaseUnityLoopSystem
    {
        private readonly QueryDescription queryDescription = new QueryDescription().WithAll<SDKTransform>().WithNone<Transform>();
        private TransformInstantiator cleanTransform;

        protected override void Update(float _)
        {
            World.InlineEntityQuery<TransformInstantiator, SDKTransform>(in queryDescription, ref cleanTransform);
        }

        public InstantiateTransformUnitySystem(World world, IComponentPoolsRegistry componentPools) : base(world)
        {
            cleanTransform = new TransformInstantiator(componentPools.GetReferenceTypePool(typeof(Transform)));
        }

        private readonly struct TransformInstantiator : IForEachWithEntity<SDKTransform>
        {
            private readonly IComponentPool transformPool;

            public TransformInstantiator(IComponentPool transformPool)
            {
                this.transformPool = transformPool;
            }

            public void Update(in Entity entity, ref SDKTransform t0)
            {
                var emptyGameObject = (Transform)transformPool.Rent();
                emptyGameObject.transform.SetParent(null);
                emptyGameObject.name = "Entity " + entity.Id;
                entity.Add(emptyGameObject);
            }
        }
    }
}
