using Arch.Core;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.McpServer.Components;
using DCL.McpServer.Systems;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.McpServer.Tests
{
    public class McpPointerEventSystemShould : UnitySystemTestBase<McpPointerEventSystem>
    {
        private const int TARGET_CRDT_ID = 512;
        private const int BLOCKER_CRDT_ID = 513;

        private World sceneWorld = null!;
        private Entity playerEntity;
        private Entity targetEntity;

        private GameObject cameraGo = null!;
        private GameObject playerGo = null!;
        private GameObject targetGo = null!;
        private GameObject blockerGo = null!;

        private BoxCollider targetCollider = null!;
        private PBPointerEvents targetPointerEvents = null!;
        private ISceneStateProvider sceneStateProvider = null!;
        private uint tick;

        [SetUp]
        public void SetUp()
        {
            sceneWorld = World.Create();

            cameraGo = new GameObject("mcp-click-test-camera");
            Camera camera = cameraGo.AddComponent<Camera>();
            world.Create(new CameraComponent(camera)); // Mode defaults to FirstPerson

            playerGo = new GameObject("mcp-click-test-player");
            playerEntity = world.Create(new CharacterTransform(playerGo.transform));

            targetGo = new GameObject("mcp-click-test-target")
                {
                    transform = { position = new Vector3(0f, 0f, 5f), },
                };

            targetCollider = targetGo.AddComponent<BoxCollider>();

            targetPointerEvents = new PBPointerEvents
            {
                PointerEvents =
                {
                    new PBPointerEvents.Types.Entry
                    {
                        EventType = PointerEventType.PetDown,
                        EventInfo = new PBPointerEvents.Types.Info
                        {
                            Button = InputAction.IaPointer,
                            HoverText = "Open",
                            MaxDistance = 10f,
                        },
                    },
                },
            };

            targetPointerEvents.AppendPointerEventResultsIntent.InitializeWithAlloc();

            // The entity needs a TransformComponent so ResolveEntityAimPoint can aim the validation ray at it;
            // without it the aim point is Vector3.zero and the delivery bails out before the raycast.
            targetEntity = sceneWorld.Create(targetPointerEvents, new CRDTEntity(TARGET_CRDT_ID), new TransformComponent(targetGo.transform));

            // Colliders created/moved this frame are not in the PhysX scene until transforms are synced (no physics step runs in EditMode).
            Physics.SyncTransforms();

            tick = 100u;
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            sceneStateProvider.State.Returns(new Atomic<SceneState>(SceneState.Running));
            sceneStateProvider.TickNumber.Returns(_ => tick);

            ISceneFacade sceneFacade = Substitute.For<ISceneFacade>();
            sceneFacade.SceneStateProvider.Returns(sceneStateProvider);
            sceneFacade.EcsExecutor.Returns(new SceneEcsExecutor(sceneWorld));

            IReadonlyReactiveProperty<ISceneFacade?> currentScene = Substitute.For<IReadonlyReactiveProperty<ISceneFacade?>>();
            currentScene.Value.Returns(sceneFacade);

            IScenesCache scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(currentScene);

            IEntityCollidersGlobalCache collidersCache = Substitute.For<IEntityCollidersGlobalCache>();

            collidersCache.TryGetSceneEntity(Arg.Any<Collider>(), out Arg.Any<GlobalColliderSceneEntityInfo>())
                          .Returns(call =>
                           {
                               var collider = call.ArgAt<Collider>(0);

                               if (collider == targetCollider)
                               {
                                   call[1] = new GlobalColliderSceneEntityInfo(
                                       new SceneEcsExecutor(sceneWorld),
                                       new ColliderSceneEntityInfo(targetEntity, new CRDTEntity(TARGET_CRDT_ID), ColliderLayer.ClPointer));

                                   return true;
                               }

                               if (blockerGo != null && collider == blockerGo.GetComponent<BoxCollider>())
                               {
                                   call[1] = new GlobalColliderSceneEntityInfo(
                                       new SceneEcsExecutor(sceneWorld),
                                       new ColliderSceneEntityInfo(sceneWorld.Create(new CRDTEntity(BLOCKER_CRDT_ID)), new CRDTEntity(BLOCKER_CRDT_ID), ColliderLayer.ClPhysics));

                                   return true;
                               }

                               return false;
                           });

            system = new McpPointerEventSystem(world, scenesCache, collidersCache, playerEntity);
            system.Initialize();
        }

        protected override void OnTearDown()
        {
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(targetGo);

            if (blockerGo != null)
                Object.DestroyImmediate(blockerGo);

            sceneWorld.Dispose();
        }

        private UniTaskCompletionSource<McpPointerClickResult> AddIntent(
            PointerEventType eventType = PointerEventType.PetDown,
            int? targetId = null,
            McpPressHandoff? press = null)
        {
            var completion = new UniTaskCompletionSource<McpPointerClickResult>();

            world.Add(playerEntity, new McpEcsPointerEventIntent(targetId ?? targetEntity.Id, null, InputAction.IaPointer, eventType, press)
            {
                Completion = completion,
            });

            return completion;
        }

        /// <summary>Delivers a press and returns its result, asserting the handoff the release leg needs is filled.</summary>
        private McpPointerClickResult DeliverPress()
        {
            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0);

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.True);
            Assert.That(result.Press, Is.Not.Null);

            // The scene-world flush clears the intent at the end of a real frame.
            targetPointerEvents.AppendPointerEventResultsIntent.Clear();

            return result;
        }

        private static McpPointerClickResult ResultOf(UniTaskCompletionSource<McpPointerClickResult> completion)
        {
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            return completion.Task.GetAwaiter().GetResult();
        }

        [Test]
        public void DeliverPressThenOrderedReleaseOnNextTick()
        {
            UniTaskCompletionSource<McpPointerClickResult> pressCompletion = AddIntent();

            system!.Update(0);

            var actions = targetPointerEvents.AppendPointerEventResultsIntent.ValidInputActions;
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions[0], Is.EqualTo((InputAction.IaPointer, PointerEventType.PetDown)));

            McpPointerClickResult pressResult = ResultOf(pressCompletion);
            Assert.That(pressResult.Hit, Is.True);
            Assert.That(pressResult.CrdtEntityId, Is.EqualTo(TARGET_CRDT_ID));
            Assert.That(pressResult.HoverText, Is.EqualTo("Open"));
            Assert.That(pressResult.Press, Is.Not.Null);
            Assert.That(pressResult.Press.Value.Entity, Is.EqualTo(targetEntity));
            Assert.That(pressResult.Press.Value.Tick, Is.EqualTo(tick));
            Assert.That(world.Has<McpEcsPointerEventIntent>(playerEntity), Is.False);

            // The scene-world flush clears the intent at the end of a real frame.
            targetPointerEvents.AppendPointerEventResultsIntent.Clear();

            UniTaskCompletionSource<McpPointerClickResult> releaseCompletion = AddIntent(PointerEventType.PetUp, press: pressResult.Press);

            system.Update(0); // same tick: keeps waiting so PetUp lands on a later tick than PetDown
            Assert.That(releaseCompletion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));

            tick++;
            system.Update(0);

            actions = targetPointerEvents.AppendPointerEventResultsIntent.ValidInputActions;
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions[0], Is.EqualTo((InputAction.IaPointer, PointerEventType.PetUp)));

            McpPointerClickResult releaseResult = ResultOf(releaseCompletion);
            Assert.That(releaseResult.Hit, Is.True);
            Assert.That(releaseResult.UpRayMissed, Is.False);
            Assert.That(world.Has<McpEcsPointerEventIntent>(playerEntity), Is.False);
        }

        [Test]
        public void DeliverSinglePressWithoutWaiting()
        {
            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0);

            var actions = targetPointerEvents.AppendPointerEventResultsIntent.ValidInputActions;
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions[0], Is.EqualTo((InputAction.IaPointer, PointerEventType.PetDown)));

            Assert.That(ResultOf(completion).Hit, Is.True);
            Assert.That(world.Has<McpEcsPointerEventIntent>(playerEntity), Is.False);
        }

        [Test]
        public void DeliverSingleReleaseWithoutPressContext()
        {
            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent(PointerEventType.PetUp);

            system!.Update(0);

            var actions = targetPointerEvents.AppendPointerEventResultsIntent.ValidInputActions;
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions[0], Is.EqualTo((InputAction.IaPointer, PointerEventType.PetUp)));

            Assert.That(ResultOf(completion).Hit, Is.True);
            Assert.That(world.Has<McpEcsPointerEventIntent>(playerEntity), Is.False);
        }

        [Test]
        public void ReplayPressHitWhenReleaseRayIsBlocked()
        {
            McpPointerClickResult pressResult = DeliverPress();

            // A blocker slides between the camera and the target after the press.
            blockerGo = new GameObject("mcp-click-test-blocker") { transform = { position = new Vector3(0f, 0f, 2f) }};

            blockerGo.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            UniTaskCompletionSource<McpPointerClickResult> releaseCompletion = AddIntent(PointerEventType.PetUp, press: pressResult.Press);

            tick++;
            system!.Update(0);

            var actions = targetPointerEvents.AppendPointerEventResultsIntent.ValidInputActions;
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions[0], Is.EqualTo((InputAction.IaPointer, PointerEventType.PetUp)));

            McpPointerClickResult releaseResult = ResultOf(releaseCompletion);
            Assert.That(releaseResult.Hit, Is.True);
            Assert.That(releaseResult.UpRayMissed, Is.True);
        }

        [Test]
        public void ReportPressOnlyWhenTargetDiesBeforeRelease()
        {
            McpPointerClickResult pressResult = DeliverPress();

            sceneWorld.Destroy(targetEntity);

            UniTaskCompletionSource<McpPointerClickResult> releaseCompletion = AddIntent(PointerEventType.PetUp, press: pressResult.Press);

            tick++;
            system!.Update(0);

            McpPointerClickResult releaseResult = ResultOf(releaseCompletion);
            Assert.That(releaseResult.Hit, Is.False);
            Assert.That(releaseResult.UpRayMissed, Is.True);
            Assert.That(releaseResult.FailureReason, Does.Contain("destroyed"));
        }

        [Test]
        public void FailReleaseWhenSceneWorldChangedMidClick()
        {
            McpPointerClickResult pressResult = DeliverPress();

            var reloadedWorld = World.Create();

            try
            {
                McpPressHandoff stalePress = pressResult.Press.Value;
                stalePress.World = reloadedWorld;

                UniTaskCompletionSource<McpPointerClickResult> releaseCompletion = AddIntent(PointerEventType.PetUp, press: stalePress);

                tick++;
                system!.Update(0);

                McpPointerClickResult releaseResult = ResultOf(releaseCompletion);
                Assert.That(releaseResult.Hit, Is.False);
                Assert.That(releaseResult.FailureReason, Does.Contain("reloaded"));
                Assert.That(targetPointerEvents.AppendPointerEventResultsIntent.ValidInputActions.Count, Is.EqualTo(0));
            }
            finally
            {
                reloadedWorld.Dispose();
            }
        }

        [Test]
        public void FailWhenAnotherColliderBlocksTheRay()
        {
            blockerGo = new GameObject("mcp-click-test-blocker") { transform = { position = new Vector3(0f, 0f, 2f) }};

            blockerGo.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0);

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.BlockedByCrdtId, Is.EqualTo(BLOCKER_CRDT_ID));
            Assert.That(targetPointerEvents.AppendPointerEventResultsIntent.ValidInputActions.Count, Is.EqualTo(0));
            Assert.That(world.Has<McpEcsPointerEventIntent>(playerEntity), Is.False);
        }

        [Test]
        public void FailWhenOutOfRange()
        {
            targetPointerEvents.PointerEvents[0].EventInfo.MaxDistance = 2f;

            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0);

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("out of range"));
            Assert.That(targetPointerEvents.AppendPointerEventResultsIntent.ValidInputActions.Count, Is.EqualTo(0));
        }

        [Test]
        public void FailWhenEntityHasNoPointerEvents()
        {
            sceneWorld.Remove<PBPointerEvents>(targetEntity);

            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0);

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("PointerEvents"));
        }

        [Test]
        public void FailWhenEntityIdIsUnknown()
        {
            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent(targetId: 987654);

            system!.Update(0);

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("no entity"));
        }
    }
}
