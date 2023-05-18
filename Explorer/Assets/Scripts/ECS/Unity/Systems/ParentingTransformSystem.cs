using Arch.Core;
using Arch.Core.Extensions;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using ECS.Unity.Systems;
using System.Collections.Generic;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(InstantiateTransformSystem))]
[UpdateBefore(typeof(UpdateTransformSystem))]
public partial class ParentingTransformSystem : BaseUnityLoopSystem
{
    private readonly QueryDescription queryDescription = new QueryDescription().WithAll<SDKTransform, Transform>();
    private ParentTransform parentTransform;

    public ParentingTransformSystem(World world, Dictionary<CRDTEntity, Entity> entitiesMap) : base(world)
    {
        parentTransform = new ParentTransform(entitiesMap);
    }

    protected override void Update(float t)
    {
        World.InlineEntityQuery<ParentTransform, SDKTransform, Transform>(in queryDescription, ref parentTransform);
    }

    private readonly struct ParentTransform : IForEachWithEntity<SDKTransform, Transform>
    {
        private readonly Dictionary<CRDTEntity, Entity> entitiesMap;

        public ParentTransform(Dictionary<CRDTEntity, Entity> entitiesMap)
        {
            this.entitiesMap = entitiesMap;
        }

        public void Update(in Entity entity, ref SDKTransform sdkTransform, ref Transform entityTransform)
        {
            if (sdkTransform.ParentId.Id != 0 && sdkTransform.IsDirty)
            {
                Transform transformParent = entitiesMap[sdkTransform.ParentId].Get<Transform>();

                if (!entityTransform.IsChildOf(transformParent))
                    entityTransform.SetParent(transformParent);
            }
        }
    }
}
