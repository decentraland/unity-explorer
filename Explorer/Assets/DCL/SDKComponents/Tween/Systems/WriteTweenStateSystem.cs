using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;

namespace ECS.Unity.Tween.Systems
{
    /// <summary>
    ///     Utility to write Tween State to CRDT (propagate it back to the scene)
    /// </summary>
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(TweenUpdaterSystem))]
    [LogCategory(ReportCategory.TWEEN)]
    public partial class WriteTweenStateSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public WriteTweenStateSystem(World world, IECSToCRDTWriter ecsToCRDTWriter)
            : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            ExecuteQuery(World);
            RemoveQuery(World);
        }

        [Query]
        private void Execute(ref CRDTEntity sdkEntity, ref PBTweenState tweenState)
        {
                ecsToCRDTWriter.PutMessage<PBTweenState, TweenStateStatus>(
                    static (component, tweenStateStatus) => component.State = tweenStateStatus, sdkEntity, tweenState.State);
        }

        [Query]
        private void Remove(ref CRDTEntity sdkEntity, ref RemovedComponents removedComponents)
        {
            if (removedComponents.Remove<PBTweenState>())
                ecsToCRDTWriter.DeleteMessage<PBTweenState>(sdkEntity);
        }
    }
}
