using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using UnityEngine;

namespace ECS.Unity.Systems
{
    /// <summary>
    ///     Runs after the full cycle of the systems to ensure that all Transforms are instantiated.
    ///     <para>It's important to validate it as Unity Components systems rely on the existence of the parent transform</para>
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(InstantiateTransformSystem))]
    [UpdateBefore(typeof(ComponentInstantiationGroup))]
    [ThrottlingEnabled]
    public partial class AssertDisconnectedTransformsSystem : BaseUnityLoopSystem
    {
        internal AssertDisconnectedTransformsSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            FindDisconnectedTransformQuery(World);
        }

        [Query]
        [All(typeof(CRDTEntity), typeof(SDKTransform))]
        [None(typeof(TransformComponent), typeof(DeleteEntityIntention))]
        private void FindDisconnectedTransform(ref CRDTEntity entity)
        {
            ReportHub.LogError($"Transform does not exist for the alive entity \"{entity}\"", ReportCategory.ECS);
        }
    }
}
