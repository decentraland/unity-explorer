using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
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
    public partial class WriteTweenDataSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public WriteTweenDataSystem(World world, IECSToCRDTWriter ecsToCRDTWriter)
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
        private void Execute(ref CRDTEntity sdkEntity, ref SDKTweenComponent tweenComponent, ref TransformComponent transform)
        {
            if (!tweenComponent.IsTweenStateDirty) return;

            WriteTweenState(sdkEntity, ref tweenComponent);

            WriteTweenTransform(sdkEntity, transform);
        }

        private void WriteTweenState(CRDTEntity sdkEntity, ref SDKTweenComponent tweenComponent)
        {
            tweenComponent.IsTweenStateDirty = false;
            ecsToCRDTWriter.PutMessage<PBTweenState, TweenStateStatus>(
                static (component, tweenStateStatus) => component.State = tweenStateStatus, sdkEntity, tweenComponent.TweenStateStatus);
        }

        private void WriteTweenTransform(CRDTEntity sdkEntity, TransformComponent transform)
        {
            ecsToCRDTWriter.PutMessage<SDKTransform, TransformComponent>(
                static (component, tweenStateStatus) =>
                {
                    component.IsDirty = true;
                    component.Position = tweenStateStatus.Transform.localPosition;
                    component.Rotation = tweenStateStatus.Transform.localRotation;
                    component.Scale = tweenStateStatus.Transform.localScale;
                }, sdkEntity, transform);
        }

        [Query]
        private void Remove(ref CRDTEntity sdkEntity, ref RemovedComponents removedComponents)
        {
            if (removedComponents.Remove<PBTween>())
                ecsToCRDTWriter.DeleteMessage<PBTweenState>(sdkEntity);
        }
    }
}
