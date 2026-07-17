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
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.McpServer.Tests
{
    public class McpPointerClickSystemShould : UnitySystemTestBase<McpPointerClickSystem>
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
            targetEntity = sceneWorld.Create(targetPointerEvents, new CRDTEntity(TARGET_CRDT_ID));

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

            system = new McpPointerClickSystem(world, scenesCache, collidersCache, playerEntity);
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
            McpPointerClickIntent.ClickKind kind = McpPointerClickIntent.ClickKind.CLICK,
            int? targetId = null)
        {
            var completion = new UniTaskCompletionSource<McpPointerClickResult>();

            world.Add(playerEntity, new McpPointerClickIntent
            {
                TargetEntityId = targetId ?? targetEntity.Id,
                Button = InputAction.IaPointer,
                Kind = kind,
                Phase = McpPointerClickIntent.ClickPhase.DOWN,
                Deadline = UnityEngine.Time.time + 5f,
                Completion = completion,
            });

            return completion;
        }

        private static McpPointerClickResult ResultOf(UniTaskCompletionSource<McpPointerClickResult> completion)
        {
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            return completion.Task.GetAwaiter().GetResult();
        }

        [Test]
        public void DeliverDownThenUpOnNextTick()
        {
            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0);

            var actions = targetPointerEvents.AppendPointerEventResultsIntent.ValidInputActions;
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions[0], Is.EqualTo((InputAction.IaPointer, PointerEventType.PetDown)));
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));

            // The scene-world flush clears the intent at the end of a real frame.
            targetPointerEvents.AppendPointerEventResultsIntent.Clear();

            system.Update(0); // same tick: keeps waiting so PetUp lands on a later tick than PetDown
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));

            tick++;
            system.Update(0); // observes the tick advance
            system.Update(0); // delivers PetUp

            actions = targetPointerEvents.AppendPointerEventResultsIntent.ValidInputActions;
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions[0], Is.EqualTo((InputAction.IaPointer, PointerEventType.PetUp)));

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.True);
            Assert.That(result.CrdtEntityId, Is.EqualTo(TARGET_CRDT_ID));
            Assert.That(result.HoverText, Is.EqualTo("Open"));
            Assert.That(result.UpRayMissed, Is.False);
            Assert.That(world.Has<McpPointerClickIntent>(playerEntity), Is.False);
        }

        [Test]
        public void DeliverSingleDownWithoutWaiting()
        {
            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent(McpPointerClickIntent.ClickKind.DOWN);

            system!.Update(0);

            var actions = targetPointerEvents.AppendPointerEventResultsIntent.ValidInputActions;
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions[0], Is.EqualTo((InputAction.IaPointer, PointerEventType.PetDown)));

            Assert.That(ResultOf(completion).Hit, Is.True);
            Assert.That(world.Has<McpPointerClickIntent>(playerEntity), Is.False);
        }

        [Test]
        public void FailWhenAnotherColliderBlocksTheRay()
        {
            blockerGo = new GameObject("mcp-click-test-blocker") { transform = { position = new Vector3(0f, 0f, 2f) }};

            blockerGo.AddComponent<BoxCollider>();

            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0);

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.BlockedByCrdtId, Is.EqualTo(BLOCKER_CRDT_ID));
            Assert.That(targetPointerEvents.AppendPointerEventResultsIntent.ValidInputActions.Count, Is.EqualTo(0));
            Assert.That(world.Has<McpPointerClickIntent>(playerEntity), Is.False);
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

        [Test]
        public void FailWhenDeadlinePassed()
        {
            var completion = new UniTaskCompletionSource<McpPointerClickResult>();

            world.Add(playerEntity, new McpPointerClickIntent
            {
                TargetEntityId = targetEntity.Id,
                Button = InputAction.IaPointer,
                Kind = McpPointerClickIntent.ClickKind.CLICK,
                Phase = McpPointerClickIntent.ClickPhase.DOWN,
                Deadline = UnityEngine.Time.time - 1f,
                Completion = completion,
            });

            system!.Update(0);

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("timed out"));
            Assert.That(world.Has<McpPointerClickIntent>(playerEntity), Is.False);
        }
    }
}
