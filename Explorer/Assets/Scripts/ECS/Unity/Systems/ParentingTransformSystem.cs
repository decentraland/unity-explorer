using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using System.Collections.Generic;
using Unity.ECS.Components;
using UnityEngine;

namespace ECS.Unity.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InstantiateTransformSystem))]
    [UpdateBefore(typeof(UpdateTransformSystem))]
    public partial class ParentingTransformSystem : BaseUnityLoopSystem
    {
        private readonly QueryDescription queryDescription = new QueryDescription().WithAll<SDKTransform, Transform>();
        private Transform sceneRootTransform;

        private OrphanTransform orphanTransform;
        private ParentTransform parentTransform;

        public ParentingTransformSystem(World world, in Dictionary<CRDTEntity, Entity> entitiesMap, Transform sceneRootTransform) : base(world)
        {
            orphanTransform = new OrphanTransform(sceneRootTransform);
            parentTransform = new ParentTransform(entitiesMap, sceneRootTransform, World);
        }

        protected override void Update(float t)
        {
            World.InlineQuery<OrphanTransform, SDKTransform, Transform>(in queryDescription, ref orphanTransform);
            World.InlineEntityQuery<ParentTransform, SDKTransform, Transform>(in queryDescription, ref parentTransform);
        }

        private readonly struct OrphanTransform : IForEach<SDKTransform, Transform>
        {
            private readonly Transform sceneRootTransform;

            public OrphanTransform(Transform sceneRootTransform)
            {
                this.sceneRootTransform = sceneRootTransform;
            }

            public void Update(ref SDKTransform sdkTransform, ref Transform entityTransform)
            {
                if (!sdkTransform.IsDirty) return;

                if (sdkTransform.ParentId.Id == SpecialEntityId.SCENE_ROOT_ENTITY && entityTransform.parent != sceneRootTransform)
                    entityTransform.SetParent(sceneRootTransform, true);
            }
        }

        private readonly struct ParentTransform : IForEachWithEntity<SDKTransform, Transform>
        {
            private readonly Transform sceneRootTransform;
            private readonly Dictionary<CRDTEntity, Entity> entitiesMap;
            private readonly World world;

            public ParentTransform(in Dictionary<CRDTEntity, Entity> entitiesMap, Transform sceneRootTransform, in World world)
            {
                this.entitiesMap = entitiesMap;
                this.sceneRootTransform = sceneRootTransform;
                this.world = world;
            }

            public void Update(in Entity entity, ref SDKTransform sdkTransform, ref Transform entityTransform)
            {
                if (!sdkTransform.IsDirty) return;

                if (sdkTransform.ParentId.Id != SpecialEntityId.SCENE_ROOT_ENTITY)
                {
                    if (entitiesMap.TryGetValue(sdkTransform.ParentId, out Entity parentEntity))
                    {
                        Transform parentTransform = world.Get<Transform>(parentEntity);

                        if (entityTransform.parent != parentTransform)
                            entityTransform.SetParent(parentTransform, true);
                    }
                    else { entityTransform.SetParent(sceneRootTransform, true); }
                }
            }
        }
    }
}
