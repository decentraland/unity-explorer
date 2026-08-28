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

        private GlobalInputEvents globalInputEvents = null!;
        private ProcessPointerEventsSystem system = null!;
        private Keyboard keyboard = null!;
        private UnityEngine.InputSystem.InputAction primaryAction = null!;

        [SetUp]
        public void SetUp()
        {
            base.Setup();

            world = World.Create();
            sceneWorld = World.Create();

            cameraGo = new GameObject("pointer-events-test-camera");
            world.Create(new CameraComponent(cameraGo.AddComponent<Camera>()));

            keyboard = InputSystem.AddDevice<Keyboard>();
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
                PointerEvents =
                {
                    new PBPointerEvents.Types.Entry
                    {
                        EventType = PointerEventType.PetDown,
                        EventInfo = new PBPointerEvents.Types.Info
                        {
                            Button = InputAction.IaPrimary,
                            MaxDistance = 10f,
                        },
                    },
                },
            };

            targetPointerEvents.AppendPointerEventResultsIntent.InitializeWithAlloc();
            targetEntity = sceneWorld.Create(targetPointerEvents, new CRDTEntity(TARGET_CRDT_ID));

            IEventSystem eventSystem = Substitute.For<IEventSystem>();
            eventSystem.IsPointerOverGameObject().Returns(false);

            globalInputEvents = new GlobalInputEvents();

            system = new ProcessPointerEventsSystem(world, actionsMap, Substitute.For<IEntityCollidersGlobalCache>(), eventSystem, globalInputEvents);
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

        /// <summary>Points the pipeline ray at the target through a real raycast, so the hit carries a live collider.</summary>
        private void HoverTarget()
        {
            Physics.SyncTransforms();

            var ray = new Ray(Vector3.zero, Vector3.forward);
            Assert.That(Physics.Raycast(ray, out RaycastHit hit, 100f), Is.True, "test precondition: the ray must hit the target collider");
            Assert.That(hit.collider, Is.EqualTo(targetCollider), "test precondition: the ray must hit the target collider");

            var entityInfo = new GlobalColliderSceneEntityInfo(
                new SceneEcsExecutor(sceneWorld),
                new ColliderSceneEntityInfo(targetEntity, new CRDTEntity(TARGET_CRDT_ID), ColliderLayer.ClPointer));

            ref PlayerOriginRaycastResultForSceneEntities raycastResult = ref world.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity);
            raycastResult.SetRay(ray);
            raycastResult.SetupHit(hit, entityInfo, hit.distance, hit.distance);
        }

        private void PostSyntheticPress(InputAction button)
        {
            world.Get<SyntheticPointerInput>(pipelineEntity) = new SyntheticPointerInput
            {
                AimPoint = targetGo.transform.position,
                PressButton = button,
                PostedAtFrame = UnityEngine.Time.frameCount,
            };
        }

        private static IGlobalInputEvents.Entry Entry(InputAction action, PointerEventType type) =>
            new (action, type);

        [Test]
        public void SuppressGlobalBroadcastOfSyntheticEdgeBoundToHoveredEntity()
        {
            HoverTarget();
            PostSyntheticPress(InputAction.IaPrimary);

            globalInputEvents.Add(Entry(InputAction.IaPrimary, PointerEventType.PetDown));
            globalInputEvents.Add(Entry(InputAction.IaSecondary, PointerEventType.PetDown));

            system.Update(0);

            PBPointerEvents targetPointerEvents = sceneWorld.Get<PBPointerEvents>(targetEntity);

            Assert.That(targetPointerEvents.AppendPointerEventResultsIntent.ValidInputActions,
                Is.EqualTo(new[] { (InputAction.IaPrimary, PointerEventType.PetDown) }),
                "the edge must land entity-bound");

            Assert.That(globalInputEvents.Entries,
                Is.EqualTo(new[] { Entry(InputAction.IaSecondary, PointerEventType.PetDown) }),
                "the entity-bound edge must leave the scene-root broadcast buffer; unrelated edges must stay");
        }

        [Test]
        public void SuppressGlobalBroadcastOfRealEdgeBoundToHoveredEntity()
        {
            HoverTarget();

            Press(keyboard.eKey);
            Assert.That(primaryAction.WasPressedThisFrame(), Is.True, "test precondition: the real press must be visible this frame");

            globalInputEvents.Add(Entry(InputAction.IaPrimary, PointerEventType.PetDown));

            system.Update(0);

            Assert.That(globalInputEvents.Entries, Is.Empty,
                "a real key press bound to the hovered entity must not also broadcast to the scene root");
        }

        [Test]
        public void KeepGlobalBroadcastWhenNothingIsHovered()
        {
            PostSyntheticPress(InputAction.IaPrimary);

            globalInputEvents.Add(Entry(InputAction.IaPrimary, PointerEventType.PetDown));

            system.Update(0);

            Assert.That(globalInputEvents.Entries,
                Is.EqualTo(new[] { Entry(InputAction.IaPrimary, PointerEventType.PetDown) }),
                "an edge that landed on no entity keeps its scene-root broadcast");
        }
    }
}
