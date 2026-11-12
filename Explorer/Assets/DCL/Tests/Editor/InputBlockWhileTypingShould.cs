using Arch.Core;
using DCL.Input;
using DCL.Input.Component;
using ECS.Abstract;
using NUnit.Framework;

namespace DCL.Tests.Editor
{
    public class InputBlockWhileTypingShould
    {
        private World world = null!;
        private SingleInstanceEntity inputMapEntity;
        private ECSInputBlock inputBlock = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            world.Create(new InputMapComponent(AllKinds()));
            inputMapEntity = world.CacheInputMap();
            inputBlock = new ECSInputBlock(world);
        }

        [TearDown]
        public void TearDown() =>
            world.Dispose();

        [TestCase(InputMapComponent.Kind.Shortcuts)]
        [TestCase(InputMapComponent.Kind.InWorldCamera)]
        [TestCase(InputMapComponent.Kind.VoiceChat)]
        public void DeactivateMapWhileTyping(InputMapComponent.Kind kind)
        {
            // Act
            inputBlock.Disable(InputMapComponent.BLOCK_WHILE_TYPING);

            // Assert
            Assert.That(ActiveKinds() & kind, Is.EqualTo(InputMapComponent.Kind.None));
        }

        [Test]
        public void RestoreEveryMapWhenTypingEnds()
        {
            // Arrange
            inputBlock.Disable(InputMapComponent.BLOCK_WHILE_TYPING);

            // Act
            inputBlock.Enable(InputMapComponent.BLOCK_WHILE_TYPING);

            // Assert
            Assert.That(ActiveKinds(), Is.EqualTo(AllKinds()));
        }

        [Test]
        public void KeepPlayerMovementActiveWhileTyping()
        {
            // Act
            inputBlock.Disable(InputMapComponent.BLOCK_WHILE_TYPING);

            // Assert
            Assert.That(ActiveKinds() & InputMapComponent.Kind.Player, Is.EqualTo(InputMapComponent.Kind.Player));
        }

        private InputMapComponent.Kind ActiveKinds() =>
            inputMapEntity.GetInputMapComponent(world).Active;

        private static InputMapComponent.Kind AllKinds()
        {
            InputMapComponent.Kind all = InputMapComponent.Kind.None;

            foreach (InputMapComponent.Kind kind in InputMapComponent.VALUES)
                all |= kind;

            return all;
        }
    }
}
