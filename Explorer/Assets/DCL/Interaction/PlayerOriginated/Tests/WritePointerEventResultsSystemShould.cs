using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Systems;
using ECS.ComponentsPooling;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Linq;

namespace DCL.Interaction.PlayerOriginated.Tests
{
    public class WritePointerEventResultsSystemShould : UnitySystemTestBase<WritePointerEventResultsSystem>
    {
        private IECSToCRDTWriter writer;

        [SetUp]
        public void SetUp()
        {
            Entity rootEntity = world.Create(new SceneRootComponent());
            AddTransformToEntity(rootEntity);

            IComponentPool<RaycastHit> raycastHitPool = Substitute.For<IComponentPool<RaycastHit>>();
            IComponentPool<PBPointerEventsResult> pointerEventsResultsPool = Substitute.For<IComponentPool<PBPointerEventsResult>>();

            raycastHitPool.Get().Returns(_ => new RaycastHit().Reset());
            pointerEventsResultsPool.Get().Returns(_ => new PBPointerEventsResult());

            ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.TickNumber.Returns(123u);

            system = new WritePointerEventResultsSystem(world, rootEntity,
                writer = Substitute.For<IECSToCRDTWriter>(),
                raycastHitPool,
                pointerEventsResultsPool,
                sceneStateProvider);
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

            world.Create(sdkEntity, sdkEvents);

            system.Update(0);

            AssertMessageAppended(1, 0);
            AssertMessageAppended(3, 1);

            void AssertMessageAppended(int originalIndex, uint counter)
            {
                PBPointerEvents.Types.Entry entry = sdkEvents.PointerEvents[originalIndex];

                writer.Received()
                      .AppendMessage(sdkEntity, Arg.Is<PBPointerEventsResult>(t =>
                           t.Button == entry.EventInfo.Button &&
                           t.State == entry.EventType &&
                           t.Timestamp == counter &&
                           t.TickNumber == 123u &&
                           t.Hit != null));
            }

            Assert.That(writer.ReceivedCalls().Count(), Is.EqualTo(2));
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
