using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.Tween.Components;
using DCL.SDKComponents.Tween.Systems;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace DCL.SDKComponents.Tween.Tests
{

    public class TweenUpdaterSystemShould : UnitySystemTestBase<TweenUpdaterSystem>
    {
        private Entity entity;
        private PBTween pbTween;
        private TweenLoaderSystem tweenLoaderSystem;


        private const float DEFAULT_CURRENT_TIME_0 = 0f;
        private const float DEFAULT_CURRENT_TIME_1 = 1f;


        public void SetUp()
        {
            system = new TweenUpdaterSystem(world, Substitute.For<IECSToCRDTWriter>());
            var crdtEntity = new CRDTEntity(1);
            tweenLoaderSystem = new TweenLoaderSystem(world);

            var startVector = new Decentraland.Common.Vector3() { X = 0, Y = 0, Z = 0};
            var endVector = new Decentraland.Common.Vector3() { X = 10, Y = 0, Z = 0 };
            var move = new Move() { End = endVector, Start = startVector };
            pbTween = new PBTween()
            {
                CurrentTime = DEFAULT_CURRENT_TIME_0,
                Duration = 1000,
                EasingFunction = EasingFunction.EfLinear,
                IsDirty = true,
                Move = move,
                Playing = true,
            };

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);

            world.Add(entity, crdtEntity);
            world.Add(entity, pbTween);
            tweenLoaderSystem.Update(0);
            system.Update(0);
        }


        public void TearDown()
        {
            system?.Dispose();
            tweenLoaderSystem?.Dispose();
        }



        public void ChangingPBTweenCurrentTimeUpdatesTheTweenStateStatus()
        {
            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.CurrentTime.Equals(DEFAULT_CURRENT_TIME_0) &&
                              comp.TweenStateStatus == TweenStateStatus.TsActive ));

            pbTween.CurrentTime = DEFAULT_CURRENT_TIME_1;
            tweenLoaderSystem.Update(0);
            system.Update(0);

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.CurrentTime.Equals(DEFAULT_CURRENT_TIME_1) &&
                              comp.TweenStateStatus == TweenStateStatus.TsCompleted));
        }


        public void ChangingPBTweenPlayingValueUpdatesTheTweenStateStatus()
        {
            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.IsPlaying &&
                              comp.TweenStateStatus == TweenStateStatus.TsActive));

            pbTween.Playing = false;
            tweenLoaderSystem.Update(0);
            system.Update(0);

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(!comp.IsPlaying &&
                              comp.TweenStateStatus == TweenStateStatus.TsPaused));

            pbTween.Playing = true;
            tweenLoaderSystem.Update(0);
            system.Update(0);

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.IsPlaying &&
                              comp.TweenStateStatus == TweenStateStatus.TsActive));
        }

    }
}
