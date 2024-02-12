using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using Collections.Pooled;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using DCL.Utilities;
using DG.Tweening;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Tween.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.Unity.Tween.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.TWEEN)]
    [ThrottlingEnabled]
    public partial class TweenLoaderSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
       private readonly WorldProxy globalWorld;
        private Sequence currentTweener;

        public TweenLoaderSystem(World world, WorldProxy globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            LoadTweenQuery(World);
            LoadTweenSequenceQuery(World);

            UpdateTweenQuery(World);

            HandleComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(SDKTweenComponent), typeof(PBTweenSequence))]
        private void LoadTween(in Entity entity, ref PBTween pbTweenModel, ref TransformComponent transformComponent)
        {
            if (pbTweenModel.ModeCase == PBTween.ModeOneofCase.None) return;

            bool isPlaying = !pbTweenModel.HasPlaying || pbTweenModel.Playing;
            var tweenComponent = new SDKTweenComponent(entity, false, isPlaying, pbTweenModel.CurrentTime, transformComponent.Transform, null, pbTweenModel.ModeCase, pbTweenModel, true, null);
            var tweenState = new PBTweenState();

            World.Add(entity, tweenComponent, tweenState);
        }


        [Query]
        [None(typeof(SDKTweenComponent))]
        private void LoadTweenSequence(in Entity entity, ref PBTween pbTweenModel, ref PBTweenSequence pbTweenSequence, ref TransformComponent transformComponent)
        {
            if (pbTweenModel.ModeCase == PBTween.ModeOneofCase.None) return;

            bool isPlaying = !pbTweenModel.HasPlaying || pbTweenModel.Playing;
            var tweenComponent = new SDKTweenComponent(entity, false, isPlaying, pbTweenModel.CurrentTime, transformComponent.Transform, null, pbTweenModel.ModeCase, pbTweenModel, true, pbTweenSequence);
            var tweenState = new PBTweenState();

            World.Add(entity, tweenComponent, tweenState);
        }

        [Query]
        private void UpdateTween(ref PBTween pbTween, ref SDKTweenComponent sdkTweenComponent, ref TransformComponent transformComponent)
        {
            if (!pbTween.IsDirty)
               return;

            //check if pbTween data changed, if so, mark SDK as dirty and update its values
        }



        [Query]
        [None(typeof(PBTween), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(in Entity entity, ref SDKTweenComponent tweenComponent)
        {
            // If the component is removed at scene-world, the global-world representation should disappear entirely
            globalWorld.Add(tweenComponent.globalWorldEntity, new DeleteEntityIntention());

            World.Remove<SDKTweenComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(in Entity entity, ref SDKTweenComponent sdkTweenComponent)
        {
            World.Remove<SDKTweenComponent>(entity);
            globalWorld.Add(sdkTweenComponent.globalWorldEntity, new DeleteEntityIntention());
        }

        [Query]
        public void FinalizeComponents(ref SDKTweenComponent sdkTweenComponent)
        {
            globalWorld.Add(sdkTweenComponent.globalWorldEntity, new DeleteEntityIntention());
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

    }
}
