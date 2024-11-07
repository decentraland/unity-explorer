﻿using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using DCL.SDKComponents.Tween.Systems;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using Arch.Core;
using Decentraland.Common;
using SceneRunner.Scene;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.Tween.Tests
{
    [TestFixture]
    public class TweenUpdaterSystemShould : UnitySystemTestBase<TweenUpdaterSystem>
    {

        private Entity entity;
        private PBTween pbTween;
        private TweenerPool tweneerPool;


        private const float DEFAULT_CURRENT_TIME_0 = 0f;
        private const float DEFAULT_CURRENT_TIME_1 = 1f;

        [SetUp]
        public void SetUp()
        {
            tweneerPool = new TweenerPool();
            system = new TweenUpdaterSystem(world, Substitute.For<IECSToCRDTWriter>(), tweneerPool, null, Substitute.For<SceneStateProvider>());
            var crdtEntity = new CRDTEntity(1);

            var startVector = new Vector3() { X = 0, Y = 0, Z = 0};
            var endVector = new Vector3() { X = 10, Y = 0, Z = 0 };
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

            world.Add(entity, crdtEntity, pbTween);
            system.Update(0);
        }

        [TearDown]
        public void TearDown()
        {
            system?.Dispose();
        }


        [Test]
        public void ChangingPBTweenCurrentTimeUpdatesTheTweenStateStatus()
        {
            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsActive ));

            pbTween.CurrentTime = DEFAULT_CURRENT_TIME_1;
            pbTween.IsDirty = true;
            system.Update(0);
            //We need a second update, to move the state from playing to complete.
            system.Update(0);


            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsCompleted));
        }



        [Test]
        public void ChangingPBTweenPlayingValueUpdatesTheTweenStateStatus()
        {
            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsActive));

            pbTween.Playing = false;
            pbTween.IsDirty = true;

            system.Update(0);

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsPaused));

            pbTween.Playing = true;
            pbTween.IsDirty = true;

            system.Update(0);

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsActive));
        }


    }

}
