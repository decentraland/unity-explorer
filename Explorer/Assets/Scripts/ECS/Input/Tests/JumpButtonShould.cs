using Arch.Core;
using Cysharp.Threading.Tasks;
using ECS.CharacterMotion.Components;
using ECS.Input.Component;
using ECS.Input.Systems;
using ECS.Input.Systems.Physics;
using NUnit.Framework;
using UnityEngine.InputSystem;

[TestFixture]
public class JumpInputComponentShould : InputTestFixture
{

    /*private UpdateInputPhysicsTickSystem updatePhysicsTickSystem;
    private UpdateInputPhysicsButtonSystem<JumpInputComponent> updateInputPhysicsButtonSystem;
    private UpdateInputJumpSystem updateInputJumpSystem;

    private World world;
    private Keyboard inputDevice;


    [SetUp]
    public void SetUp()
    {
        base.Setup();
        world = World.Create();


        DCLInput dlcInput = new DCLInput();
        dlcInput.Enable();
        inputDevice = InputSystem.AddDevice<Keyboard>();

        updatePhysicsTickSystem = new UpdateInputPhysicsTickSystem(world);
        updateInputPhysicsButtonSystem =  new UpdateInputPhysicsButtonSystem<JumpInputComponent>(world, dlcInput.Player.Jump);
        updateInputJumpSystem = new UpdateInputJumpSystem(world);
    }

    [Test]
    public void PressAndReleaseJump()
    {
        Press(inputDevice.spaceKey);

        updatePhysicsTickSystem.Update(0);
        updateInputPhysicsButtonSystem.Update(0);
        updateInputJumpSystem.Update(0);

        //Assert
        world.Query(new QueryDescription().WithAll<JumpInputComponent>(),
            (ref JumpInputComponent jumpInput) => Assert.AreEqual(1, jumpInput.Power));

        Release(inputDevice.spaceKey);

        updatePhysicsTickSystem.Update(0);
        updateInputPhysicsButtonSystem.Update(0);
        updateInputJumpSystem.Update(0);

        //Assert
        world.Query(new QueryDescription().WithAll<JumpInputComponent>(),
            (ref JumpInputComponent jumpInput) => Assert.AreEqual(0, jumpInput.Power));
    }

    [Test]
    public void PressAndHoldJump()
    {
        Press(inputDevice.spaceKey);

        updatePhysicsTickSystem.Update(0);
        updateInputPhysicsButtonSystem.Update(0);
        updateInputJumpSystem.Update(0);

        //Assert
        world.Query(new QueryDescription().WithAll<JumpInputComponent>(),
            (ref JumpInputComponent jumpInput) => Assert.AreEqual(1, jumpInput.Power));


        updatePhysicsTickSystem.Update(0);
        updateInputPhysicsButtonSystem.Update(0);
        updateInputJumpSystem.Update(UpdateInputJumpSystem.HOLD_TIME * 1000 / 2);

        //Assert
        world.Query(new QueryDescription().WithAll<JumpInputComponent>(),
            (ref JumpInputComponent jumpInput) => Assert.AreEqual(1, jumpInput.Power));

        updatePhysicsTickSystem.Update(0);
        updateInputPhysicsButtonSystem.Update(0);
        updateInputJumpSystem.Update(UpdateInputJumpSystem.HOLD_TIME * 1000);

        //Assert
        world.Query(new QueryDescription().WithAll<JumpInputComponent>(),
            (ref JumpInputComponent jumpInput) => Assert.AreEqual(0, jumpInput.Power));
    }


    [Test]
    public void RespondOnHigherFrameRate()
    {
        //We do a fixed Update
        updateInputJumpSystem.Update(0);
        updatePhysicsTickSystem.Update(0);

        //We do 2 frame updates.
        Press(inputDevice.spaceKey);
        updateInputPhysicsButtonSystem.Update(0);
        Release(inputDevice.spaceKey);
        updateInputPhysicsButtonSystem.Update(0);


        //Back On the Fixed, the value jump should be updated even if the key has already been released in the previous frame.
        updateInputJumpSystem.Update(0);
        updatePhysicsTickSystem.Update(0);
        world.Query(new QueryDescription().WithAll<JumpInputComponent>(),
            (ref JumpInputComponent jumpInput) =>
            {
                Assert.IsTrue(jumpInput.IsKeyDown(updateInputJumpSystem.tickValue));
            });
    }

    [Test]
    public void RespondOnLowerFrameRate()
    {
        //We do a fixed Update
        updateInputJumpSystem.Update(0);
        updatePhysicsTickSystem.Update(0);

        //We do 1 frame updates.
        Press(inputDevice.spaceKey);
        updateInputPhysicsButtonSystem.Update(0);

        //We do 2 consecuent FixedUpdates. Even if the key was never released, the value should be 0
        updateInputJumpSystem.Update(0);
        updatePhysicsTickSystem.Update(0);
        world.Query(new QueryDescription().WithAll<JumpInputComponent>(),
            (ref JumpInputComponent jumpInput) => Assert.IsTrue(jumpInput.IsKeyDown(updateInputJumpSystem.tickValue)));

        updateInputJumpSystem.Update(0);
        updatePhysicsTickSystem.Update(0);
        world.Query(new QueryDescription().WithAll<JumpInputComponent>(),
            (ref JumpInputComponent jumpInput) => Assert.IsFalse(jumpInput.IsKeyDown(updateInputJumpSystem.tickValue)));
    }*/
}
