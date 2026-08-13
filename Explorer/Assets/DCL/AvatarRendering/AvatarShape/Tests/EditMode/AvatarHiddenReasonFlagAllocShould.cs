using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Friends.UserBlocking;
using DCL.Quality;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    // Pins the bitwise flag tests in AvatarShapeVisibilitySystem.SetHiddenComponent. The two checks are the direct
    // bitwise forms `(Reason & hiddenReason) == 0` / `!= 0` (equivalent to Enum.HasFlag for the single-flag values
    // both call sites pass), so steady-state calls must be structural no-ops: (true, Blocked) leaves an already-set
    // bit alone, and (false, Banned) removes nothing because that bit is clear. Every entity below starts with
    // HiddenPlayerComponent{Reason=Blocked} and must end each pass with that exact component untouched.
    public class AvatarHiddenReasonFlagAllocShould : UnitySystemTestBase<AvatarShapeVisibilitySystem>
    {
        private const int BLOCKED_AVATARS = 100;

                private Entity[] entities = null!;

        [SetUp]
        public void SetUp()
        {
            system = new AvatarShapeVisibilitySystem(
                world,
                Substitute.For<IUserBlockingCache>(),
                Substitute.For<IRendererFeaturesCache>(),
                startFadeDithering: 2.0f,
                endFadeDithering: 0.5f,
                includeBannedUsersFromScene: false);

            // No system.Initialize() on purpose: SetHiddenComponent never touches the camera singleton, and skipping
            // Initialize keeps this test free of any scene/camera setup.

            entities = new Entity[BLOCKED_AVATARS];
            for (var i = 0; i < BLOCKED_AVATARS; i++)
                entities[i] = world.Create(new HiddenPlayerComponent { Reason = HiddenPlayerComponent.HiddenReason.Blocked });
        }

        [Test]
        public void KeepHiddenReasonUnchangedOnSteadyStateFlagChecks()
        {
            RunFlagChecks();

            for (var i = 0; i < entities.Length; i++)
            {
                Assert.IsTrue(world.Has<HiddenPlayerComponent>(entities[i]));
                Assert.AreEqual(HiddenPlayerComponent.HiddenReason.Blocked, world.Get<HiddenPlayerComponent>(entities[i]).Reason);
            }
        }

        private void RunFlagChecks()
        {
            for (var i = 0; i < entities.Length; i++)
            {
                // Steady-state "still blocked" -> reaches the flag test via short-circuit; no-op (bit set).
                system!.SetHiddenComponent(entities[i], true, HiddenPlayerComponent.HiddenReason.Blocked);
                // "not banned" -> reaches the flag test; no-op (Banned bit clear, no HiddenPlayerComponent removal).
                system.SetHiddenComponent(entities[i], false, HiddenPlayerComponent.HiddenReason.Banned);
            }
        }
    }
}
