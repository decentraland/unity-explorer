using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using UnityEngine;

namespace ECS.Unity.Systems
{
    /// <summary>
    ///     Runs after the full cycle of the systems to ensure that all Transforms are instantiated.
    ///     <para>It's important to validate it as Unity Components systems rely on the existence of the parent transform</para>
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InstantiateTransformUnitySystem))]
    [UpdateBefore(typeof(ComponentInstantiationGroup))]
    public partial class AssertDisconnectedTransformsSystem : BaseUnityLoopSystem
    {
        internal AssertDisconnectedTransformsSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            FindDisconnectedTransformQuery(World);
        }

        [Query]
        [All(typeof(CRDTEntity), typeof(SDKTransform))]
        [None(typeof(Transform), typeof(DeleteEntityIntention))]
        private void FindDisconnectedTransform(ref CRDTEntity entity)
        {
            Debug.LogError($"Transform does not exist for the alive entity \"{entity}\"");
        }
    }
}
