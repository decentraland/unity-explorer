using Arch.Core;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Tables;
using DCL.AvatarRendering.Emotes;
using DCL.Prefs;
using NSubstitute;
using NUnit.Framework;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Tests
{
    /// <summary>
    ///     Regression coverage for unity-explorer#9588 (sender side): while a joining player is still on the
    ///     loading screen, <see cref="Character.CharacterMotion.Systems.TeleportCharacterSystem" /> (Pending /
    ///     !IsPositionSet branches) already relocates the character to the scene spawn point every frame, but
    ///     <c>JustTeleported</c> is only added once loading finishes (<c>ResolveAsSuccess</c>). Before the fix,
    ///     <see cref="PlayerMovementNetSendSystem" /> reads only <c>PlayerTeleportIntent.JustTeleported</c> to
    ///     decide <c>isInstant</c>, so every loading-phase broadcast of the spawn position goes out as ordinary
    ///     (non-instant) movement, which is exactly what lets the observer-side interpolation bug
    ///     (see <see cref="RemotePlayersMovementSystemTeleportDequeueShould" />) play out as a visible slide.
    /// </summary>
    [TestFixture]
    public class PlayerMovementNetSendSystemLoadingTeleportFlagShould
    {
        private World world;
        private Entity entity;
        private PlayerMovementNetSendSystem system;
        private IMovementMessageBus movementMessageBus;
        private GameObject characterGameObject;
        private LiveKitMovementMessageBus liveKitMessageBus;
        private MultiplayerMovementSettings settings;
        private MultiplayerDebugSettings debugSettings;

        [SetUp]
        public void SetUp()
        {
            // Established test pattern for reading/writing DCLPlayerPrefs from a plain EditMode [Test]
            // (RuntimeInitializeOnLoadMethod never fires outside Play Mode) - see
            // ChatReactionRecentsServiceShould / HomeMarkerControllerShould / RealmLaunchSettingsHomePositionOverrideShould.
            typeof(DCLPlayerPrefs)
               .GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!
               .SetValue(null, new InMemoryDCLPlayerPrefs());

            world = World.Create();

            characterGameObject = new GameObject("SelfPlayer");
            CharacterController characterController = characterGameObject.AddComponent<CharacterController>();

            IMessagePipesHub messagePipesHub = Substitute.For<IMessagePipesHub>();
            messagePipesHub.IslandPipe().Returns(Substitute.For<IMessagePipe>());
            messagePipesHub.ScenePipe().Returns(Substitute.For<IMessagePipe>());

            var movementInbox = new MovementInbox(Substitute.For<IReadOnlyEntityParticipantTable>(), world);

            var broadcaster = new LiveKitMessagesBroadcaster(
                Substitute.For<IGateKeeperSceneRoom>(),
                messagePipesHub,
                new PulseActivation(false));

            liveKitMessageBus = new LiveKitMovementMessageBus(messagePipesHub, movementInbox, broadcaster);

            movementMessageBus = Substitute.For<IMovementMessageBus>();

            settings = ScriptableObject.CreateInstance<MultiplayerMovementSettings>();
            settings.MoveSendRate = 0.1f;
            settings.StandSendRate = 1f;
            settings.VelocityTiers = new float[0]; // avoid NRE in VelocityTierFromSpeed; not under test here

            debugSettings = ScriptableObject.CreateInstance<MultiplayerDebugSettings>();
            debugSettings.SelfSending = false; // keep the unused `liveKitMessageBus` field fully inert

            system = new PlayerMovementNetSendSystem(world, liveKitMessageBus, movementMessageBus, settings, debugSettings);

            entity = world.Create(
                new PlayerMovementNetworkComponent(characterController),
                new CharacterAnimationComponent(),
                new StunComponent(),
                new MovementInputComponent(),
                new CharacterEmoteComponent(),
                new HeadIKComponent(),
                new HandPointAtComponent());
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            Object.DestroyImmediate(characterGameObject);
            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(debugSettings);
            liveKitMessageBus.Dispose();

            typeof(DCLPlayerPrefs)
               .GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!
               .SetValue(null, null);
        }

        [Test]
        public void MarkBroadcastInstantWhileTeleportIntentIsPending()
        {
            // First tick is unconditionally isInstant: true (PlayerMovementNetworkComponent.IsFirstMessage) and
            // does not exercise the seam under test - drain it first.
            system.Update(0.016f);
            movementMessageBus.ClearReceivedCalls();

            // Simulate the loading-phase window: TeleportCharacterSystem's Pending branch has already moved the
            // character to the spawn point, but JustTeleported is only added later in ResolveAsSuccess - so at
            // this point only the bare (non-JustTeleported) PlayerTeleportIntent is present on the entity, exactly
            // as it is for the whole duration of another player's loading screen.
            var teleportIntent = new PlayerTeleportIntent(
                sceneDef: null,
                parcel: Vector2Int.zero,
                position: Vector3.zero,
                cancellationToken: CancellationToken.None);

            world.Add(entity, teleportIntent);

            // Force a second SendMessage deterministically without depending on UnityEngine.Time.unscaledTime
            // (which does not advance within a synchronous EditMode [Test]): flipping IsGrounded takes the
            // unconditional "animation state changed" branch (PlayerMovementNetSendSystem.cs:79-84), which calls
            // SendMessage regardless of the MoveSendRate/StandSendRate timing gate.
            ref CharacterAnimationComponent anim = ref world.Get<CharacterAnimationComponent>(entity);
            anim.States.IsGrounded = true;
            world.Set(entity, anim);

            system.Update(0.016f);

            NetworkMovementMessage sent = default;
            movementMessageBus.Received(1).Send(Arg.Do<NetworkMovementMessage>(m => sent = m));

            // PIN (bug): PlayerMovementNetSendSystem.cs:77 reads only World.Has<PlayerTeleportIntent.JustTeleported>,
            // which is never added until the load finishes -> isInstant is false for every loading-phase broadcast.
            // FIX: `|| World.Has<PlayerTeleportIntent>(entity)` also covers the Pending window -> isInstant is true.
            Assert.IsTrue(sent.isInstant,
                "A movement broadcast sent while a PlayerTeleportIntent (loading/teleport) is pending on the " +
                "entity should be marked isInstant so observers snap instead of interpolating the loading-phase " +
                "position jump.");
        }
    }
}
