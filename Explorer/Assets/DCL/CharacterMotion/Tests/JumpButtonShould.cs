using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using ECS.Abstract;
using ECS.Input;
using ECS.Input.Systems.Physics;
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

            var dlcInput = new DCLInput();
            dlcInput.Enable();
            inputDevice = InputSystem.AddDevice<Keyboard>();

            ICharacterControllerSettings controllerSettings = Substitute.For<ICharacterControllerSettings>();
            controllerSettings.HoldJumpTime.Returns(1f);

            playerEntity = world.Create(new PlayerComponent(), controllerSettings, new CharacterRigidTransform { IsGrounded = true });

            updatePhysicsTickSystem = new UpdateInputPhysicsTickSystem(world);
            updateInputJumpSystem = new UpdateInputJumpSystem(world, dlcInput.Player.Jump);
            updateInputJumpSystem.Initialize();

            fixedTick = world.CachePhysicsTick();
        }

        private UpdateInputPhysicsTickSystem updatePhysicsTickSystem;
        private UpdateInputJumpSystem updateInputJumpSystem;

        private World world;
        private Keyboard inputDevice;

        private Entity playerEntity;
        private SingleInstanceEntity fixedTick;

        [Test]
        public async Task PressAndReleaseJump()
        {
            Press(inputDevice.spaceKey);

            updateInputJumpSystem.Update(0);

            //Assert
            Assert.IsTrue(world.Get<JumpInputComponent>(playerEntity).IsChargingJump);

            await UniTask.Yield();

            Release(inputDevice.spaceKey);
            updateInputJumpSystem.Update(0);

            //Assert
            Assert.IsFalse(world.Get<JumpInputComponent>(playerEntity).IsChargingJump);
        }

        [Test]
        public async Task JumpReleaseAfterHoldTime()
        {
            Press(inputDevice.spaceKey);

            updateInputJumpSystem.Update(0);

            //Assert
            Assert.IsTrue(world.Get<JumpInputComponent>(playerEntity).IsChargingJump);

            await UniTask.Yield();

            updateInputJumpSystem.Update(2);

            //Assert
            Assert.IsFalse(world.Get<JumpInputComponent>(playerEntity).IsChargingJump);
        }

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
            Assert.IsTrue(world.Get<JumpInputComponent>(playerEntity).PhysicalButtonArguments.GetPower(fixedTick.GetPhysicsTickComponent(world).Tick) > 0);
        }
    }
}
