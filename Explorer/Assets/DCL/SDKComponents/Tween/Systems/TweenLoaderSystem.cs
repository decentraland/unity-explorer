using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CrdtEcsBridge.Components.Transform;
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
        public TweenLoaderSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            UpdateTweenQuery(World);
            LoadTweenQuery(World);
        }

        [Query]
        [None(typeof(SDKTweenComponent))]
        private void LoadTween(in Entity entity, ref PBTween pbTween)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            SDKTweenComponent sdkTweenComponent = new SDKTweenComponent
                {
                IsDirty = true, HelperSDKTransform = new SDKTransform()
                };

            World.Add(entity, sdkTweenComponent);
        }

        [Query]
        private void UpdateTween(ref PBTween pbTween, ref SDKTweenComponent tweenComponent)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            if (pbTween.IsDirty)
                tweenComponent.IsDirty = true;

            //TODO: Juani: Do we need the AreSameModels? Should the dirty flag be enough?
            /*if (pbTween.IsDirty || !TweenSDKComponentHelper.AreSameModels(pbTween, tweenComponent.CurrentTweenModel))
            {
                tweenComponent.CurrentTweenModel = new SDKTweenModel(pbTween);
                tweenComponent.IsDirty = true;
            }*/
        }
    }
}
