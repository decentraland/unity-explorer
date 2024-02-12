using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using ECS.Unity.Tween.Components;
using System;

namespace ECS.Unity.Tween.Systems
{
    /// <summary>
    ///     Utility to write Tween State to CRDT (propagate it back to the scene)
    /// </summary>
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(TweenUpdaterSystem))]
    [LogCategory(ReportCategory.TWEEN)]
    public partial class WriteTweenStateSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
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
        private void Execute(ref CRDTEntity sdkEntity, ref SDKTweenComponent tweenComponent)
        {
            if (!tweenComponent.IsTweenStateDirty) return;

            tweenComponent.IsTweenStateDirty = false;
            ecsToCRDTWriter.PutMessage<PBTweenState, TweenStateStatus>(
                static (component, tweenStateStatus) => component.State = tweenStateStatus, sdkEntity, tweenComponent.TweenStateStatus);
        }

        [Query]
        private void Remove(ref CRDTEntity sdkEntity, ref RemovedComponents removedComponents)
        {
            if (removedComponents.Remove<PBTweenState>())
                ecsToCRDTWriter.DeleteMessage<PBTweenState>(sdkEntity);
        }

        public void FinalizeComponents(in Query query)
        {
        }
    }
}
