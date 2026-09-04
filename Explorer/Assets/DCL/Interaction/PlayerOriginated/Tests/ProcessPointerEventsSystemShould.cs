using Arch.Core;
using CRDT;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.Input;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Systems;
using DCL.Interaction.Utility;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using InputAction = DCL.ECSComponents.InputAction;
using PointerEventType = DCL.ECSComponents.PointerEventType;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.Interaction.PlayerOriginated.Tests
{
    public class ProcessPointerEventsSystemShould : InputTestFixture
    {
        private const int TARGET_CRDT_ID = 640;

        private World world = null!;
        private World sceneWorld = null!;
        private Entity pipelineEntity;
        private Entity targetEntity;

        private GameObject cameraGo = null!;
        private GameObject targetGo = null!;
        private BoxCollider targetCollider = null!;

        private ProcessPointerEventsSystem system = null!;
        private UnityEngine.InputSystem.InputAction primaryAction = null!;

        [SetUp]
        public void SetUp()
        {
            base.Setup();

            world = World.Create();
            sceneWorld = World.Create();

            cameraGo = new GameObject("pointer-events-test-camera");
            world.Create(new CameraComponent(cameraGo.AddComponent<Camera>()));

            InputSystem.AddDevice<Keyboard>();
            primaryAction = new UnityEngine.InputSystem.InputAction(binding: "<Keyboard>/e");
            primaryAction.Enable();

            var actionsMap = new Dictionary<InputAction, UnityEngine.InputSystem.InputAction>
            {
                { InputAction.IaPrimary, primaryAction },
            };

            pipelineEntity = world.Create(
                new SyntheticPointerInput(),
                new PlayerOriginRaycastResultForSceneEntities(),
                new ProximityResultForSceneEntities(),
                new HoverStateComponent(),
                new HoverFeedbackComponent(4));

            targetGo = new GameObject("pointer-events-test-target")
                {
                    transform = { position = new Vector3(0f, 0f, 5f) },
                };

            targetCollider = targetGo.AddComponent<BoxCollider>();

            var targetPointerEvents = new PBPointerEvents
            {
                PointerEvents = { Entry(PointerEventType.PetDown, InputAction.IaPrimary, 10f) },
            };

            targetPointerEvents.AppendPointerEventResultsIntent.InitializeWithAlloc();
            targetEntity = sceneWorld.Create(targetPointerEvents, new CRDTEntity(TARGET_CRDT_ID));

            IEventSystem eventSystem = Substitute.For<IEventSystem>();
            eventSystem.IsPointerOverGameObject().Returns(false);

            // The entity hovered on the previous frame is resolved through the colliders cache, so the leave path
            // needs the cache to know the target's collider.
            IEntityCollidersGlobalCache collidersCache = Substitute.For<IEntityCollidersGlobalCache>();

            collidersCache.TryGetSceneEntity(Arg.Any<Collider>(), out Arg.Any<GlobalColliderSceneEntityInfo>())
                          .Returns(call =>
                           {
                               if (call.ArgAt<Collider>(0) != targetCollider)
                                   return false;

                               call[1] = TargetEntityInfo();
                               return true;
                           });

            system = new ProcessPointerEventsSystem(world, actionsMap, collidersCache, eventSystem);
            system.Initialize();
        }

        [TearDown]
        public void DisposeWorlds()
        {
            primaryAction.Disable();
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(targetGo);
            world.Dispose();
            sceneWorld.Dispose();
        }

        private static PBPointerEvents.Types.Entry Entry(PointerEventType eventType, InputAction button, float maxDistance) =>
            new ()
            {
                EventType = eventType,
                EventInfo = new PBPointerEvents.Types.Info
                {
                    Button = button,
                    MaxDistance = maxDistance,
                },
            };

        private GlobalColliderSceneEntityInfo TargetEntityInfo() =>
            new (new SceneEcsExecutor(sceneWorld),
                new ColliderSceneEntityInfo(targetEntity, new CRDTEntity(TARGET_CRDT_ID), ColliderLayer.ClPointer));

        /// <summary>Replaces the pointer events the target declares.</summary>
        private void DeclarePointerEvents(params PBPointerEvents.Types.Entry[] entries)
        {
            PBPointerEvents pointerEvents = sceneWorld.Get<PBPointerEvents>(targetEntity);
            pointerEvents.PointerEvents.Clear();

            foreach (PBPointerEvents.Types.Entry entry in entries)
                pointerEvents.PointerEvents.Add(entry);
        }

        private AppendPointerEventResultsIntent TargetIntent() =>
            sceneWorld.Get<PBPointerEvents>(targetEntity).AppendPointerEventResultsIntent;

        /// <summary>
        ///     Points the pipeline ray at the target through a real raycast, so the hit carries a live collider.
        ///     <paramref name="distance" /> overrides the distance the qualification reads off the hit.
        /// </summary>
        private void HoverTarget(float? distance = null)
        {
            Physics.SyncTransforms();

            var ray = new Ray(Vector3.zero, Vector3.forward);
            Assert.That(Physics.Raycast(ray, out RaycastHit hit, 100f), Is.True, "test precondition: the ray must hit the target collider");
            Assert.That(hit.collider, Is.EqualTo(targetCollider), "test precondition: the ray must hit the target collider");

            ref PlayerOriginRaycastResultForSceneEntities raycastResult = ref world.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity);
            raycastResult.SetRay(ray);
            raycastResult.SetupHit(hit, TargetEntityInfo(), distance ?? hit.distance, distance ?? hit.distance);
        }

        /// <summary>The ray reaches nothing this frame, so whatever was hovered is left behind.</summary>
        private void LookAway() =>
            world.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity).Reset();

        private void PostSyntheticPress(InputAction button)
        {
            world.Get<SyntheticPointerInput>(pipelineEntity) = new SyntheticPointerInput
            {
                AimPoint = targetGo.transform.position,
                PressButton = button,
                PostedAtFrame = UnityEngine.Time.frameCount,
            };
        }

        /// <summary>A press restricted to one entity, the way a driver that named an entity posts it.</summary>
        private void PostSyntheticPressTargeting(InputAction button, World targetWorld, Entity target)
        {
            world.Get<SyntheticPointerInput>(pipelineEntity) = new SyntheticPointerInput
            {
                AimPoint = targetGo.transform.position,
                PressButton = button,
                TargetWorld = targetWorld,
                TargetEntity = target,
                PostedAtFrame = UnityEngine.Time.frameCount,
            };
        }

        [Test]
        public void MergeAnUntargetedSyntheticEdgeIntoTheHoveredEntity()
        {
            HoverTarget();
            PostSyntheticPress(InputAction.IaPrimary);

            system.Update(0);

            // An aim that names no entity accepts whatever the ray reached: the edge lands on it like a real key.
            Assert.That(TargetIntent().ValidInputActions,
                Is.EqualTo(new[] { (InputAction.IaPrimary, PointerEventType.PetDown) }),
                "the edge must land entity-bound on the hovered entity");
        }

        [Test]
        public void MergeASyntheticEdgeIntoTheEntityItNamed()
        {
            HoverTarget();
            PostSyntheticPressTargeting(InputAction.IaPrimary, sceneWorld, targetEntity);

            system.Update(0);

            Assert.That(TargetIntent().ValidInputActions,
                Is.EqualTo(new[] { (InputAction.IaPrimary, PointerEventType.PetDown) }),
                "the ray reached the entity the edge was promised to, so it must be delivered");
        }

        [Test]
        public void NotMergeASyntheticEdgeIntoAnEntityItDidNotName()
        {
            // The ray reached the target, but the edge was promised to another entity: whatever the ray found is
            // an occluder from the driver's point of view, and firing its handler is the delivery that used to
            // happen a frame before the driver was told its aim was blocked.
            Entity otherEntity = sceneWorld.Create(new CRDTEntity(TARGET_CRDT_ID + 1));

            HoverTarget();
            PostSyntheticPressTargeting(InputAction.IaPrimary, sceneWorld, otherEntity);

            system.Update(0);

            Assert.That(TargetIntent().ValidInputActions, Is.Empty,
                "no button edge may reach an entity the post did not name");
        }

        [Test]
        public void StillIssueHoverEnterToTheEntityTheRayReached()
        {
            Entity otherEntity = sceneWorld.Create(new CRDTEntity(TARGET_CRDT_ID + 1));

            HoverTarget();
            PostSyntheticPressTargeting(InputAction.IaPrimary, sceneWorld, otherEntity);

            system.Update(0);

            // Only the button edge is withheld. Hover follows the ray for real input too, and a human's cursor
            // passing over an occluder produces exactly this enter.
            Assert.That(TargetIntent().ValidIndicesCount(), Is.EqualTo(1),
                "the hover entry of the entity under the ray must still be appended");
        }

        [Test]
        public void NotMergeASyntheticEdgeWhoseTargetBelongsToAnotherWorld()
        {
            // Arch entity ids are unique per world, and every loaded scene shares one physics scene, so the
            // filter is keyed on (world, entity) — matching the id alone would deliver across scenes.
            World otherWorld = World.Create();

            try
            {
                HoverTarget();
                PostSyntheticPressTargeting(InputAction.IaPrimary, otherWorld, targetEntity);

                system.Update(0);

                Assert.That(TargetIntent().ValidInputActions, Is.Empty,
                    "an entity of the same id in another world is not the named target");
            }
            finally
            {
                World.Destroy(otherWorld);
            }
        }

        /// <summary>
        ///     The entity's qualification for button input follows the last entry iterated; the enter is appended
        ///     per qualified entry. A tight-range entry declared last must not hide the enter an earlier entry
        ///     issued, or the leave completing it is never sent and the scene keeps the hover forever.
        /// </summary>
        [Test]
        public void IssueTheHoverLeaveWhenOnlyAnEarlierEntryQualified()
        {
            DeclarePointerEvents(
                Entry(PointerEventType.PetHoverEnter, InputAction.IaPointer, 100f),
                Entry(PointerEventType.PetHoverLeave, InputAction.IaPointer, 100f),
                Entry(PointerEventType.PetDown, InputAction.IaPrimary, 1f));

            HoverTarget();
            system.Update(0);

            AppendPointerEventResultsIntent afterEnter = TargetIntent();
            Assert.That(afterEnter.ValidIndicesCount(), Is.EqualTo(1), "the enter of the entry in range is issued");
            Assert.That(afterEnter.ValidIndexAt(0), Is.EqualTo(0));
            afterEnter.Clear();

            LookAway();
            system.Update(0);

            AppendPointerEventResultsIntent afterLeave = TargetIntent();
            Assert.That(afterLeave.ValidIndicesCount(), Is.EqualTo(1), "the leave completing that enter must follow");
            Assert.That(afterLeave.ValidIndexAt(0), Is.EqualTo(1));
        }

        /// <summary>
        ///     A hover that drifts out of range before the ray leaves the entity issued its enter on the frame it
        ///     began; the leave must still follow when the ray moves on, whatever the range was on that last frame.
        /// </summary>
        [Test]
        public void IssueTheHoverLeaveWhenTheHoverLeftRangeBeforeTheRayMovedOn()
        {
            DeclarePointerEvents(
                Entry(PointerEventType.PetHoverEnter, InputAction.IaPointer, 10f),
                Entry(PointerEventType.PetHoverLeave, InputAction.IaPointer, 10f));

            HoverTarget();
            system.Update(0);
            Assert.That(TargetIntent().ValidIndicesCount(), Is.EqualTo(1), "test precondition: the enter is issued in range");
            TargetIntent().Clear();

            HoverTarget(distance: 50f);
            system.Update(0);
            Assert.That(TargetIntent().ValidIndicesCount(), Is.EqualTo(0), "the same entity is still hovered: no leave yet");

            LookAway();
            system.Update(0);

            AppendPointerEventResultsIntent afterLeave = TargetIntent();
            Assert.That(afterLeave.ValidIndicesCount(), Is.EqualTo(1), "the enter was issued, so the leave must follow");
            Assert.That(afterLeave.ValidIndexAt(0), Is.EqualTo(1));
        }

        [Test]
        public void NotIssueAHoverLeaveForAHoverThatNeverQualified()
        {
            DeclarePointerEvents(
                Entry(PointerEventType.PetHoverEnter, InputAction.IaPointer, 1f),
                Entry(PointerEventType.PetHoverLeave, InputAction.IaPointer, 1f));

            HoverTarget();
            system.Update(0);
            Assert.That(TargetIntent().ValidIndicesCount(), Is.EqualTo(0), "test precondition: out of range, so no enter");

            LookAway();
            system.Update(0);

            Assert.That(TargetIntent().ValidIndicesCount(), Is.EqualTo(0), "no enter was issued, so no leave may complete it");
        }
    }
}
