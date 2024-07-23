using CRDT;
using CrdtEcsBridge.Components.ResetExtensions;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.Optimization.Pools;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace DCL.Interaction.PlayerOriginated.Tests
{
    public class WritePointerEventResultsSystemShould : UnitySystemTestBase<WritePointerEventResultsSystem>
    {
        private readonly List<PBPointerEventsResult> results = new ();
        private IECSToCRDTWriter writer;
        private IGlobalInputEvents globalInputEvents;
        private ISceneStateProvider sceneStateProvider;

        [SetUp]
        public void SetUp()
        {
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.Geometry.Returns(new ParcelMathHelper.SceneGeometry(Vector3.zero, new ParcelMathHelper.SceneCircumscribedPlanes()));

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.TickNumber.Returns(123u);
            sceneStateProvider.IsCurrent.Returns(true);

            IComponentPool<RaycastHit> pool = Substitute.For<IComponentPool<RaycastHit>>();
            pool.Get().Returns(new RaycastHit().Reset());

            system = new WritePointerEventResultsSystem(world, sceneData,
                writer = Substitute.For<IECSToCRDTWriter>(),
                sceneStateProvider,
                globalInputEvents = Substitute.For<IGlobalInputEvents>(),
                pool);
        }

        [TearDown]
        public void ClearResults()
        {
            results.Clear();
        }

        [Test]
        public void WriteGlobalEvents()
        {
            writer.AppendMessage(
                       Arg.Any<Action<PBPointerEventsResult, (RaycastHit, InputAction, PointerEventType, ISceneStateProvider)>>(),
                       Arg.Any<CRDTEntity>(),
                       Arg.Any<int>(),
                       Arg.Any<(RaycastHit, InputAction, PointerEventType, ISceneStateProvider)>())
                  .Returns(info =>
                   {
                       var res = new PBPointerEventsResult();

                       info.ArgAt<Action<PBPointerEventsResult, (RaycastHit, InputAction, PointerEventType, ISceneStateProvider)>>(0)
                           .Invoke(res, info.ArgAt<(RaycastHit, InputAction, PointerEventType, ISceneStateProvider)>(3));

                       results.Add(res);

                       return res;
                   });

            globalInputEvents.Entries.Returns(new List<IGlobalInputEvents.Entry>
            {
                new (InputAction.IaBackward, PointerEventType.PetUp),
                new (InputAction.IaAction6, PointerEventType.PetDown),
            });

            system.Update(0);

            Assert.That(results.Count, Is.EqualTo(2));
            PBPointerEventsResult first = results[0];
            Assert.That(first.Button, Is.EqualTo(InputAction.IaBackward));
            Assert.That(first.State, Is.EqualTo(PointerEventType.PetUp));
            Assert.That(first.TickNumber, Is.EqualTo(123u));
            Assert.That(first.Timestamp, Is.EqualTo(123u));
            Assert.That(first.Hit, Is.Null);

            PBPointerEventsResult second = results[1];
            Assert.That(second.Button, Is.EqualTo(InputAction.IaAction6));
            Assert.That(second.State, Is.EqualTo(PointerEventType.PetDown));
            Assert.That(second.TickNumber, Is.EqualTo(123u));
            Assert.That(second.Timestamp, Is.EqualTo(123u));
            Assert.That(second.Hit, Is.Null);
        }

        [Test]
        public void WriteValidResults()
        {
            var sdkEvents = new PBPointerEvents
            {
                AppendPointerEventResultsIntent = new AppendPointerEventResultsIntent(),
                PointerEvents =
                {
                    CreateEntry(PointerEventType.PetHoverEnter, InputAction.IaPointer),
                    CreateEntry(PointerEventType.PetHoverLeave, InputAction.IaForward),
                    CreateEntry(PointerEventType.PetUp, InputAction.IaAction3),
                    CreateEntry(PointerEventType.PetDown, InputAction.IaPrimary),
                },
            };

            sdkEvents.AppendPointerEventResultsIntent.ValidIndices.Add(1);
            sdkEvents.AppendPointerEventResultsIntent.ValidIndices.Add(3);

            var sdkEntity = new CRDTEntity(100);

            writer.AppendMessage(
                       Arg.Any<Action<PBPointerEventsResult, (RaycastHit, InputAction, PointerEventType, ISceneStateProvider)>>(),
                       Arg.Any<CRDTEntity>(),
                       Arg.Any<int>(),
                       Arg.Any<(RaycastHit, InputAction, PointerEventType, ISceneStateProvider)>())
                  .Returns(info =>
                   {
                       var res = new PBPointerEventsResult();

                       info.ArgAt<Action<PBPointerEventsResult, (RaycastHit, InputAction, PointerEventType, ISceneStateProvider)>>(0)
                           .Invoke(res, info.ArgAt<(RaycastHit, InputAction, PointerEventType, ISceneStateProvider)>(3));

                       results.Add(res);

                       return res;
                   });

            world.Create(sdkEntity, sdkEvents);

            system.Update(0);

            AssertMessageAppended(1, 0);
            AssertMessageAppended(3, 1);

            void AssertMessageAppended(int originalIndex, uint counter)
            {
                PBPointerEvents.Types.Entry entry = sdkEvents.PointerEvents[originalIndex];
                PBPointerEventsResult r = results[(int)counter];

                Assert.That(r.Button, Is.EqualTo(entry.EventInfo.Button));
                Assert.That(r.State, Is.EqualTo(entry.EventType));
                Assert.That(r.TickNumber, Is.EqualTo(123u));
                Assert.That(r.Timestamp, Is.EqualTo(123u));
                Assert.That(r.Hit, Is.Not.Null);
            }

            Assert.That(writer.ReceivedCalls().Count(), Is.EqualTo(2));
        }

        [Test]
        public void WriteEventsOnlyForCurrentScene()
        {
            sceneStateProvider.IsCurrent.Returns(false);

            writer.AppendMessage(
                       Arg.Any<Action<PBPointerEventsResult, (RaycastHit, InputAction, PointerEventType, ISceneStateProvider)>>(),
                       Arg.Any<CRDTEntity>(),
                       Arg.Any<int>(),
                       Arg.Any<(RaycastHit, InputAction, PointerEventType, ISceneStateProvider)>())
                  .Returns(info =>
                   {
                       var res = new PBPointerEventsResult();

                       info.ArgAt<Action<PBPointerEventsResult, (RaycastHit, InputAction, PointerEventType, ISceneStateProvider)>>(0)
                           .Invoke(res, info.ArgAt<(RaycastHit, InputAction, PointerEventType, ISceneStateProvider)>(3));

                       results.Add(res);

                       return res;
                   });

            globalInputEvents.Entries.Returns(new List<IGlobalInputEvents.Entry>
            {
                new (InputAction.IaBackward, PointerEventType.PetUp),
                new (InputAction.IaAction6, PointerEventType.PetDown),
            });

            system.Update(0);

            Assert.That(results.Count, Is.EqualTo(0));

            sceneStateProvider.IsCurrent.Returns(true);

            system.Update(0);

            Assert.That(results.Count, Is.EqualTo(2));
        }

        private static PBPointerEvents.Types.Entry CreateEntry(PointerEventType eventType, InputAction button) =>
            new ()
            {
                EventType = eventType,
                EventInfo = new PBPointerEvents.Types.Info
                {
                    Button = button,
                    MaxDistance = 100,
                },
            };
    }
}
