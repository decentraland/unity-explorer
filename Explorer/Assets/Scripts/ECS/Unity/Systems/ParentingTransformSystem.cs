using Arch.Core;
using Arch.Core.Extensions;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using ECS.Unity.Systems;
using System.Collections.Generic;
using Unity.ECS.Components;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(InstantiateTransformSystem))]
[UpdateBefore(typeof(UpdateTransformSystem))]
public partial class ParentingTransformSystem : BaseUnityLoopSystem
{
    private readonly QueryDescription queryDescription = new QueryDescription().WithAll<SDKTransform, Transform>();
    private Transform sceneRootTransform;

    private OrphanTransform orphanTransform;
    private ParentTransform parentTransform;

    public ParentingTransformSystem(World world, Dictionary<CRDTEntity, Entity> entitiesMap, Transform sceneRootTransform) : base(world)
    {
        orphanTransform = new OrphanTransform(sceneRootTransform);
        parentTransform = new ParentTransform(entitiesMap, sceneRootTransform);
    }

    protected override void Update(float t)
    {
        World.InlineEntityQuery<OrphanTransform, SDKTransform, Transform>(in queryDescription, ref orphanTransform);
        World.InlineEntityQuery<ParentTransform, SDKTransform, Transform>(in queryDescription, ref parentTransform);
    }

    private readonly struct OrphanTransform : IForEachWithEntity<SDKTransform, Transform>
    {
        private readonly Transform sceneRootTransform;

        public OrphanTransform(Transform sceneRootTransform)
        {
            this.sceneRootTransform = sceneRootTransform;
        }

        public void Update(in Entity entity, ref SDKTransform sdkTransform, ref Transform entityTransform)
        {
            if (sdkTransform.IsDirty && sdkTransform.ParentId.Id == SpecialEntityId.SCENE_ROOT_ENTITY && entityTransform.parent != sceneRootTransform)
                entityTransform.SetParent(sceneRootTransform, true);
        }
    }


    private readonly struct ParentTransform : IForEachWithEntity<SDKTransform, Transform>
    {
        private readonly Transform sceneRootTransform;
        private readonly Dictionary<CRDTEntity, Entity> entitiesMap;

        public ParentTransform(Dictionary<CRDTEntity, Entity> entitiesMap, Transform sceneRootTransform)
        {
            this.entitiesMap = entitiesMap;
            this.sceneRootTransform = sceneRootTransform;
        }

        public void Update(in Entity entity, ref SDKTransform sdkTransform, ref Transform entityTransform)
        {
            if (sdkTransform.IsDirty && sdkTransform.ParentId.Id != SpecialEntityId.SCENE_ROOT_ENTITY)
            {
                if (entitiesMap.TryGetValue(sdkTransform.ParentId, out Entity parentEntity))
                {
                    Transform parentTransform = parentEntity.Get<Transform>();

                    if (entityTransform.parent != parentTransform)
                        entityTransform.SetParent(parentTransform, true);
                }
                else { entityTransform.SetParent(sceneRootTransform, true); }


            }
        }
    }

}
