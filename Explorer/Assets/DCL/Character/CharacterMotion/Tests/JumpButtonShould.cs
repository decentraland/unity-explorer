using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using DCL.SDKComponents.InputModifier.Components;
using DCL.Time.Systems;
using ECS.Abstract;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using UnityEngine.InputSystem;

namespace DCL.CharacterMotion.Tests
{
    [TestFixture]
    public class JumpInputComponentShould : InputTestFixture
    {
        [SetUp]
        public void SetUp()
        {
            base.Setup();
            world = World.Create();

            DCLInput.Instance.Enable();
            inputDevice = InputSystem.AddDevice<Keyboard>();

            ICharacterControllerSettings controllerSettings = Substitute.For<ICharacterControllerSettings>();
            controllerSettings.LongJumpTime.Returns(1f);

            playerEntity = world.Create(
                new PlayerComponent(),
                controllerSettings,
                new CharacterRigidTransform { IsGrounded = true },
                new InputModifierComponent(),
                new JumpState());

            updatePhysicsTickSystem = new UpdatePhysicsTickSystem(world);
            updateInputJumpSystem = new UpdateInputJumpSystem(world, DCLInput.Instance.Player.Jump);
            updateInputJumpSystem.Initialize();

            fixedTick = world.CachePhysicsTick();
        }

        [OneTimeSetUp]
        public void OneTimeSetUp() =>
            EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        private UpdatePhysicsTickSystem updatePhysicsTickSystem;
        private UpdateInputJumpSystem updateInputJumpSystem;

        private World world;
        private Keyboard inputDevice;

        private Entity playerEntity;
        private SingleInstanceEntity fixedTick;

        [Test]
        public async Task JumpOccursOnCorrectPhysicalFrame()
        {
            //Lets simulate a Fixed Update tick
            updatePhysicsTickSystem.Update(0);

            //Lets simulate three frames, in which we press and release the jump button on each frame
            Press(inputDevice.spaceKey);
            updateInputJumpSystem.Update(0);

            await UniTask.Yield();

            Release(inputDevice.spaceKey);

            updateInputJumpSystem.Update(1);

            // next physics tick
            updatePhysicsTickSystem.Update(1);

            await UniTask.Yield();

            //This simulated another fixed update. On this call, the jump should occur
            Assert.IsTrue(world.Get<JumpInputComponent>(playerEntity).Trigger.IsAvailable(fixedTick.GetPhysicsTickComponent(world).Tick, 0));
        }

        // Double jump availability is driven solely by MaxAirJumpCount, which must depend only on DisableDoubleJump.

        [Test]
        public void DisableJumpDoesNotDisableDoubleJump()
        {
            // Regression for #8622: DisableJump used to zero MaxAirJumpCount, wrongly killing the double jump.
            SetAirJumpCount(1);
            SetModifier(disableJump: true);
            SetJumpCount(1); // airborne, so the normal-jump gate does not apply

            updatePhysicsTickSystem.Update(0);
            updateInputJumpSystem.Update(0);

            Assert.AreEqual(1, world.Get<JumpState>(playerEntity).MaxAirJumpCount);
        }

        [Test]
        public void DisableDoubleJumpZeroesMaxAirJumpCount()
        {
            SetAirJumpCount(1);
            SetModifier(disableDoubleJump: true);

            updatePhysicsTickSystem.Update(0);
            updateInputJumpSystem.Update(0);

            Assert.AreEqual(0, world.Get<JumpState>(playerEntity).MaxAirJumpCount);
        }

        [Test]
        public void NoModifiersKeepConfiguredMaxAirJumpCount()
        {
            SetAirJumpCount(1);

            updatePhysicsTickSystem.Update(0);
            updateInputJumpSystem.Update(0);

            Assert.AreEqual(1, world.Get<JumpState>(playerEntity).MaxAirJumpCount);
        }

        // The normal (ground) jump is blocked by withholding the input passthrough when DisableJump is set,
        // independently of DisableDoubleJump / DisableGliding.

        [Test]
        public async Task NormalJumpBlockedWhenJumpIsDisabled()
        {
            SetAirJumpCount(1);
            SetModifier(disableJump: true); // JumpCount stays 0 -> normal jump

            Assert.IsFalse(await JumpInputPassesThroughAsync());
        }

        [Test]
        public async Task NormalJumpBlockedWhenJumpAndDoubleJumpAreDisabled()
        {
            // Regression for #8622: with both flags set the player could still perform the normal jump.
            SetAirJumpCount(1);
            SetModifier(disableJump: true, disableDoubleJump: true);

            Assert.IsFalse(await JumpInputPassesThroughAsync());
        }

        private void SetAirJumpCount(int count) =>
            world.Get<ICharacterControllerSettings>(playerEntity).AirJumpCount.Returns(count);

        private void SetModifier(bool disableJump = false, bool disableDoubleJump = false, bool disableGliding = false)
        {
            var modifier = new InputModifierComponent
            {
                DisableJump = disableJump,
                DisableDoubleJump = disableDoubleJump,
                DisableGliding = disableGliding,
            };

            world.Set(playerEntity, modifier);
        }

        private void SetJumpCount(int count)
        {
            ref JumpState jumpState = ref world.Get<JumpState>(playerEntity);
            jumpState.JumpCount = count;
        }

        // Presses and releases the jump button following the proven timing of JumpOccursOnCorrectPhysicalFrame,
        // and returns whether the press reached the JumpInputComponent (i.e. the input was not gated out).
        private async Task<bool> JumpInputPassesThroughAsync()
        {
            updatePhysicsTickSystem.Update(0);

            Press(inputDevice.spaceKey);
            updateInputJumpSystem.Update(0);

            await UniTask.Yield();

            Release(inputDevice.spaceKey);
            updateInputJumpSystem.Update(1);

            updatePhysicsTickSystem.Update(1);

            await UniTask.Yield();

            return world.Get<JumpInputComponent>(playerEntity).Trigger.IsAvailable(fixedTick.GetPhysicsTickComponent(world).Tick, 0);
        }
    }
}
