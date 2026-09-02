using Arch.Core;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.Ipfs;
using DCL.SyntheticInput.Components;
using DCL.SyntheticInput.Systems;
using DCL.SyntheticInput.UiSimulation;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Multithreading;
using PlayerOriginatedRaycastSystem = DCL.Interaction.Systems.PlayerOriginatedRaycastSystem;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.SyntheticInput.Tests
{
    public class SyntheticPointerEventSystemShould : UnitySystemTestBase<SyntheticPointerEventSystem>
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
            // NUnit reuses one fixture instance for the whole class, so the stubbed cover must be cleared
            // per test — otherwise an armed cover leaks into every test that runs after it.
            uiCover = null;

            // The parked pointer is static and lives a frame longer than the assertion; EditMode tests share
            // frames, so a hold from the previous test would still read as asserted in this one.
            SyntheticCursorState.Reset();

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

            system = new SyntheticPointerEventSystem(world, scenesCache, collidersCache, playerEntity, TryFindUiCover);
            system.Initialize();
        }

        /// <summary>Armed per test: what the stubbed UI probe reports as covering any screen point, if anything.</summary>
        private string? uiCover;

        /// <summary>Non-scene geometry (skybox-like) a test can place on the ray; never registered in the colliders cache.</summary>
        private GameObject? nonSceneGo;

        private bool TryFindUiCover(Vector2 screenPoint, out string cover)
        {
            cover = uiCover ?? string.Empty;
            return uiCover != null;
        }

        protected override void OnTearDown()
        {
            SyntheticCursorState.Reset();
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(targetGo);

            if (blockerGo != null)
                Object.DestroyImmediate(blockerGo);

            if (nonSceneGo != null)
            {
                Object.DestroyImmediate(nonSceneGo);
                nonSceneGo = null;
            }

            sceneWorld.Dispose();
        }

        private UniTaskCompletionSource<SyntheticPointerOutcome> AddIntent(
            PointerEventType eventType = PointerEventType.PetDown,
            int? targetId = null,
            SyntheticPressHandoff? press = null,
            string? sceneId = null)
        {
            var completion = new UniTaskCompletionSource<SyntheticPointerOutcome>();

            world.Add(playerEntity, new SyntheticPointerEventIntent(targetId ?? targetEntity.Id, sceneId, null, InputAction.IaPointer, eventType, press)
            {
                Completion = completion,
            });

            return completion;
        }

        private void SetCurrentSceneDefinitionId(string id) =>
            sceneFacade.SceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition(id, new SceneMetadata()));

        private ref SyntheticPointerInput syntheticInput => ref world.Get<SyntheticPointerInput>(pipelineEntity);

        /// <summary>
        ///     Emulates the frame of the reticle pipeline at its contract boundary: consumes the posted synthetic
        ///     input, raycasts along the synthetic ray and publishes the raycast/hover state the way
        ///     PlayerOriginatedRaycastSystem and ProcessPointerEventsSystem do.
        /// </summary>
        private void RunPipelineFrame(bool assignHover = true, bool isAtDistance = true, string? hoverText = null)
        {
            ref SyntheticPointerInput synthetic = ref syntheticInput;
            Assert.That(synthetic.AimPoint.HasValue, Is.True, "a synthetic aim should have been posted");
            Assert.That(synthetic.IsPostedThisFrame, Is.True, "the pipeline honors a post only during the frame it was stamped with");
            Vector3 aim = synthetic.AimPoint!.Value;
            synthetic = default(SyntheticPointerInput);

            Vector3 origin = cameraGo.transform.position;
            var ray = new Ray(origin, (aim - origin).normalized);

            ref PlayerOriginRaycastResultForSceneEntities raycastResult = ref world.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity);
            raycastResult.SetRay(ray, aim);

            ref HoverStateComponent hoverState = ref world.Get<HoverStateComponent>(pipelineEntity);
            hoverState.Clear();

            ref HoverFeedbackComponent hoverFeedback = ref world.Get<HoverFeedbackComponent>(pipelineEntity);
            hoverFeedback.Clear();

            if (Physics.Raycast(ray, out RaycastHit hit, PlayerOriginatedRaycastSystem.MAX_RAYCAST_DISTANCE)
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

        /// <summary>
        ///     Emulates a frame the reticle pipeline guards away (cursor panning, in-world camera): the posted
        ///     input is still consumed, but no ray is built, so the raycast result is reset and no synthetic-aim
        ///     echo is published.
        /// </summary>
        private void RunPipelineSkippedFrame()
        {
            syntheticInput = default(SyntheticPointerInput);

            ref PlayerOriginRaycastResultForSceneEntities raycastResult = ref world.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity);
            raycastResult.Reset();
            raycastResult.ClearSyntheticAim();

            ref HoverStateComponent hoverState = ref world.Get<HoverStateComponent>(pipelineEntity);
            hoverState.Clear();

            ref HoverFeedbackComponent hoverFeedback = ref world.Get<HoverFeedbackComponent>(pipelineEntity);
            hoverFeedback.Clear();
        }

        /// <summary>Delivers a press and returns its outcome, asserting the handoff the release leg needs is filled.</summary>
        private SyntheticPointerOutcome DeliverPress()
        {
            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddIntent();

            system.Update(0); // inject
            RunPipelineFrame();
            system.Update(0); // observe

            SyntheticPointerOutcome outcome = OutcomeOf(completion);
            Assert.That(outcome.Result.Hit, Is.True);
            Assert.That(outcome.Press, Is.Not.Null);

            return outcome;
        }

        private static SyntheticPointerResult ResultOf(UniTaskCompletionSource<SyntheticPointerOutcome> completion) =>
            OutcomeOf(completion).Result;

        private static SyntheticPointerOutcome OutcomeOf(UniTaskCompletionSource<SyntheticPointerOutcome> completion)
        {
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            return completion.Task.GetAwaiter().GetResult();
        }

        [Test]
        public void PostSyntheticAimAndPressOnInjectFrame()
        {
            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddIntent();

            system.Update(0);

            ref SyntheticPointerInput synthetic = ref syntheticInput;
            Assert.That(synthetic.AimPoint, Is.EqualTo((Vector3?)targetGo.transform.position));
            Assert.That(synthetic.PressButton, Is.EqualTo((InputAction?)InputAction.IaPointer));
            Assert.That(synthetic.ReleaseButton, Is.Null);
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(world.Has<SyntheticPointerEventIntent>(playerEntity), Is.True);
        }

        [Test]
        public void StayPendingUntilPipelineConsumesTheInput()
        {
            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddIntent();

            system.Update(0); // inject
            system.Update(0); // the pipeline has not run: keep waiting

            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(world.Has<SyntheticPointerEventIntent>(playerEntity), Is.True);
        }

        [Test]
        public void LeaveForeignSyntheticInputAloneWhenNoRequestIsPending()
        {
            // Another automation driver's post must survive an idle update of this system untouched;
            // stale posts die at the pipeline's readers, not at a sweeping owner.
            var foreignAim = new Vector3(1f, 2f, 3f);
            syntheticInput = new SyntheticPointerInput { AimPoint = foreignAim, PostedAtFrame = UnityEngine.Time.frameCount };

            system.Update(0);

            Assert.That(syntheticInput.AimPoint, Is.EqualTo((Vector3?)foreignAim));
        }

        [Test]
        public void DeliverPressThenOrderedReleaseOnNextTick()
        {
            UniTaskCompletionSource<SyntheticPointerOutcome> pressCompletion = AddIntent();

            system.Update(0); // inject
            RunPipelineFrame(hoverText: "Open");
            system.Update(0); // observe

            SyntheticPointerOutcome pressOutcome = OutcomeOf(pressCompletion);
            Assert.That(pressOutcome.Result.Hit, Is.True);
            Assert.That(pressOutcome.Result.CrdtEntityId, Is.EqualTo(TARGET_CRDT_ID));
            Assert.That(pressOutcome.Result.HoverText, Is.EqualTo("Open"));
            Assert.That(pressOutcome.Press, Is.Not.Null);
            Assert.That(pressOutcome.Press!.Value.Entity, Is.EqualTo(targetEntity));
            Assert.That(pressOutcome.Press.Value.Tick, Is.EqualTo(tick));
            Assert.That(world.Has<SyntheticPointerEventIntent>(playerEntity), Is.False);

            // The observe frame of a press re-posts the aim so the hover stays on the target between the legs.
            Assert.That(syntheticInput.AimPoint.HasValue, Is.True);
            Assert.That(syntheticInput.PressButton, Is.Null);

            UniTaskCompletionSource<SyntheticPointerOutcome> releaseCompletion = AddIntent(PointerEventType.PetUp, press: pressOutcome.Press);

            system.Update(0); // same tick: keeps waiting so PetUp lands on a later tick than PetDown
            Assert.That(releaseCompletion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(syntheticInput.ReleaseButton, Is.Null, "no button may be posted while the release waits for the tick");

            tick++;
            system.Update(0); // inject the release

            Assert.That(syntheticInput.ReleaseButton, Is.EqualTo((InputAction?)InputAction.IaPointer));

            RunPipelineFrame();
            system.Update(0); // observe

            SyntheticPointerResult releaseResult = ResultOf(releaseCompletion);
            Assert.That(releaseResult.Hit, Is.True);
            Assert.That(releaseResult.UpRayMissed, Is.False);
            Assert.That(world.Has<SyntheticPointerEventIntent>(playerEntity), Is.False);
        }

        [Test]
        public void DeliverSingleReleaseWithoutPressContext()
        {
            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddIntent(PointerEventType.PetUp);

            system.Update(0);

            Assert.That(syntheticInput.ReleaseButton, Is.EqualTo((InputAction?)InputAction.IaPointer));
            Assert.That(syntheticInput.PressButton, Is.Null);

            RunPipelineFrame();
            system.Update(0);

            Assert.That(ResultOf(completion).Hit, Is.True);
            Assert.That(world.Has<SyntheticPointerEventIntent>(playerEntity), Is.False);
        }

        [Test]
        public void ReportReleaseMissWhenAnotherColliderBlocksIt()
        {
            SyntheticPointerOutcome pressOutcome = DeliverPress();

            // A blocker slides between the camera and the target after the press.
            blockerGo = new GameObject("mcp-click-test-blocker") { transform = { position = new Vector3(0f, 0f, 2f) } };

            blockerGo.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            UniTaskCompletionSource<SyntheticPointerOutcome> releaseCompletion = AddIntent(PointerEventType.PetUp, press: pressOutcome.Press);

            tick++;
            system.Update(0); // inject
            RunPipelineFrame();
            system.Update(0); // observe

            SyntheticPointerResult releaseResult = ResultOf(releaseCompletion);
            Assert.That(releaseResult.Hit, Is.False);
            Assert.That(releaseResult.UpRayMissed, Is.True);
            Assert.That(releaseResult.BlockedByCrdtId, Is.EqualTo(BLOCKER_CRDT_ID));
        }

        [Test]
        public void ReportPressOnlyWhenTargetDiesBeforeRelease()
        {
            SyntheticPointerOutcome pressOutcome = DeliverPress();

            sceneWorld.Destroy(targetEntity);

            UniTaskCompletionSource<SyntheticPointerOutcome> releaseCompletion = AddIntent(PointerEventType.PetUp, press: pressOutcome.Press);

            tick++;
            system.Update(0);

            SyntheticPointerResult releaseResult = ResultOf(releaseCompletion);
            Assert.That(releaseResult.Hit, Is.False);
            Assert.That(releaseResult.UpRayMissed, Is.True);
            Assert.That(releaseResult.FailureReason, Does.Contain("destroyed"));
        }

        [Test]
        public void FailReleaseWhenSceneWorldChangedMidClick()
        {
            SyntheticPointerOutcome pressOutcome = DeliverPress();

            var reloadedWorld = World.Create();

            try
            {
                SyntheticPressHandoff stalePress = pressOutcome.Press!.Value;
                stalePress.World = reloadedWorld;

                UniTaskCompletionSource<SyntheticPointerOutcome> releaseCompletion = AddIntent(PointerEventType.PetUp, press: stalePress);

                tick++;
                system.Update(0);

                SyntheticPointerResult releaseResult = ResultOf(releaseCompletion);
                Assert.That(releaseResult.Hit, Is.False);
                Assert.That(releaseResult.UpRayMissed, Is.True);
                Assert.That(releaseResult.FailureReason, Does.Contain("reloaded"));
            }
            finally
            {
                reloadedWorld.Dispose();
            }
        }

        [Test]
        public void MarkReleaseAsPressOnlyWhenCurrentSceneGuardRejectsIt()
        {
            SyntheticPointerOutcome pressOutcome = DeliverPress();

            // The scene stops being current between the legs (player crossed a parcel border).
            sceneStateProvider.IsCurrent.Returns(false);

            UniTaskCompletionSource<SyntheticPointerOutcome> releaseCompletion = AddIntent(PointerEventType.PetUp, press: pressOutcome.Press);

            tick++;
            system.Update(0);

            SyntheticPointerResult releaseResult = ResultOf(releaseCompletion);
            Assert.That(releaseResult.Hit, Is.False);
            Assert.That(releaseResult.UpRayMissed, Is.True);
            Assert.That(releaseResult.FailureReason, Does.Contain("no running current scene"));
        }

        [Test]
        public void MarkReleaseAsPressOnlyWhenPinnedSceneChangedBetweenLegs()
        {
            SetCurrentSceneDefinitionId("scene-press");
            SyntheticPointerOutcome pressOutcome = DeliverPress();

            SetCurrentSceneDefinitionId("scene-after-move");

            UniTaskCompletionSource<SyntheticPointerOutcome> releaseCompletion = AddIntent(PointerEventType.PetUp, press: pressOutcome.Press, sceneId: "scene-press");

            tick++;
            system.Update(0);

            SyntheticPointerResult releaseResult = ResultOf(releaseCompletion);
            Assert.That(releaseResult.Hit, Is.False);
            Assert.That(releaseResult.UpRayMissed, Is.True);
            Assert.That(releaseResult.FailureReason, Does.Contain("pinned"));
        }

        [Test]
        public void FailWhenAnotherColliderBlocksTheRay()
        {
            blockerGo = new GameObject("mcp-click-test-blocker") { transform = { position = new Vector3(0f, 0f, 2f) } };

            blockerGo.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddIntent();

            system.Update(0);
            RunPipelineFrame();
            system.Update(0);

            SyntheticPointerResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.UpRayMissed, Is.False);
            Assert.That(result.BlockedByCrdtId, Is.EqualTo(BLOCKER_CRDT_ID));
            Assert.That(world.Has<SyntheticPointerEventIntent>(playerEntity), Is.False);
        }

        [Test]
        public void FailWhenOutOfRange()
        {
            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddIntent();

            system.Update(0);
            RunPipelineFrame(isAtDistance: false);
            system.Update(0);

            SyntheticPointerResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("out of range"));
        }

        [Test]
        public void FailWhenEntityHasNoPointerEvents()
        {
            sceneWorld.Remove<PBPointerEvents>(targetEntity);

            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddIntent();

            system.Update(0);
            RunPipelineFrame(assignHover: false);
            system.Update(0);

            SyntheticPointerResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("PointerEvents"));
        }

        [Test]
        public void FailWhenPipelineSkipsTheFrame()
        {
            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddIntent();

            system.Update(0);
            RunPipelineSkippedFrame();
            system.Update(0);

            SyntheticPointerResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("did not process"));
        }

        [Test]
        public void FailTruthfullyWhenPipelineSkipsTheReleaseFrame()
        {
            // The press frame left a raycast result whose ray passes through the very aim the release re-uses;
            // only the explicit synthetic-aim echo distinguishes a skipped release frame from a processed one.
            SyntheticPointerOutcome pressOutcome = DeliverPress();

            UniTaskCompletionSource<SyntheticPointerOutcome> releaseCompletion = AddIntent(PointerEventType.PetUp, press: pressOutcome.Press);

            tick++;
            system.Update(0); // inject the release
            RunPipelineSkippedFrame();
            system.Update(0); // observe

            SyntheticPointerResult releaseResult = ResultOf(releaseCompletion);
            Assert.That(releaseResult.Hit, Is.False);
            Assert.That(releaseResult.UpRayMissed, Is.True);
            Assert.That(releaseResult.FailureReason, Does.Contain("did not process"));
        }

        [Test]
        public void FailWhenCurrentSceneHasNoDefinitionId()
        {
            // The current scene's definition carries an empty id (the codebase's id-less definition), so no pin can match it.
            SetCurrentSceneDefinitionId(string.Empty);

            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddIntent(sceneId: "scene-gone");

            system.Update(0);

            SyntheticPointerResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("pinned"));
            Assert.That(syntheticInput.AimPoint, Is.Null, "no synthetic input may be posted for a rejected request");
        }

        [Test]
        public void FailWhenPinnedSceneIsNotCurrent()
        {
            // The pinned scene may still be loaded, but the player stands in a different one now.
            SetCurrentSceneDefinitionId("scene-current");

            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddIntent(sceneId: "scene-elsewhere");

            system.Update(0);

            SyntheticPointerResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("pinned"));
            Assert.That(syntheticInput.AimPoint, Is.Null, "no synthetic input may be posted for a rejected request");
        }

        [Test]
        public void DeliverWhenPinnedSceneIsCurrent()
        {
            SetCurrentSceneDefinitionId("scene-here");

            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddIntent(sceneId: "scene-here");

            system.Update(0);
            RunPipelineFrame();
            system.Update(0);

            Assert.That(ResultOf(completion).Hit, Is.True);
        }

        [Test]
        public void FailWhenEntityIdIsUnknown()
        {
            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddIntent(targetId: 987654);

            system.Update(0);

            SyntheticPointerResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("no entity"));
        }

        private UniTaskCompletionSource<SyntheticPointerOutcome> AddAimlessIntent(PointerEventType eventType, SyntheticPressHandoff? press = null)
        {
            var completion = new UniTaskCompletionSource<SyntheticPointerOutcome>();

            world.Add(playerEntity, new SyntheticPointerEventIntent(-1, null, null, InputAction.IaSecondary, eventType, press)
            {
                Completion = completion,
            });

            return completion;
        }

        private UniTaskCompletionSource<SyntheticPointerOutcome> AddHoverIntent(float holdSecondsFromNow)
        {
            var completion = new UniTaskCompletionSource<SyntheticPointerOutcome>();

            SyntheticPointerEventIntent intent = SyntheticPointerEventIntent.Hover(targetEntity.Id, null, null, null, UnityEngine.Time.time + holdSecondsFromNow);
            intent.Completion = completion;
            world.Add(playerEntity, intent);

            return completion;
        }

        /// <summary>
        ///     Emulates the pipeline frame of an aimless post: the cursor ray stays in charge, so the raycast
        ///     publishes no synthetic-aim echo and hits whatever sits along the camera's forward direction.
        /// </summary>
        private void RunPipelineAimlessFrame(bool cursorHoversTarget)
        {
            ref SyntheticPointerInput synthetic = ref syntheticInput;
            Assert.That(synthetic.AimPoint, Is.Null, "an aimless post must keep the cursor ray");
            Assert.That(synthetic.IsPostedThisFrame, Is.True);
            synthetic = default(SyntheticPointerInput);

            ref PlayerOriginRaycastResultForSceneEntities raycastResult = ref world.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity);
            ref HoverStateComponent hoverState = ref world.Get<HoverStateComponent>(pipelineEntity);
            hoverState.Clear();

            var ray = new Ray(cameraGo.transform.position, cameraGo.transform.forward);
            raycastResult.SetRay(ray);

            if (cursorHoversTarget
                && Physics.Raycast(ray, out RaycastHit hit, PlayerOriginatedRaycastSystem.MAX_RAYCAST_DISTANCE)
                && collidersCache.TryGetSceneEntity(hit.collider, out GlobalColliderSceneEntityInfo info))
            {
                raycastResult.SetupHit(hit, info, hit.distance, hit.distance);
                hoverState.AssignCollider(hit.collider!, true, true);
            }
            else
                raycastResult.Reset();
        }

        [Test]
        public void PostButtonOnlyEdgeWithoutAim()
        {
            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddAimlessIntent(PointerEventType.PetDown);

            system.Update(0);

            Assert.That(syntheticInput.AimPoint, Is.Null);
            Assert.That(syntheticInput.PressButton, Is.EqualTo((InputAction?)InputAction.IaSecondary));

            RunPipelineAimlessFrame(cursorHoversTarget: false);
            system.Update(0);

            SyntheticPointerOutcome outcome = OutcomeOf(completion);
            Assert.That(outcome.Result.Hit, Is.False, "nothing was hovered: the edge went out as a global broadcast");
            Assert.That(outcome.Result.FailureReason, Is.Null);
            Assert.That(outcome.Result.UpRayMissed, Is.False);
            Assert.That(outcome.Press, Is.Not.Null);
            Assert.That(outcome.Press!.Value.Entity, Is.EqualTo(Entity.Null));
            Assert.That(outcome.Press.Value.Tick, Is.EqualTo(tick));
        }

        [Test]
        public void OrderAimlessReleaseOntoALaterTick()
        {
            UniTaskCompletionSource<SyntheticPointerOutcome> pressCompletion = AddAimlessIntent(PointerEventType.PetDown);

            system.Update(0);
            RunPipelineAimlessFrame(cursorHoversTarget: false);
            system.Update(0);

            SyntheticPointerOutcome pressOutcome = OutcomeOf(pressCompletion);

            UniTaskCompletionSource<SyntheticPointerOutcome> releaseCompletion = AddAimlessIntent(PointerEventType.PetUp, pressOutcome.Press);

            system.Update(0); // same tick: keeps waiting so PetUp lands on a later tick than PetDown
            Assert.That(releaseCompletion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(syntheticInput.ReleaseButton, Is.Null, "no button may be posted while the release waits for the tick");

            tick++;
            system.Update(0);

            Assert.That(syntheticInput.ReleaseButton, Is.EqualTo((InputAction?)InputAction.IaSecondary));
            Assert.That(syntheticInput.AimPoint, Is.Null);

            RunPipelineAimlessFrame(cursorHoversTarget: false);
            system.Update(0);

            SyntheticPointerResult releaseResult = ResultOf(releaseCompletion);
            Assert.That(releaseResult.FailureReason, Is.Null);
            Assert.That(releaseResult.UpRayMissed, Is.False, "an aimless release has no target to miss");
        }

        [Test]
        public void ReportTheHoveredEntityWhenAnAimlessEdgeLandsEntityBound()
        {
            // The camera happens to point straight at the target: the edge goes entity-bound, like a real key.
            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddAimlessIntent(PointerEventType.PetDown);

            system.Update(0);
            RunPipelineAimlessFrame(cursorHoversTarget: true);
            system.Update(0);

            SyntheticPointerResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.True);
            Assert.That(result.CrdtEntityId, Is.EqualTo(TARGET_CRDT_ID));
        }

        [Test]
        public void KeepRepostingTheHoverAimWithoutButtonsWhileTheHoldLasts()
        {
            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddHoverIntent(holdSecondsFromNow: 1000f);

            system.Update(0);

            Assert.That(syntheticInput.AimPoint, Is.EqualTo((Vector3?)targetGo.transform.position));
            Assert.That(syntheticInput.PressButton, Is.Null);
            Assert.That(syntheticInput.ReleaseButton, Is.Null);

            RunPipelineFrame();
            system.Update(0); // still holding: the aim is re-posted, no observation happens

            Assert.That(syntheticInput.AimPoint.HasValue, Is.True);
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(world.Has<SyntheticPointerEventIntent>(playerEntity), Is.True);
        }

        [Test]
        public void CompleteTheHoverWithHoverDiagnosticsOnceTheHoldExpires()
        {
            UniTaskCompletionSource<SyntheticPointerOutcome> completion = AddHoverIntent(holdSecondsFromNow: -1f);

            system.Update(0); // the hold is already over: post once and observe next frame

            Assert.That(syntheticInput.PressButton, Is.Null);
            Assert.That(syntheticInput.ReleaseButton, Is.Null);

            RunPipelineFrame(hoverText: "Open");
            system.Update(0);

            SyntheticPointerOutcome outcome = OutcomeOf(completion);
            Assert.That(outcome.Result.Hit, Is.True);
            Assert.That(outcome.Result.CrdtEntityId, Is.EqualTo(TARGET_CRDT_ID));
            Assert.That(outcome.Result.HoverText, Is.EqualTo("Open"));
            Assert.That(outcome.Press, Is.Null, "a hover delivers no press handoff");
            Assert.That(syntheticInput.AimPoint, Is.Null, "the aim stops being posted, so the hover leaves naturally");
        }

        [Test]
        public void AimThroughTheScreenPointWhenGiven()
        {
            Camera camera = cameraGo.GetComponent<Camera>();
            var screenCenter = new Vector2(camera.pixelWidth / 2f, camera.pixelHeight / 2f);
            Vector3 expectedAim = camera.ScreenPointToRay(screenCenter).GetPoint(PlayerOriginatedRaycastSystem.MAX_RAYCAST_DISTANCE);

            var completion = new UniTaskCompletionSource<SyntheticPointerOutcome>();

            world.Add(playerEntity, new SyntheticPointerEventIntent(-1, null, null, InputAction.IaPointer, PointerEventType.PetDown, screenPoint: screenCenter)
            {
                Completion = completion,
            });

            system.Update(0);

            Assert.That(syntheticInput.AimPoint.HasValue, Is.True);
            Assert.That(Vector3.Distance(syntheticInput.AimPoint!.Value, expectedAim), Is.LessThan(0.001f));

            // The centered camera looks straight at the target, so the screen-center ray must hit it.
            RunPipelineFrame();
            system.Update(0);

            SyntheticPointerResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.True);
            Assert.That(result.CrdtEntityId, Is.EqualTo(TARGET_CRDT_ID));
        }

        [Test]
        public void RefuseAScreenPointCoveredByUi()
        {
            Camera camera = cameraGo.GetComponent<Camera>();
            var screenCenter = new Vector2(camera.pixelWidth / 2f, camera.pixelHeight / 2f);
            uiCover = "MainUI/Sidebar/ExploreButton";

            var completion = new UniTaskCompletionSource<SyntheticPointerOutcome>();

            world.Add(playerEntity, new SyntheticPointerEventIntent(-1, null, null, InputAction.IaPointer, PointerEventType.PetDown, screenPoint: screenCenter)
            {
                Completion = completion,
            });

            system.Update(0);

            SyntheticPointerResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.BlockedByUi, Is.EqualTo("MainUI/Sidebar/ExploreButton"));
            Assert.That(result.FailureReason, Does.Contain("ui_click"), "the failure names the tool that can click it");
            Assert.That(syntheticInput.AimPoint, Is.Null, "nothing was aimed: a real click at that pixel lands on the UI");
        }

        [Test]
        public void AimThroughCoveringUiWhenForced()
        {
            Camera camera = cameraGo.GetComponent<Camera>();
            var screenCenter = new Vector2(camera.pixelWidth / 2f, camera.pixelHeight / 2f);
            uiCover = "MainUI/Sidebar/ExploreButton";

            var completion = new UniTaskCompletionSource<SyntheticPointerOutcome>();

            world.Add(playerEntity, new SyntheticPointerEventIntent(-1, null, null, InputAction.IaPointer, PointerEventType.PetDown, screenPoint: screenCenter, force: true)
            {
                Completion = completion,
            });

            system.Update(0);

            Assert.That(syntheticInput.AimPoint.HasValue, Is.True, "force aims past the cover");

            // Complete the gesture: an aim left posted on the shared pipeline entity leaks into the next test.
            RunPipelineFrame();
            system.Update(0);

            Assert.That(ResultOf(completion).Hit, Is.True);
        }

        [Test]
        public void NeverGateAWorldAimOnUiCover()
        {
            // The pipeline's UI bypass is correct for a world aim: the driver named a world target, not a pixel,
            // so a cover anywhere on screen is irrelevant. Gating it here would break click_entity.
            uiCover = "MainUI/Sidebar/ExploreButton";

            var completion = new UniTaskCompletionSource<SyntheticPointerOutcome>();

            world.Add(playerEntity, new SyntheticPointerEventIntent(targetEntity.Id, null, null, InputAction.IaPointer, PointerEventType.PetDown)
            {
                Completion = completion,
            });

            system.Update(0);

            Assert.That(syntheticInput.AimPoint.HasValue, Is.True, "a world aim is unaffected by UI cover");

            RunPipelineFrame();
            system.Update(0);

            Assert.That(ResultOf(completion).Hit, Is.True);
        }

        /// <summary>
        ///     Placing non-scene geometry (the skybox's collider, in the live client) on the ray and aiming at an
        ///     empty point in front of it. Reporting only the collider the ray met reads as if that object were in
        ///     the way; what actually happened is that the aim point held nothing.
        /// </summary>
        [Test]
        public void ReportAnEmptyAimPointRatherThanTheGeometryBeyondIt()
        {
            PlaceNonSceneGeometryAt(new Vector3(20f, 0f, 20f));

            var completion = new UniTaskCompletionSource<SyntheticPointerOutcome>();
            var aimPoint = new Vector3(10f, 0f, 10f);

            world.Add(playerEntity, new SyntheticPointerEventIntent(-1, null, aimPoint, InputAction.IaPointer, PointerEventType.PetDown)
            {
                Completion = completion,
            });

            system.Update(0);
            RunPipelineFrame();
            system.Update(0);

            SyntheticPointerResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("nothing at the aim point"));
            Assert.That(result.FailureReason, Does.Contain("further on"));
        }

        [Test]
        public void ReportNonSceneGeometryThatBlocksTheAimAsABlocker()
        {
            PlaceNonSceneGeometryAt(new Vector3(2.5f, 0f, 2.5f));

            var completion = new UniTaskCompletionSource<SyntheticPointerOutcome>();
            var aimPoint = new Vector3(10f, 0f, 10f);

            world.Add(playerEntity, new SyntheticPointerEventIntent(-1, null, aimPoint, InputAction.IaPointer, PointerEventType.PetDown)
            {
                Completion = completion,
            });

            system.Update(0);
            RunPipelineFrame();
            system.Update(0);

            SyntheticPointerResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("blocks the aim"));
            Assert.That(result.FailureReason, Does.Contain("before it"));
        }

        /// <summary>A screen-point aim is projected to the raycast limit, so no distance to it is worth reporting.</summary>
        [Test]
        public void ReportAScreenPointMissWithoutADistanceToTheAim()
        {
            Camera camera = cameraGo.GetComponent<Camera>();

            // Aim at a corner so the ray misses the target box entirely and only meets the non-scene geometry.
            PlaceNonSceneGeometryAt(new Vector3(-20f, 0f, 20f));
            var screenCorner = new Vector2(0f, camera.pixelHeight / 2f);

            var completion = new UniTaskCompletionSource<SyntheticPointerOutcome>();

            world.Add(playerEntity, new SyntheticPointerEventIntent(-1, null, null, InputAction.IaPointer, PointerEventType.PetDown, screenPoint: screenCorner)
            {
                Completion = completion,
            });

            system.Update(0);
            RunPipelineFrame();
            system.Update(0);

            SyntheticPointerResult result = ResultOf(completion);
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FailureReason, Does.Contain("nothing clickable at that point"));
            Assert.That(result.FailureReason, Does.Not.Contain(" m "), "a screen-point aim has no meaningful distance to report");
        }

        /// <summary>
        ///     The frames between a press and its release belong to no intent, and a driver has no hardware
        ///     pointer sitting on the target — so the press pixel is parked for the pipeline (and for the
        ///     PBPrimaryPointerInfo ray a scene samples) to keep building the reticle ray through it.
        /// </summary>
        [Test]
        public void ParkThePointerAtThePressedPixelWhileTheButtonIsHeld()
        {
            DeliverPress();

            Assert.That(world.Has<SyntheticPointerHold>(playerEntity), Is.True);
            Assert.That(SyntheticCursorState.TryGetPointerPosition(out Vector2 parked), Is.True);
            Assert.That(parked, Is.EqualTo(ScreenPointOfTarget()));
        }

        [Test]
        public void KeepAssertingTheParkedPointerOnFramesThatCarryNoIntent()
        {
            DeliverPress();

            // The assertion is frame-scoped, so only re-stating it every frame keeps the pointer on the
            // gesture for the whole camera sweep a held press is turned into.
            SyntheticCursorState.Reset();
            system.Update(0);

            Assert.That(SyntheticCursorState.TryGetPointerPosition(out Vector2 parked), Is.True);
            Assert.That(parked, Is.EqualTo(ScreenPointOfTarget()));
        }

        [Test]
        public void HandThePointerBackWhenTheReleaseIsDelivered()
        {
            SyntheticPointerOutcome press = DeliverPress();

            UniTaskCompletionSource<SyntheticPointerOutcome> releaseCompletion = AddIntent(PointerEventType.PetUp, press: press.Press);

            tick++;
            system.Update(0); // inject the release
            RunPipelineFrame();
            system.Update(0); // observe

            Assert.That(ResultOf(releaseCompletion).Hit, Is.True);
            Assert.That(world.Has<SyntheticPointerHold>(playerEntity), Is.False);

            SyntheticCursorState.Reset();
            system.Update(0);

            Assert.That(SyntheticCursorState.TryGetPointerPosition(out _), Is.False, "the hardware mouse owns the pointer again once the button is up");
        }

        [Test]
        public void NotParkThePointerForAnAimlessPress()
        {
            // An aimless edge names no target, so there is no pixel of the driver's choosing to park at:
            // the cursor ray stays in charge, exactly as it does for the delivery itself.
            AddAimlessIntent(PointerEventType.PetDown);

            system.Update(0);
            RunPipelineAimlessFrame(cursorHoversTarget: true);
            system.Update(0);

            Assert.That(world.Has<SyntheticPointerHold>(playerEntity), Is.False);
            Assert.That(SyntheticCursorState.TryGetPointerPosition(out _), Is.False);
        }

        [Test]
        public void NotParkThePointerWhenThePressIsAimedOffScreen()
        {
            // A world aim needs no line of sight, so a driver can press on something behind the camera —
            // there is no pixel a human could have pressed, and projecting one would invent a position.
            targetGo.transform.position = new Vector3(0f, 0f, -5f);
            Physics.SyncTransforms();

            DeliverPress();

            Assert.That(world.Has<SyntheticPointerHold>(playerEntity), Is.False);
            Assert.That(SyntheticCursorState.TryGetPointerPosition(out _), Is.False);
        }

        [Test]
        public void DropAParkedPointerWhoseReleaseNeverArrived()
        {
            DeliverPress();

            // An abandoned gesture (a driver that died between the legs) must not keep the pointer away
            // from the hardware mouse forever.
            world.Set(playerEntity, new SyntheticPointerHold { ScreenPosition = Vector2.one, ExpiryTime = UnityEngine.Time.time - 1f });
            SyntheticCursorState.Reset();

            system.Update(0);

            Assert.That(world.Has<SyntheticPointerHold>(playerEntity), Is.False);
            Assert.That(SyntheticCursorState.TryGetPointerPosition(out _), Is.False);
        }

        /// <summary>Where the target's aim point sits on screen, which is the pixel a press on it occupies.</summary>
        private Vector2 ScreenPointOfTarget()
        {
            Vector3 projected = cameraGo.GetComponent<Camera>().WorldToScreenPoint(targetGo.transform.position);
            return new Vector2(projected.x, projected.y);
        }

        private void PlaceNonSceneGeometryAt(Vector3 position)
        {
            nonSceneGo = new GameObject("non-scene-geometry") { transform = { position = position, localScale = new Vector3(4f, 4f, 4f) } };
            nonSceneGo.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
        }
    }
}
