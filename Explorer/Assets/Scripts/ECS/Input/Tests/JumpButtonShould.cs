using Arch.Core;
using CrdtEcsBridge.Components.Special;
using Cysharp.Threading.Tasks;
using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
using ECS.Input.Component;
using ECS.Input.Systems;
using ECS.Input.Systems.Physics;
using NSubstitute;
using NUnit.Framework;
using UnityEngine.InputSystem;

[TestFixture]
public class JumpInputComponentShould : InputTestFixture
{

    private UpdateInputPhysicsTickSystem updatePhysicsTickSystem;
    private UpdateInputJumpSystem updateInputJumpSystem;

    private World world;
    private Keyboard inputDevice;

    private Entity playerEntity;


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
    }

    [Test]
    public void PressAndReleaseJump()
    {
        Press(inputDevice.spaceKey);

        updatePhysicsTickSystem.Update(0);
        updateInputJumpSystem.Update(0);

        //Assert
        Assert.IsTrue(world.Get<JumpInputComponent>(playerEntity).IsChargingJump);

        Release(inputDevice.spaceKey);
        updatePhysicsTickSystem.Update(0);
        updateInputJumpSystem.Update(0);

        //Assert
        Assert.IsFalse(world.Get<JumpInputComponent>(playerEntity).IsChargingJump);
    }


}
