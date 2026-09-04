using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Utility;
using DCL.Interaction.Utility;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine.InputSystem;
using InputAction = DCL.ECSComponents.InputAction;

namespace DCL.Interaction.PlayerOriginated.Tests
{
    public class HoverFeedbackUtilsShould : InputTestFixture
    {
        private World world = null!;

        [SetUp]
        public void CreateWorld()
        {
            world = World.Create();
        }

        [TearDown]
        public void DestroyWorld()
        {
            world.Dispose();
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

        [Test]
        public void IssueHoverLeaveForPreviousEntity()
        {
            GlobalColliderSceneEntityInfo previousColliderSceneInfo = CreateColliderInfo();

            // Add PBPointerEvents component
            var pbPointerEvents = new PBPointerEvents
            {
                PointerEvents =
                {
                    CreateEntry(PointerEventType.PetHoverLeave, InputAction.IaPointer),
                    CreateEntry(PointerEventType.PetHoverEnter, InputAction.IaAny),
                },
            };

            pbPointerEvents.AppendPointerEventResultsIntent.InitializeWithAlloc();

            previousColliderSceneInfo.EcsExecutor.World.Add(previousColliderSceneInfo.ColliderSceneEntityInfo.EntityReference, pbPointerEvents);

            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in previousColliderSceneInfo, previousHoverEnterIssued: true);

            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndicesCount(), Is.EqualTo(1));
            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndexAt(0), Is.EqualTo(0));
        }

        /// <summary>
        ///     The leave completes an enter that was already issued, so the ray of the frame the hover ended on —
        ///     which points somewhere else entirely — must not gate it. Regression: a target with a tight
        ///     maxDistance used to keep a hover the scene could never see end.
        /// </summary>
        [Test]
        public void IssueHoverLeaveEvenAfterTheReticleMovedOutOfTheTargetRange()
        {
            GlobalColliderSceneEntityInfo previousColliderSceneInfo = CreateColliderInfo();

            var pbPointerEvents = new PBPointerEvents
            {
                PointerEvents =
                {
                    CreateEntry(PointerEventType.PetHoverLeave, InputAction.IaPointer),
                    CreateEntry(PointerEventType.PetHoverEnter, InputAction.IaAny),
                },
            };

            pbPointerEvents.AppendPointerEventResultsIntent.InitializeWithAlloc();

            previousColliderSceneInfo.EcsExecutor.World.Add(previousColliderSceneInfo.ColliderSceneEntityInfo.EntityReference, pbPointerEvents);

            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in previousColliderSceneInfo, previousHoverEnterIssued: true);

            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndicesCount(), Is.EqualTo(1));
            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndexAt(0), Is.EqualTo(0));
        }

        [Test]
        public void NotIssueHoverLeaveIfTheHoverWasNeverQualified()
        {
            GlobalColliderSceneEntityInfo previousColliderSceneInfo = CreateColliderInfo();

            var pbPointerEvents = new PBPointerEvents
            {
                AppendPointerEventResultsIntent = new AppendPointerEventResultsIntent(),
                PointerEvents =
                {
                    CreateEntry(PointerEventType.PetHoverLeave, InputAction.IaPointer),
                    CreateEntry(PointerEventType.PetHoverEnter, InputAction.IaAny),
                },
            };

            previousColliderSceneInfo.EcsExecutor.World.Add(previousColliderSceneInfo.ColliderSceneEntityInfo.EntityReference, pbPointerEvents);

            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in previousColliderSceneInfo, previousHoverEnterIssued: false);

            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndicesCount(), Is.EqualTo(0));
        }

        [Test]
        public void NotIssueHoverLeaveIfComponentWasRemoved()
        {
            GlobalColliderSceneEntityInfo previousColliderSceneInfo = CreateColliderInfo();

            // Don't add PBPointerEvents component

            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in previousColliderSceneInfo, previousHoverEnterIssued: true);

            // Nothing to assert, just checking that no exception is thrown
        }

        [Test]
        public void NotIssueHoverLeaveIfEntityDied()
        {
            GlobalColliderSceneEntityInfo previousColliderSceneInfo = CreateColliderInfo();

            // Add PBPointerEvents component
            var pbPointerEvents = new PBPointerEvents
            {
                AppendPointerEventResultsIntent = new AppendPointerEventResultsIntent(),
                PointerEvents =
                {
                    CreateEntry(PointerEventType.PetHoverLeave, InputAction.IaPointer),
                    CreateEntry(PointerEventType.PetHoverEnter, InputAction.IaAny),
                },
            };

            previousColliderSceneInfo.EcsExecutor.World.Add(previousColliderSceneInfo.ColliderSceneEntityInfo.EntityReference, pbPointerEvents);

            world.Destroy(previousColliderSceneInfo.ColliderSceneEntityInfo.EntityReference);

            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in previousColliderSceneInfo, previousHoverEnterIssued: true);

            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndicesCount(), Is.EqualTo(0));
        }

        private GlobalColliderSceneEntityInfo CreateColliderInfo() =>
            new (new SceneEcsExecutor(world),
                new ColliderSceneEntityInfo(world.Create(new CRDTEntity(123)), 123, ColliderLayer.ClPhysics));
    }
}
