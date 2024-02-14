using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using DG.Tweening;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using ECS.Unity.Tween.Components;

namespace ECS.Unity.Tween.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.TWEEN)]
    [ThrottlingEnabled]
    public partial class TweenLoaderSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private Sequence currentTweener;

        public TweenLoaderSystem(World world) : base(world)
        { }

        protected override void Update(float t)
        {
            UpdateTweenQuery(World);
            LoadTweenQuery(World);

            HandleComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(SDKTweenComponent))]
        private void LoadTween(in Entity entity, ref PBTween pbTween)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            var tweenComponent = new SDKTweenComponent
                {
                    IsDirty = true,
                };
            tweenComponent.CurrentTweenModel.Update(pbTween);

            World.Add(entity, tweenComponent);
        }

        [Query]
        private void UpdateTween(ref PBTween pbTween, ref SDKTweenComponent sdkTweenComponent)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            if (pbTween.IsDirty || !AreSameModels(pbTween, sdkTweenComponent.CurrentTweenModel))
            {
                sdkTweenComponent.CurrentTweenModel.Update(pbTween);
                sdkTweenComponent.IsDirty = true;
            }
        }

        [Query]
        [None(typeof(PBTween), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(in Entity entity, ref SDKTweenComponent tweenComponent)
        {
            tweenComponent.Removed = true;
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(in Entity entity, ref SDKTweenComponent sdkTweenComponent)
        {
           // World.Remove<SDKTweenComponent>(entity);
            //globalWorld.Add(sdkTweenComponent.globalWorldEntity, new DeleteEntityIntention());
        }

        [Query]
        public void FinalizeComponents(ref SDKTweenComponent sdkTweenComponent)
        {
            //globalWorld.Add(sdkTweenComponent.globalWorldEntity, new DeleteEntityIntention());
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

        private static bool AreSameModels(PBTween modelA, SDKTweenModel modelB)
        {
            if (modelA == null)
                return false;

            if (modelB.ModeCase != modelA.ModeCase
                || modelB.EasingFunction != modelA.EasingFunction
                || !modelB.CurrentTime.Equals(modelA.CurrentTime)
                || !modelB.Duration.Equals(modelA.Duration))
                return false;

            return modelA.ModeCase switch
                   {
                       PBTween.ModeOneofCase.Scale => modelB.Scale.Start.Equals(modelA.Scale.Start) && modelB.Scale.End.Equals(modelA.Scale.End),
                       PBTween.ModeOneofCase.Rotate => modelB.Rotate.Start.Equals(modelA.Rotate.Start) && modelB.Rotate.End.Equals(modelA.Rotate.End),
                       PBTween.ModeOneofCase.Move => modelB.Move.Start.Equals(modelA.Move.Start) && modelB.Move.End.Equals(modelA.Move.End),
                       PBTween.ModeOneofCase.None => modelB.Move.Start.Equals(modelA.Move.Start) && modelB.Move.End.Equals(modelA.Move.End),
                       _ => modelB.Move.Start.Equals(modelA.Move.Start) && modelB.Move.End.Equals(modelA.Move.End),
                   };
        }

    }
}
