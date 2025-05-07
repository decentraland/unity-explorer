using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.TWEEN)]
    public partial class TweenCleanUpSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly TweenerPool tweenerPool;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public TweenCleanUpSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, TweenerPool tweenerPool) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.tweenerPool = tweenerPool;
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref SDKTweenComponent tweenComponent, CRDTEntity sdkEntity)
        {
            CleanUpTweenBeforeRemoval(sdkEntity, ref tweenComponent);
        }

        [Query]
        [None(typeof(PBTween), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref SDKTweenComponent tweenComponent, CRDTEntity sdkEntity)
        {
            CleanUpTweenBeforeRemoval(sdkEntity, ref tweenComponent);
            World.Remove<SDKTweenComponent>(entity);
        }

        [Query]
        [All(typeof(SDKTweenComponent))]
        private void FinalizeComponents(CRDTEntity sdkEntity, ref SDKTweenComponent tweenComponent)
        {
            CleanUpTweenBeforeRemoval(sdkEntity, ref tweenComponent);
        }

        private void CleanUpTweenBeforeRemoval(CRDTEntity sdkEntity, ref SDKTweenComponent sdkTweenComponent)
        {
            tweenerPool.ReleaseCustomTweenerFrom(sdkTweenComponent);
            ecsToCRDTWriter.DeleteMessage<PBTweenState>(sdkEntity);
        }
    }
}
