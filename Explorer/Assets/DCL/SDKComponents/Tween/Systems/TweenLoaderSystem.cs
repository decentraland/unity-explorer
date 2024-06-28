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
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.TWEEN)]
    [ThrottlingEnabled]
    public partial class TweenLoaderSystem : BaseUnityLoopSystem
    {
        private readonly IObjectPool<PBTween> pbTweenPool;

        public TweenLoaderSystem(World world, IObjectPool<PBTween> tweenPool) : base(world)
        {
            pbTweenPool = tweenPool;
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

            var pbTweenCopy = pbTweenPool.Get();
            pbTweenCopy.MergeFrom(pbTween);

            // We have to keep a copy of the tween to compare for possible changes when PBTween is not correctly dirtyed by SDK scenes
            SDKTweenComponent sdkTweenComponent = new SDKTweenComponent
            {
                IsDirty = true, CachedTween = pbTweenCopy
            };

            World.Add(entity, sdkTweenComponent);
        }

        [Query]
        private void UpdateTween(ref PBTween pbTween, ref SDKTweenComponent tweenComponent)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            // (Juani & Fran): Im not happy to leave this AreSameModels check. But apparently its required as SDK might not mark the tween component as dirty.
            // Its present in the old renderer. If this was not needed, the CurrentTween field can be deleted
            if (pbTween.IsDirty || !TweenSDKComponentHelper.AreSameModels(pbTween, tweenComponent.CachedTween))
            {
                tweenComponent.CachedTween = new PBTween(pbTween);
                tweenComponent.IsDirty = true;
            }
        }
    }
}
