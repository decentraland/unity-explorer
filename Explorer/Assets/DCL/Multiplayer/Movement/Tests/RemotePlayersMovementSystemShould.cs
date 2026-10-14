using Arch.Core;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Tables;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement.Tests
{
    public class RemotePlayersMovementSystemShould : UnitySystemTestBase<RemotePlayersMovementSystem>
    {
        private Entity entity;
        private GameObject avatar = null!;
        private RemotePlayerInterpolationSettings interpolationSettings = null!;
        private ICharacterControllerSettings characterSettings = null!;

        [SetUp]
        public void SetUp()
        {
            interpolationSettings = ScriptableObject.CreateInstance<RemotePlayerInterpolationSettings>();
            interpolationSettings.UseSpeedUp = false;
            var settings = Substitute.For<IMultiplayerMovementSettings>();
            settings.MoveSendRate.Returns(1f);
            settings.InterpolationSettings.Returns(interpolationSettings);
            settings.MinTeleportDistance.Returns(0f);
            characterSettings = Substitute.For<ICharacterControllerSettings>();
            system = new RemotePlayersMovementSystem(world, settings, characterSettings);
            avatar = new GameObject("Remote movement test");
            avatar.transform.position = new Vector3(9, 0, 0);
            var pool = new ObjectPool<SimplePriorityQueue<NetworkMovementMessage, double>>(
                () => new SimplePriorityQueue<NetworkMovementMessage, double>());
            var movement = new RemotePlayerMovementComponent(pool)
            {
                Initialized = true,
                InitialCooldownTime = 10,
                PastMessage = Message(100, 9),
            };
            entity = world.Create(new CharacterTransform(avatar.transform), new HeadIKComponent(), movement,
                new InterpolationComponent(), new ExtrapolationComponent(), new HandPointAtComponent());
        }

        protected override void OnTearDown()
        {
            world.Get<RemotePlayerMovementComponent>(entity).Dispose();
            Object.DestroyImmediate(avatar);
            Object.DestroyImmediate(interpolationSettings);
        }

        [Test]
        public void ApplyAnOlderTimelineResetBeforeActiveInterpolationAndExtrapolation()
        {
            var interpolation = new InterpolationComponent();
            interpolation.Restart(Message(100, 9), Message(101, 10), InterpolationType.Linear, characterSettings);
            var extrapolation = new ExtrapolationComponent();
            extrapolation.Restart(Message(100, 9), 10);
            world.Set(entity, interpolation);
            world.Set(entity, extrapolation);
            world.Get<RemotePlayerMovementComponent>(entity).Enqueue(Message(1, 3, resetTimeline: true));

            system.Update(0.01f);

            Assert.AreEqual(new Vector3(3, 0, 0), avatar.transform.position);
            Assert.AreEqual(1, world.Get<RemotePlayerMovementComponent>(entity).PastMessage.timestamp);
            Assert.IsFalse(world.Get<InterpolationComponent>(entity).Enabled);
            Assert.IsFalse(world.Get<ExtrapolationComponent>(entity).Enabled);

            ref RemotePlayerMovementComponent movement = ref world.Get<RemotePlayerMovementComponent>(entity);
            movement.InitialCooldownTime = 10;
            movement.Enqueue(Message(2, 6));
            system.Update(0.01f);

            Assert.AreEqual(new Vector3(6, 0, 0), avatar.transform.position);
            Assert.AreEqual(2, world.Get<RemotePlayerMovementComponent>(entity).PastMessage.timestamp);
        }

        [Test]
        public void KeepTheTimelineResetWhenTheInboxExceedsItsMessageCap()
        {
            ref RemotePlayerMovementComponent movement = ref world.Get<RemotePlayerMovementComponent>(entity);
            movement.Enqueue(Message(110, 10));
            movement.Enqueue(Message(1, 3, resetTimeline: true));
            for (var i = 2; i <= 30; i++)
                movement.Enqueue(Message(i, 3));

            system.Update(0.01f);

            Assert.AreEqual(new Vector3(3, 0, 0), avatar.transform.position);
            Assert.AreEqual(1, world.Get<RemotePlayerMovementComponent>(entity).PastMessage.timestamp);
            Assert.That(world.Get<RemotePlayerMovementComponent>(entity).Queue!.Count, Is.LessThanOrEqualTo(10));
        }

        [TestCase(2d, 6f)]
        [TestCase(0.5d, 3f)]
        public void PreserveTheResetWhenPendingMovementReceivesAnotherSample(double laterTimestamp, float expectedX)
        {
            const string WALLET = "wallet";
            var participants = Substitute.For<IReadOnlyEntityParticipantTable>();
            var inbox = new MovementInbox(participants, world);
            inbox.Enqueue(Message(1, 3, resetTimeline: true), WALLET);
            inbox.Enqueue(Message(laterTimestamp, 6), WALLET);
            inbox.DrainToEntities();
            participants.TryGet(WALLET, out Arg.Any<IReadOnlyEntityParticipantTable.Entry>())
                        .Returns(ci =>
                         {
                             ci[1] = new IReadOnlyEntityParticipantTable.Entry(WALLET, entity, RoomSource.Pulse);
                             return true;
                         });

            inbox.TryFlushPending(WALLET);
            system.Update(0.01f);

            Assert.AreEqual(new Vector3(expectedX, 0, 0), avatar.transform.position);
            Assert.AreEqual(laterTimestamp < 1 ? 1 : laterTimestamp, world.Get<RemotePlayerMovementComponent>(entity).PastMessage.timestamp);
        }

        [Test]
        public void ContinueDroppingOrdinaryOutOfOrderMovementWithoutATimelineReset()
        {
            world.Get<RemotePlayerMovementComponent>(entity).Enqueue(Message(1, 3));

            system.Update(0.01f);

            Assert.AreEqual(new Vector3(9, 0, 0), avatar.transform.position);
            Assert.AreEqual(100, world.Get<RemotePlayerMovementComponent>(entity).PastMessage.timestamp);
            Assert.That(world.Get<RemotePlayerMovementComponent>(entity).Queue, Is.Empty);
        }

        private static NetworkMovementMessage Message(double timestamp, float x, bool resetTimeline = false) =>
            new ()
            {
                timestamp = timestamp,
                position = new Vector3(x, 0, 0),
                isInstant = true,
                ResetTimeline = resetTimeline,
            };
    }
}
