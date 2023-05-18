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
        private readonly Transform sceneRootTransform;

        protected override void Update(float _)
        {
            World.InlineEntityQuery<TransformInstantiator, SDKTransform>(in queryDescription, ref instantiateTransform);
        }

        public InstantiateTransformSystem(World world, IComponentPoolsRegistry componentPools) : base(world)
        {
            IComponentPool transformPool = componentPools.GetReferenceTypePool(typeof(Transform));
            sceneRootTransform = (Transform)transformPool.Rent();
            sceneRootTransform.name = "SCENE_ROOT";
            world.Create(sceneRootTransform);

            instantiateTransform = new TransformInstantiator(transformPool, sceneRootTransform);
        }

        private readonly struct TransformInstantiator : IForEachWithEntity<SDKTransform>
        {
            private readonly IComponentPool gameObjectPool;
            private readonly Transform sceneRoot;

            public TransformInstantiator(IComponentPool gameObjectPool, Transform sceneRoot)
            {
                this.gameObjectPool = gameObjectPool;
                this.sceneRoot = sceneRoot;
            }

            public void Update(in Entity entity, ref SDKTransform sdkTransform)
            {
                var newTransform = (Transform)gameObjectPool.Rent();

                newTransform.SetParent(sceneRoot.transform);
                newTransform.name = "Entity " + entity.Id;

                newTransform.position = sdkTransform.Position;
                newTransform.rotation = sdkTransform.Rotation;
                newTransform.localScale = sdkTransform.Scale;
                entity.Add(newTransform);
            }
        }


    }
}
