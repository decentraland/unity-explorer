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

            UpdateTweenQuery(World);

            HandleComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(SDKTweenComponent))]
        private void LoadTween(in Entity entity, ref PBTween pbTweenModel, ref TransformComponent transformComponent)
        {
            if (pbTweenModel.ModeCase == PBTween.ModeOneofCase.None) return;

            bool isPlaying = !pbTweenModel.HasPlaying || pbTweenModel.Playing;
            var tweenComponent = new SDKTweenComponent(entity, false, isPlaying, pbTweenModel.CurrentTime, null, pbTweenModel.ModeCase, pbTweenModel, true);
            var tweenState = new PBTweenState();

            World.Add(entity, tweenComponent, tweenState);
        }

        [Query]
        private void UpdateTween(ref PBTween pbTween, ref SDKTweenComponent sdkTweenComponent, ref TransformComponent transformComponent)
        {

            if (!AreSameModels(pbTween, sdkTweenComponent.currentTweenModel))
            {
                sdkTweenComponent.currentTweenModel = pbTween;
                sdkTweenComponent.dirty = true;
            }

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

        private static bool AreSameModels(PBTween modelA, PBTween modelB)
        {
            if (modelB == null || modelA == null)
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
