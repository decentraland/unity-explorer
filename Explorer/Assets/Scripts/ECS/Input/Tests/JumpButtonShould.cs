using Arch.Core;
using CrdtEcsBridge.Components.Special;
using Cysharp.Threading.Tasks;
using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
using ECS.Input.Component;
using ECS.Input.Component.Physics;
using ECS.Input.Systems;
using ECS.Input.Systems.Physics;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using UnityEngine.InputSystem;

[TestFixture]
public class JumpInputComponentShould : InputTestFixture
{

    private UpdateInputPhysicsTickSystem updatePhysicsTickSystem;
    private UpdateInputJumpSystem updateInputJumpSystem;

    private World world;
    private Keyboard inputDevice;

    private Entity playerEntity;
    private Entity physicsTickEntity;


    [SetUp]
    public void SetUp()
    {
        base.Setup();
        world = World.Create();


        DCLInput dlcInput = new DCLInput();
        dlcInput.Enable();
        inputDevice = InputSystem.AddDevice<Keyboard>();

        ICharacterControllerSettings controllerSettings = Substitute.For<ICharacterControllerSettings>();
        controllerSettings.HoldJumpTime.Returns(1f);

        playerEntity = world.Create(new PlayerComponent(), controllerSettings,
            new CharacterPhysics()
        {
            IsGrounded = true
        });

        updatePhysicsTickSystem = new UpdateInputPhysicsTickSystem(world);
        updateInputJumpSystem = new UpdateInputJumpSystem(world, dlcInput.Player.Jump);

        world.Query(new QueryDescription().WithAll<PhysicsTickComponent>(), (in Entity entity) => { physicsTickEntity = entity; });
    }

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

        await UniTask.Yield();

        //This simulated another fixed update. On this call, the jump should occur
        int physicsTick = world.Get<PhysicsTickComponent>(physicsTickEntity).tick;
        Assert.IsTrue(world.Get<JumpInputComponent>(playerEntity).PhysicalButtonArguments.GetPower(physicsTick) > 0);
    }


}
