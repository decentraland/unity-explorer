using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.Tween.Components;
using ECS.Abstract;
using ECS.Unity.Groups;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.TWEEN)]
    [ThrottlingEnabled]
    public partial class TweenLoaderSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<SDKTweenComponent> sdkTweenComponentPool;

        private TweenLoaderSystem(World world, IComponentPool<SDKTweenComponent> sdkTweenComponentPool) : base(world)
        {
            this.sdkTweenComponentPool = sdkTweenComponentPool;
        }

        protected override void Update(float t)
        {
            UpdateTweenQuery(World);
            LoadTweenQuery(World);
        }

        [Query]
        [None(typeof(TweenComponent))]
        private void LoadTween(in Entity entity, ref PBTween pbTween)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            var tweenComponent = new TweenComponent();

            SDKTweenComponent sdkTweenComponent = sdkTweenComponentPool.Get();

            sdkTweenComponent.IsDirty = true;

            if (sdkTweenComponent.CurrentTweenModel == null) { sdkTweenComponent.CurrentTweenModel = new SDKTweenModel(pbTween); }
            else { sdkTweenComponent.CurrentTweenModel.Update(pbTween); }

            tweenComponent.SDKTweenComponent = sdkTweenComponent;
            World.Add(entity, tweenComponent);
        }

        [Query]
        private void UpdateTween(ref PBTween pbTween, ref TweenComponent tweenComponent)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            if (pbTween.IsDirty || !AreSameModels(pbTween, tweenComponent.SDKTweenComponent.CurrentTweenModel))
            {
                tweenComponent.SDKTweenComponent.CurrentTweenModel.Update(pbTween);
                tweenComponent.SDKTweenComponent.IsDirty = true;
            }
        }

        private static bool AreSameModels(PBTween modelA, SDKTweenModel modelB)
        {
            if (modelA == null)
                return false;

            if (modelB.ModeCase != modelA.ModeCase
                || modelB.EasingFunction != modelA.EasingFunction
                || !modelB.CurrentTime.Equals(modelA.CurrentTime)
                || !modelB.Duration.Equals(modelA.Duration)
                || !modelB.IsPlaying.Equals(!modelA.HasPlaying || modelA.Playing))
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
