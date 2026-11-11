using Arch.Core;
using CRDT;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.SyntheticInput.Systems;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using InputAction = UnityEngine.InputSystem.InputAction;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.SyntheticInput.Tests
{
    public class SyntheticReticleReaderShould
    {
        private World globalWorld = null!;
        private World sceneWorld = null!;
        private GameObject cameraGo = null!;
        private Entity cameraEntity;
        private Entity pipelineEntity;
        private IScenesCache scenesCache = null!;
        private SyntheticReticleReader reader = null!;

        [SetUp]
        public void SetUp()
        {
            globalWorld = World.Create();
            sceneWorld = World.Create();

            cameraGo = new GameObject("reticle-test-camera");
            cameraEntity = globalWorld.Create(new CameraComponent(cameraGo.AddComponent<Camera>()), new CursorComponent { CursorState = CursorState.Locked, Position = new Vector2(10f, 20f) });

            pipelineEntity = globalWorld.Create(
                new PlayerOriginRaycastResultForSceneEntities(),
                new PlayerOriginRaycastResultForGlobalEntities(),
                new HoverStateComponent(),
                new HoverFeedbackComponent(4));

            scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(null));

            reader = new SyntheticReticleReader(globalWorld, scenesCache);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraGo);
            sceneWorld.Dispose();
            globalWorld.Dispose();
        }

        private ref PlayerOriginRaycastResultForSceneEntities sceneResult => ref globalWorld.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity);

        private GlobalColliderSceneEntityInfo SceneEntity(int crdtId, PBPointerEvents? pointerEvents)
        {
            Entity entity = pointerEvents != null
                ? sceneWorld.Create(new CRDTEntity(crdtId), pointerEvents)
                : sceneWorld.Create(new CRDTEntity(crdtId));

            return new GlobalColliderSceneEntityInfo(new SceneEcsExecutor(sceneWorld), new ColliderSceneEntityInfo(entity, new CRDTEntity(crdtId), ColliderLayer.ClPointer));
        }

        private static PBPointerEvents PointerEventsWithMaxDistance(float maxDistance)
        {
            var events = new PBPointerEvents();
            events.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info { MaxDistance = maxDistance, HoverText = "Open" },
            });
            return events;
        }

        [Test]
        public void ReportNothingWhenTheRayReachedNoEntity()
        {
            ReticleReading reading = reader.Read();

            Assert.That(reading.Available, Is.True);
            Assert.That(reading.Kind, Is.EqualTo(ReticleTargetKind.Nothing));
            Assert.That(reading.SceneEntityId, Is.EqualTo(-1));
        }

        [Test]
        public void UseTheScreenCentreWhileTheCursorIsLocked()
        {
            ReticleReading reading = reader.Read();

            Assert.That(reading.CursorLocked, Is.True);
            Assert.That(reading.RayFromScreenCenter, Is.True);
        }

        [Test]
        public void UseTheCursorPixelWhileTheCursorIsFree()
        {
            globalWorld.Get<CursorComponent>(cameraEntity).CursorState = CursorState.Free;

            ReticleReading reading = reader.Read();

            Assert.That(reading.CursorLocked, Is.False);
            Assert.That(reading.RayFromScreenCenter, Is.False);
            Assert.That(reading.PointerImagePoint.x, Is.EqualTo(10f));
        }

        [Test]
        public void BeUnavailableWhileTheCursorPansTheCamera()
        {
            globalWorld.Get<CursorComponent>(cameraEntity).CursorState = CursorState.Panning;

            ReticleReading reading = reader.Read();

            Assert.That(reading.Available, Is.False);
            Assert.That(reading.UnavailableReason, Does.Contain("panning"));
        }

        [Test]
        public void ReportASceneEntityWithItsIdsRangeAndHoverText()
        {
            GlobalColliderSceneEntityInfo entityInfo = SceneEntity(crdtId: 530, PointerEventsWithMaxDistance(10f));
            sceneResult.SetupHit(default(RaycastHit), entityInfo, hitDistance: 3f, playerDistance: 2.5f);

            ReticleReading reading = reader.Read();

            Assert.That(reading.Kind, Is.EqualTo(ReticleTargetKind.SceneEntity));
            Assert.That(reading.CrdtEntityId, Is.EqualTo(530));
            Assert.That(reading.SceneEntityId, Is.EqualTo(entityInfo.ColliderSceneEntityInfo.EntityReference.Id));
            Assert.That(reading.DistanceToCamera, Is.EqualTo(3f));
            Assert.That(reading.DistanceToPlayer, Is.EqualTo(2.5f));
            Assert.That(reading.HasPointerEvents, Is.True);
            Assert.That(reading.WithinRange, Is.True);
            Assert.That(reading.HoverText, Is.EqualTo("Open"), "the hover text comes from the entity's cursor entry when no tooltip is shown");
            Assert.That(reading.SceneId, Is.Null, "the entity's world is not the current scene's");
        }

        [Test]
        public void ReportAnEntityOutOfItsOwnRange()
        {
            sceneResult.SetupHit(default(RaycastHit), SceneEntity(crdtId: 531, PointerEventsWithMaxDistance(2f)), hitDistance: 8f, playerDistance: 7.5f);

            ReticleReading reading = reader.Read();

            Assert.That(reading.Kind, Is.EqualTo(ReticleTargetKind.SceneEntity));
            Assert.That(reading.WithinRange, Is.False, "a press from here would be refused by the entity's maxDistance");
        }

        [Test]
        public void ReportAnEntityWithoutPointerEvents()
        {
            sceneResult.SetupHit(default(RaycastHit), SceneEntity(crdtId: 532, null), hitDistance: 3f, playerDistance: 2f);

            ReticleReading reading = reader.Read();

            Assert.That(reading.Kind, Is.EqualTo(ReticleTargetKind.SceneEntity));
            Assert.That(reading.HasPointerEvents, Is.False);
            Assert.That(reading.WithinRange, Is.False);
            Assert.That(reading.HoverText, Is.Null);
        }

        [Test]
        public void PreferTheShownTooltipOverTheDeclaredHoverText()
        {
            sceneResult.SetupHit(default(RaycastHit), SceneEntity(crdtId: 533, PointerEventsWithMaxDistance(10f)), hitDistance: 3f, playerDistance: 2f);
            globalWorld.Get<HoverFeedbackComponent>(pipelineEntity).Add(new HoverFeedbackComponent.Tooltip("E Open", new InputAction("open")));

            ReticleReading reading = reader.Read();

            Assert.That(reading.HoverText, Is.EqualTo("E Open"));
            Assert.That(reading.Tooltips, Is.EqualTo(new[] { "E Open" }));
        }

        [Test]
        public void ReportAnAvatarFromTheGlobalResult()
        {
            Entity avatar = globalWorld.Create();
            globalWorld.Get<PlayerOriginRaycastResultForGlobalEntities>(pipelineEntity).SetupHit(default(RaycastHit), new GlobalColliderGlobalEntityInfo(avatar), distance: 4f);

            ReticleReading reading = reader.Read();

            Assert.That(reading.Kind, Is.EqualTo(ReticleTargetKind.Avatar));
            Assert.That(reading.GlobalEntityId, Is.EqualTo(avatar.Id));
            Assert.That(reading.DistanceToCamera, Is.EqualTo(4f));
        }

        [Test]
        public void EmitEveryWireKeyWhateverTheKind()
        {
            Newtonsoft.Json.Linq.JObject nothing = reader.Read().ToJson();
            sceneResult.SetupHit(default(RaycastHit), SceneEntity(crdtId: 534, PointerEventsWithMaxDistance(10f)), hitDistance: 3f, playerDistance: 2f);
            Newtonsoft.Json.Linq.JObject entity = reader.Read().ToJson();

            var nothingKeys = new System.Collections.Generic.List<string>();
            var entityKeys = new System.Collections.Generic.List<string>();

            foreach (Newtonsoft.Json.Linq.JProperty property in nothing.Properties()) nothingKeys.Add(property.Name);
            foreach (Newtonsoft.Json.Linq.JProperty property in entity.Properties()) entityKeys.Add(property.Name);

            CollectionAssert.AreEquivalent(entityKeys, nothingKeys);
            Assert.That(nothing["kind"]?.ToString(), Is.EqualTo("nothing"));
            Assert.That(entity["kind"]?.ToString(), Is.EqualTo("scene_entity"));
        }
    }
}
