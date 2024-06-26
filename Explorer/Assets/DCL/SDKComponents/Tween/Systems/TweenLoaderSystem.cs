using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
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
                IsDirty = true, CachedTween = new PBTween(pbTween)
            };

            World.Add(entity, sdkTweenComponent);
        }

        [Query]
        private void UpdateTween(in Entity enitity, ref PBTween pbTween, ref SDKTweenComponent tweenComponent)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            // (Juani & Fran): Im not happy to leaave this AreSameModels check. But apparently its required as SDK might not mark the tween component as dirty.
            // Its present in the old renderer. If this was not needed, the CurrentTween field can be deleted
            if (pbTween.IsDirty || !TweenSDKComponentHelper.AreSameModels(pbTween, tweenComponent.CachedTween))
            {
                tweenComponent.CachedTween = pbTween;
                tweenComponent.IsDirty = true;
            }
        }
    }
}