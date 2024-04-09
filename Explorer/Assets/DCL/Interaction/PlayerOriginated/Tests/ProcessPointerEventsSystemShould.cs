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


        public void CreateWorld()
        {
            world = World.Create();
        }


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


        public void IssueHoverLeaveForPreviousEntity()
        {
            GlobalColliderEntityInfo previousColliderInfo = CreateColliderInfo();

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

            previousColliderInfo.EcsExecutor.World.Add(previousColliderInfo.ColliderEntityInfo.EntityReference, pbPointerEvents);

            var raycastResult = new PlayerOriginRaycastResult
            {
                UnityRaycastHit = new RaycastHit
                    { distance = 99 },
            };

            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResult, in previousColliderInfo);

            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndices.Length, Is.EqualTo(1));
            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndices[0], Is.EqualTo(0));
        }


        public void NotIssueHoverLeaveIfOutOfRange()
        {
            GlobalColliderEntityInfo previousColliderInfo = CreateColliderInfo();

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

            previousColliderInfo.EcsExecutor.World.Add(previousColliderInfo.ColliderEntityInfo.EntityReference, pbPointerEvents);

            var raycastResult = new PlayerOriginRaycastResult
            {
                UnityRaycastHit = new RaycastHit
                    { distance = 150 },
            };

            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResult, in previousColliderInfo);

            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndices.Length, Is.EqualTo(0));
        }


        public void NotIssueHoverLeaveIfComponentWasRemoved()
        {
            GlobalColliderEntityInfo previousColliderInfo = CreateColliderInfo();

            // Don't add PBPointerEvents component

            var raycastResult = new PlayerOriginRaycastResult
            {
                UnityRaycastHit = new RaycastHit
                    { distance = 50 },
            };

            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResult, in previousColliderInfo);

            // Nothing to assert, just checking that no exception is thrown
        }


        public void NotIssueHoverLeaveIfEntityDied()
        {
            GlobalColliderEntityInfo previousColliderInfo = CreateColliderInfo();

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

            previousColliderInfo.EcsExecutor.World.Add(previousColliderInfo.ColliderEntityInfo.EntityReference, pbPointerEvents);

            var raycastResult = new PlayerOriginRaycastResult
            {
                UnityRaycastHit = new RaycastHit
                    { distance = 50 },
            };

            world.Destroy(previousColliderInfo.ColliderEntityInfo.EntityReference);

            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResult, in previousColliderInfo);

            Assert.That(pbPointerEvents.AppendPointerEventResultsIntent.ValidIndices.Length, Is.EqualTo(0));
        }

        private GlobalColliderEntityInfo CreateColliderInfo() =>
            new (new SceneEcsExecutor(world, new MutexSync()),
                new ColliderEntityInfo(world.Reference(world.Create(new CRDTEntity(123))), 123, ColliderLayer.ClPhysics));
    }
}
