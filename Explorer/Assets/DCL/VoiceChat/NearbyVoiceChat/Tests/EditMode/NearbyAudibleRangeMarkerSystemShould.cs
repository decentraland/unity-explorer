using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Documents the contract of <see cref="NearbyAudibleRangeMarkerSystem"/>: the two
    /// avatar-level markers (<see cref="InAudibleRangeTag"/>, <see cref="IsSuspendedTag"/>) are
    /// reconciled once per tick from the local-player ↔ avatar distance with hysteresis.
    /// Invariants:
    /// I1: <c>IsSuspendedTag ⊆ InAudibleRangeTag</c>.
    /// I2: <c>InAudibleRangeTag ⊆ StreamingAudioComponent</c>.
    /// </summary>
    public class NearbyAudibleRangeMarkerSystemShould : UnitySystemTestBase<NearbyAudibleRangeMarkerSystem>
    {
        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly List<GameObject> gameObjects = new (16);

        private Entity cameraEntity;
        private Entity playerEntity;
        private Camera camera = null!;

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            var cameraGo = CreateTrackedGameObject("TestCamera");
            cameraGo.transform.position = Vector3.zero;
            camera = cameraGo.AddComponent<Camera>();
            cameraEntity = world.Create(new CameraComponent(camera));

            var playerFocusGo = CreateTrackedGameObject("PlayerFocus");
            playerFocusGo.transform.position = Vector3.zero;
            playerEntity = world.Create(new PlayerComponent(playerFocusGo.transform));

            system = new NearbyAudibleRangeMarkerSystem(world);
            system.Initialize();
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();
            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        // ── Outer boundary — Query A / Query B / hysteresis ─────────

        [Test]
        public void AppliesAudibleRangeWhenStreamingAvatarInsideOuterInDistance()
        {
            Entity e = CreateStreamingAvatar(distance: 17f);

            system.Update(0);

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True);
        }

        [Test]
        public void DoesNotApplyAudibleRangeWhenDistanceAboveOuterIn()
        {
            Entity e = CreateStreamingAvatar(distance: 19f);

            system.Update(0);

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.False);
        }

        [Test]
        public void RemovesAudibleRangeWhenStreamingAvatarCrossesOuterOut()
        {
            // Bring entity into the in-range archetype, then move to outer-out band.
            Entity e = CreateStreamingAvatar(distance: 17f);
            system.Update(0);
            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True, "precondition: tag applied");

            MoveAvatar(e, distance: 23f);

            system.Update(0);

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.False);
        }

        [Test]
        public void KeepsAudibleRangeWithinHysteresisBandOuter()
        {
            // Hysteresis: 18 in / 22 out. At 19 m (between thresholds) tag must persist.
            Entity e = CreateStreamingAvatar(distance: 17f);
            system.Update(0);
            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True, "precondition: tag applied");

            MoveAvatar(e, distance: 19f);

            system.Update(0);

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True);
        }

        // ── Suspend boundary — Query C / Query D / hysteresis ───────

        [Test]
        public void CascadeRemovesSuspendedWhenAudibleRangeRemovedSameTick()
        {
            // Bring avatar into the suspend band: at 17 m it picks up both tags in one Update
            // (Query A → Query C). Moving to 23 m must drop both via Query B's cascade in a single tick.
            Entity e = CreateStreamingAvatar(distance: 17f);
            system.Update(0);
            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True, "precondition: audible tag applied");
            Assert.That(world.Has<IsSuspendedTag>(e), Is.True, "precondition: suspended tag applied");

            MoveAvatar(e, distance: 23f);

            system.Update(0);

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.False);
            Assert.That(world.Has<IsSuspendedTag>(e), Is.False, "cascade must drop suspended when audible is removed (invariant I1)");
        }

        [Test]
        public void AppliesSuspendedWhenInAudibleAndDistanceInOuterBand()
        {
            // Seed the in-audible archetype, then move into the suspend band so Query C fires.
            Entity e = CreateStreamingAvatar(distance: 15f);
            system.Update(0);
            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True, "precondition: audible tag applied");
            Assert.That(world.Has<IsSuspendedTag>(e), Is.False, "precondition: not yet suspended");

            MoveAvatar(e, distance: 19f);

            system.Update(0);

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True);
            Assert.That(world.Has<IsSuspendedTag>(e), Is.True);
        }

        [Test]
        public void DoesNotApplySuspendedWhenInsideSuspendIn()
        {
            Entity e = CreateStreamingAvatar(distance: 15f);

            system.Update(0);

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True);
            Assert.That(world.Has<IsSuspendedTag>(e), Is.False);
        }

        [Test]
        public void RemovesSuspendedWhenDistanceDropsBelowSuspendIn()
        {
            // Avatar enters straight into the suspend band on inward approach (Query A adds
            // InAudibleRangeTag at 18 m; Query C sees sqr ≥ 17² and adds IsSuspendedTag in the
            // same tick). Then moving to 15 m triggers Query D (sqr < 16²) → suspend removed.
            Entity e = CreateStreamingAvatar(distance: 18f);
            system.Update(0);
            Assert.That(world.Has<IsSuspendedTag>(e), Is.True, "precondition: suspended tag applied");

            MoveAvatar(e, distance: 15f);

            system.Update(0);

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True, "audible tag must persist");
            Assert.That(world.Has<IsSuspendedTag>(e), Is.False);
        }

        [Test]
        public void KeepsSuspendedWithinHysteresisBandInner()
        {
            // Hysteresis: 16 in / 17 out. At 16.5 m (between thresholds) suspended must persist.
            // Seed at 18 m (avatar enters audible-range and suspend band in the same tick), then
            // move into the hysteresis band — Query D early-returns because sqr ≥ 16².
            Entity e = CreateStreamingAvatar(distance: 18f);
            system.Update(0);
            Assert.That(world.Has<IsSuspendedTag>(e), Is.True, "precondition: suspended tag applied");

            MoveAvatar(e, distance: 16.5f);

            system.Update(0);

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True);
            Assert.That(world.Has<IsSuspendedTag>(e), Is.True);
        }

        // ── Edge cases ──────────────────────────────────────────────

        [Test]
        public void DoesNotTagNonStreamingAvatar()
        {
            // No StreamingAudioComponent — invariant I2 prevents any range marker from materializing.
            Entity e = CreateAvatarEntityAtDistance("wallet-no-stream", distance: 10f);

            system.Update(0);

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.False);
            Assert.That(world.Has<IsSuspendedTag>(e), Is.False);
        }

        [Test]
        public void SkipsAvatarWithDeleteEntityIntention()
        {
            Entity e = CreateStreamingAvatar(distance: 10f);
            world.Add<DeleteEntityIntention>(e);

            system.Update(0);

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.False);
            Assert.That(world.Has<IsSuspendedTag>(e), Is.False);
        }

        [Test]
        public void DoesNotRevisitEntityAfterStructuralChangeInSameQuery()
        {
            // Filter-trick guard: Query A's [None<InAudibleRangeTag>] excludes the entity from
            // its own iterator after `World.Add` migrates it; otherwise `World.Add<T>` on an
            // already-tagged entity would throw. Symmetric on suspend (Query C vs IsSuspendedTag).
            // Single Update brings an avatar at 18 m through the chain in one pass:
            // (no markers → InAudibleRangeTag → IsSuspendedTag) — covers both filter tricks.
            Entity e = CreateStreamingAvatar(distance: 18f);

            Assert.DoesNotThrow(() => system.Update(0));

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True);
            Assert.That(world.Has<IsSuspendedTag>(e), Is.True);
        }

        [Test]
        public void IdempotentWhenStateUnchangedAcrossTicks()
        {
            // Pure active band — distance well inside SUSPEND_IN (16 m) so only InAudibleRangeTag
            // applies. Multiple ticks must leave the tag set untouched.
            Entity e = CreateStreamingAvatar(distance: 14f);

            system.Update(0);
            system.Update(0);
            system.Update(0);

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True);
            Assert.That(world.Has<IsSuspendedTag>(e), Is.False);
        }

        [Test]
        public void ListenerPositionInThirdPersonUsesCameraFocusNotCamera()
        {
            // Camera and CameraFocus are co-located at SetUp; in third-person the helper must
            // read CameraFocus, not the camera transform. Move the camera far away to break
            // any "use camera in third-person" implementation; the avatar is positioned 17 m
            // from the focus (in-range) but 100 m from the camera (way out-of-range).
            ref CameraComponent cam = ref world.Get<CameraComponent>(cameraEntity);
            cam.Mode = CameraMode.ThirdPerson;
            camera.transform.position = new Vector3(100f, 0f, 0f);

            // CameraFocus stays at origin — the player-component anchor.
            Entity e = CreateStreamingAvatar(distance: 17f);

            system.Update(0);

            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True,
                "third-person helper must evaluate distance against PlayerComponent.CameraFocus");
        }

        // ── Helpers ─────────────────────────────────────────────────

        private Entity CreateStreamingAvatar(float distance)
        {
            string wallet = $"wallet-{distance}";
            Entity e = CreateAvatarEntityAtDistance(wallet, distance);
            world.Add(e, new StreamingAudioComponent(new[] { "sid-1" }));
            return e;
        }

        private Entity CreateAvatarEntityAtDistance(string walletId, float distance)
        {
            var avatarGo = CreateTrackedGameObject($"Avatar_{walletId}");
            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var headAnchorGo = CreateTrackedGameObject($"HeadAnchor_{walletId}");
            headAnchorGo.transform.SetParent(avatarGo.transform, worldPositionStays: false);
            headAnchorGo.transform.position = new Vector3(distance, 0f, 0f);
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, headAnchorGo.transform);

            return world.Create(new Profile(walletId, walletId, new Avatar()), avatarBase);
        }

        private void MoveAvatar(Entity entity, float distance)
        {
            AvatarBase avatarBase = world.Get<AvatarBase>(entity);
            avatarBase.HeadAnchorPoint.position = new Vector3(distance, 0f, 0f);
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }
    }
}
