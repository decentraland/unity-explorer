using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Utility;
using DCL.Interaction.Utility;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine.InputSystem;
using Utility.Multithreading;
using InputAction = DCL.ECSComponents.InputAction;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.Interaction.PlayerOriginated.Tests
{
    public class HoverFeedbackUtilsShould : InputTestFixture
    {
        private World world;

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
                AppendPointerEventResultsIntent = new AppendPointerEventResultsIntent(),
                PointerEvents =
                {
                    CreateEntry(PointerEventType.PetHoverLeave, InputAction.IaPointer),
                    CreateEntry(PointerEventType.PetHoverEnter, InputAction.IaAny),
                },
            };

            previousColliderSceneInfo.EcsExecutor.World.Add(previousColliderSceneInfo.ColliderSceneEntityInfo.EntityReference, pbPointerEvents);

            PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities = GetRaycastAt(99);

            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResultForSceneEntities, in previousColliderSceneInfo);

            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndices.Length, Is.EqualTo(1));
            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndices[0], Is.EqualTo(0));
        }

        [Test]
        public void NotIssueHoverLeaveIfOutOfRange()
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

            PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities = GetRaycastAt(150);

            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResultForSceneEntities, in previousColliderSceneInfo);

            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndices.Length, Is.EqualTo(0));
        }

        [Test]
        public void NotIssueHoverLeaveIfComponentWasRemoved()
        {
            GlobalColliderSceneEntityInfo previousColliderSceneInfo = CreateColliderInfo();

            // Don't add PBPointerEvents component

            PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities = GetRaycastAt(50);

            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResultForSceneEntities, in previousColliderSceneInfo);

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

            PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities = GetRaycastAt(50);

            world.Destroy(previousColliderSceneInfo.ColliderSceneEntityInfo.EntityReference);

            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResultForSceneEntities, in previousColliderSceneInfo);

            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndices.Length, Is.EqualTo(0));
        }

        private GlobalColliderSceneEntityInfo CreateColliderInfo() =>
            new (new SceneEcsExecutor(world),
                new ColliderSceneEntityInfo(world.Reference(world.Create(new CRDTEntity(123))), 123, ColliderLayer.ClPhysics));

        private static PlayerOriginRaycastResultForSceneEntities GetRaycastAt(float distance)
        {
            var raycastResult = new PlayerOriginRaycastResultForSceneEntities();
            raycastResult.SetupHit(new RaycastHit { distance = distance }, default(GlobalColliderSceneEntityInfo), distance);
            return raycastResult;
        }
    }
}
