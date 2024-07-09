using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Serialization;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using DCL.SDKComponents.Tween.Helpers;
using ECS.Abstract;
using ECS.Groups;
using Google.Protobuf;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
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

            // We have to keep a copy of the tween to compare for possible changes when PBTween is not correctly dirtyed by SDK scenes
            SDKTweenComponent sdkTweenComponent = new SDKTweenComponent
            {
                IsDirty = true
            };

            World.Add(entity, sdkTweenComponent);
        }

        [Query]
        private void UpdateTween(in Entity entity, ref PBTween pbTween, ref SDKTweenComponent tweenComponent)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            if (pbTween.IsDirty)
                tweenComponent.IsDirty = true;
        }
    }
}
