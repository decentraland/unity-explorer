using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.Tween.Components;
using DCL.SDKComponents.Tween.Helpers;
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

        public TweenLoaderSystem(World world, IComponentPool<SDKTweenComponent> sdkTweenComponentPool) : base(world)
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

            if (pbTween.IsDirty || !TweenSDKComponentHelper.AreSameModels(pbTween, tweenComponent.SDKTweenComponent.CurrentTweenModel))
            {
                tweenComponent.SDKTweenComponent.CurrentTweenModel.Update(pbTween);
                tweenComponent.SDKTweenComponent.IsDirty = true;
            }
        }
    }
}
