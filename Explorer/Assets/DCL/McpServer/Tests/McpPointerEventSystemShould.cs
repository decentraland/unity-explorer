using Arch.Core;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
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
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.McpServer.Tests
{
    public class McpPointerEventSystemShould : UnitySystemTestBase<McpPointerEventSystem>
    {
        private const int TARGET_CRDT_ID = 512;
        private const int BLOCKER_CRDT_ID = 513;

        private World sceneWorld = null!;
        private Entity playerEntity;
        private Entity targetEntity;
        private Entity pipelineEntity;

        private GameObject cameraGo = null!;
        private GameObject targetGo = null!;
        private GameObject blockerGo = null!;

        private BoxCollider targetCollider = null!;
        private IEntityCollidersGlobalCache collidersCache = null!;
        private ISceneStateProvider sceneStateProvider = null!;
        private ISceneFacade sceneFacade = null!;
        private IScenesCache scenesCache = null!;
        private uint tick;

        [SetUp]
        public void SetUp()
        {
            sceneWorld = World.Create();

            cameraGo = new GameObject("mcp-click-test-camera");
            Camera camera = cameraGo.AddComponent<Camera>();
            world.Create(new CameraComponent(camera)); // Mode defaults to FirstPerson

            playerEntity = world.Create();

            pipelineEntity = world.Create(
                new SyntheticPointerInput(),
                new PlayerOriginRaycastResultForSceneEntities(),
                new HoverStateComponent(),
                new HoverFeedbackComponent(4));

            targetGo = new GameObject("mcp-click-test-target")
                {
                    transform = { position = new Vector3(0f, 0f, 5f), },
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
                            Button = InputAction.IaPointer,
                            HoverText = "Open",
                            MaxDistance = 10f,
                        },
                    },
                },
            };

            // The entity needs a TransformComponent so ResolveEntityAimPoint can aim the synthetic ray at it;
            // without it the aim point is Vector3.zero and the pipeline ray misses the collider.
            targetEntity = sceneWorld.Create(targetPointerEvents, new CRDTEntity(TARGET_CRDT_ID), new TransformComponent(targetGo.transform));

            // Colliders created/moved this frame are not in the PhysX scene until transforms are synced (no physics step runs in EditMode).
            Physics.SyncTransforms();

            tick = 100u;
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            sceneStateProvider.State.Returns(new Atomic<SceneState>(SceneState.Running));
            sceneStateProvider.TickNumber.Returns(_ => tick);

            sceneFacade = Substitute.For<ISceneFacade>();
            sceneFacade.SceneStateProvider.Returns(sceneStateProvider);
            sceneFacade.EcsExecutor.Returns(new SceneEcsExecutor(sceneWorld));

            IReadonlyReactiveProperty<ISceneFacade?> currentScene = Substitute.For<IReadonlyReactiveProperty<ISceneFacade?>>();
            currentScene.Value.Returns(sceneFacade);

            scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(currentScene);

            collidersCache = Substitute.For<IEntityCollidersGlobalCache>();

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
            Object.DestroyImmediate(targetGo);

            if (blockerGo != null)
                Object.DestroyImmediate(blockerGo);

            sceneWorld.Dispose();
        }

        private UniTaskCompletionSource<McpPointerClickResult> AddIntent(
            PointerEventType eventType = PointerEventType.PetDown,
            int? targetId = null,
            McpPressHandoff? press = null,
            string? sceneId = null)
        {
            var completion = new UniTaskCompletionSource<McpPointerClickResult>();

            world.Add(playerEntity, new McpEcsPointerEventIntent(targetId ?? targetEntity.Id, sceneId, null, InputAction.IaPointer, eventType, press)
            {
                Completion = completion,
            });

            return completion;
        }

        private ref SyntheticPointerInput SyntheticInput => ref world.Get<SyntheticPointerInput>(pipelineEntity);

        /// <summary>
        ///     Emulates the frame of the reticle pipeline at its contract boundary: consumes the posted synthetic
        ///     input, raycasts along the synthetic ray and publishes the raycast/hover state the way
        ///     PlayerOriginatedRaycastSystem and ProcessPointerEventsSystem do.
        /// </summary>
        private void RunPipelineFrame(bool assignHover = true, bool isAtDistance = true, string? hoverText = null)
        {
            ref SyntheticPointerInput synthetic = ref SyntheticInput;
            Assert.That(synthetic.AimPoint.HasValue, Is.True, "a synthetic aim should have been posted");
            Vector3 aim = synthetic.AimPoint!.Value;
            synthetic = default(SyntheticPointerInput);

            Vector3 origin = cameraGo.transform.position;
            var ray = new Ray(origin, (aim - origin).normalized);

            ref PlayerOriginRaycastResultForSceneEntities raycastResult = ref world.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity);
            raycastResult.SetRay(ray);

            ref HoverStateComponent hoverState = ref world.Get<HoverStateComponent>(pipelineEntity);
            hoverState.Clear();

            ref HoverFeedbackComponent hoverFeedback = ref world.Get<HoverFeedbackComponent>(pipelineEntity);
            hoverFeedback.Clear();

            if (Physics.Raycast(ray, out RaycastHit hit, 100f)
                && collidersCache.TryGetSceneEntity(hit.collider, out GlobalColliderSceneEntityInfo info))
            {
                raycastResult.SetupHit(hit, info, hit.distance, hit.distance);

                if (assignHover)
                {
                    hoverState.AssignCollider(hit.collider!, isAtDistance, true);

                    if (hoverText != null)
                        hoverFeedback.Add(new HoverFeedbackComponent.Tooltip(hoverText, new UnityEngine.InputSystem.InputAction()));
                }
            }
            else
                raycastResult.Reset();
        }

        /// <summary>Delivers a press and returns its result, asserting the handoff the release leg needs is filled.</summary>
        private McpPointerClickResult DeliverPress()
        {
            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0); // inject
            RunPipelineFrame();
            system.Update(0); // observe

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.True);
            Assert.That(result.Press, Is.Not.Null);

            return result;
        }

        private static McpPointerClickResult ResultOf(UniTaskCompletionSource<McpPointerClickResult> completion)
        {
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            return completion.Task.GetAwaiter().GetResult();
        }

        [Test]
        public void PostSyntheticAimAndPressOnInjectFrame()
        {
            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0);

            ref SyntheticPointerInput synthetic = ref SyntheticInput;
            Assert.That(synthetic.AimPoint, Is.EqualTo((Vector3?)targetGo.transform.position));
            Assert.That(synthetic.PressButton, Is.EqualTo((InputAction?)InputAction.IaPointer));
            Assert.That(synthetic.ReleaseButton, Is.Null);
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(world.Has<McpEcsPointerEventIntent>(playerEntity), Is.True);
        }

        [Test]
        public void StayPendingUntilPipelineConsumesTheInput()
        {
            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0); // inject
            system.Update(0); // the pipeline has not run: keep waiting

            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(world.Has<McpEcsPointerEventIntent>(playerEntity), Is.True);
        }

        [Test]
        public void DeliverPressThenOrderedReleaseOnNextTick()
        {
            UniTaskCompletionSource<McpPointerClickResult> pressCompletion = AddIntent();

            system!.Update(0); // inject
            RunPipelineFrame(hoverText: "Open");
            system.Update(0); // observe

            McpPointerClickResult pressResult = ResultOf(pressCompletion);
            Assert.That(pressResult.Hit, Is.True);
            Assert.That(pressResult.CrdtEntityId, Is.EqualTo(TARGET_CRDT_ID));
            Assert.That(pressResult.HoverText, Is.EqualTo("Open"));
            Assert.That(pressResult.Press, Is.Not.Null);
            Assert.That(pressResult.Press!.Value.Entity, Is.EqualTo(targetEntity));
            Assert.That(pressResult.Press.Value.Tick, Is.EqualTo(tick));
            Assert.That(world.Has<McpEcsPointerEventIntent>(playerEntity), Is.False);

            // The observe frame of a press re-posts the aim so the hover stays on the target between the legs.
            Assert.That(SyntheticInput.AimPoint.HasValue, Is.True);
            Assert.That(SyntheticInput.PressButton, Is.Null);

            UniTaskCompletionSource<McpPointerClickResult> releaseCompletion = AddIntent(PointerEventType.PetUp, press: pressResult.Press);

            system.Update(0); // same tick: keeps waiting so PetUp lands on a later tick than PetDown
            Assert.That(releaseCompletion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(SyntheticInput.ReleaseButton, Is.Null, "no button may be posted while the release waits for the tick");

            tick++;
            system.Update(0); // inject the release

            Assert.That(SyntheticInput.ReleaseButton, Is.EqualTo((InputAction?)InputAction.IaPointer));

            RunPipelineFrame();
            system.Update(0); // observe

            McpPointerClickResult releaseResult = ResultOf(releaseCompletion);
            Assert.That(releaseResult.Hit, Is.True);
            Assert.That(releaseResult.UpRayMissed, Is.False);
            Assert.That(world.Has<McpEcsPointerEventIntent>(playerEntity), Is.False);
        }

        [Test]
        public void DeliverSingleReleaseWithoutPressContext()
        {
            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent(PointerEventType.PetUp);

            system!.Update(0);

            Assert.That(SyntheticInput.ReleaseButton, Is.EqualTo((InputAction?)InputAction.IaPointer));
            Assert.That(SyntheticInput.PressButton, Is.Null);

            RunPipelineFrame();
            system.Update(0);

            Assert.That(ResultOf(completion).Hit, Is.True);
            Assert.That(world.Has<McpEcsPointerEventIntent>(playerEntity), Is.False);
        }

        [Test]
        public void ReportReleaseMissWhenAnotherColliderBlocksIt()
        {
            McpPointerClickResult pressResult = DeliverPress();

            // A blocker slides between the camera and the target after the press.
            blockerGo = new GameObject("mcp-click-test-blocker") { transform = { position = new Vector3(0f, 0f, 2f) } };

            blockerGo.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            UniTaskCompletionSource<McpPointerClickResult> releaseCompletion = AddIntent(PointerEventType.PetUp, press: pressResult.Press);

            tick++;
            system!.Update(0); // inject
            RunPipelineFrame();
            system.Update(0); // observe

            McpPointerClickResult releaseResult = ResultOf(releaseCompletion);
            Assert.That(releaseResult.Hit, Is.False);
            Assert.That(releaseResult.UpRayMissed, Is.True);
            Assert.That(releaseResult.BlockedByCrdtId, Is.EqualTo(BLOCKER_CRDT_ID));
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
                McpPressHandoff stalePress = pressResult.Press!.Value;
                stalePress.World = reloadedWorld;

                UniTaskCompletionSource<McpPointerClickResult> releaseCompletion = AddIntent(PointerEventType.PetUp, press: stalePress);

                tick++;
                system!.Update(0);

                McpPointerClickResult releaseResult = ResultOf(releaseCompletion);
                Assert.That(releaseResult.Hit, Is.False);
                Assert.That(releaseResult.FailureReason, Does.Contain("reloaded"));
            }
            finally
            {
                reloadedWorld.Dispose();
            }
        }

        [Test]
        public void FailWhenAnotherColliderBlocksTheRay()
        {
            blockerGo = new GameObject("mcp-click-test-blocker") { transform = { position = new Vector3(0f, 0f, 2f) } };

            blockerGo.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0);
            RunPipelineFrame();
            system.Update(0);

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.UpRayMissed, Is.False);
            Assert.That(result.BlockedByCrdtId, Is.EqualTo(BLOCKER_CRDT_ID));
            Assert.That(world.Has<McpEcsPointerEventIntent>(playerEntity), Is.False);
        }

        [Test]
        public void FailWhenOutOfRange()
        {
            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0);
            RunPipelineFrame(isAtDistance: false);
            system.Update(0);

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("out of range"));
        }

        [Test]
        public void FailWhenEntityHasNoPointerEvents()
        {
            sceneWorld.Remove<PBPointerEvents>(targetEntity);

            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0);
            RunPipelineFrame(assignHover: false);
            system.Update(0);

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("PointerEvents"));
        }

        [Test]
        public void FailWhenPipelineSkipsTheFrame()
        {
            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent();

            system!.Update(0);

            // The pipeline consumed the input but published nothing (panning / in-world camera guard).
            SyntheticInput = default(SyntheticPointerInput);

            system.Update(0);

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("did not process"));
        }

        [Test]
        public void FailWhenPinnedSceneIsUnknown()
        {
            scenesCache.TryGetBySceneId("scene-gone", out Arg.Any<ISceneFacade?>()).Returns(false);

            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent(sceneId: "scene-gone");

            system!.Update(0);

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("pinned"));
            Assert.That(SyntheticInput.AimPoint, Is.Null, "no synthetic input may be posted for a rejected request");
        }

        [Test]
        public void FailWhenPinnedSceneIsNotCurrent()
        {
            // The pinned scene is still loaded, but the player stands in a different one now.
            ISceneFacade otherScene = Substitute.For<ISceneFacade>();

            scenesCache.TryGetBySceneId("scene-elsewhere", out Arg.Any<ISceneFacade?>())
                       .Returns(call =>
                        {
                            call[1] = otherScene;
                            return true;
                        });

            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent(sceneId: "scene-elsewhere");

            system!.Update(0);

            McpPointerClickResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("pinned"));
            Assert.That(SyntheticInput.AimPoint, Is.Null, "no synthetic input may be posted for a rejected request");
        }

        [Test]
        public void DeliverWhenPinnedSceneIsCurrent()
        {
            scenesCache.TryGetBySceneId("scene-here", out Arg.Any<ISceneFacade?>())
                       .Returns(call =>
                        {
                            call[1] = sceneFacade;
                            return true;
                        });

            UniTaskCompletionSource<McpPointerClickResult> completion = AddIntent(sceneId: "scene-here");

            system!.Update(0);
            RunPipelineFrame();
            system.Update(0);

            Assert.That(ResultOf(completion).Hit, Is.True);
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
