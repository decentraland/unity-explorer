using Arch.Core;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace DCL.CharacterPreview.Tests
{
    [TestFixture]
    public class CharacterPreviewControllerBaseShould
    {
        private static readonly Vector2 POSITION = new (100f, 100f);
        private static readonly Vector2 TRAVEL = new (10f, 0f);

        private readonly List<Object> created = new ();

        private TestPreview preview = null!;
        private World world = null!;
        private CharacterPreviewEventBus eventBus = null!;
        private List<CharacterPreviewPointerInput> drags = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            eventBus = new CharacterPreviewEventBus();
            preview = new TestPreview(CharacterPreviewTestViews.Create(created), Substitute.For<ICharacterPreviewFactory>(), world, eventBus);
            drags = new List<CharacterPreviewPointerInput>();
            preview.InputEventBus.OnDraggingEvent += drags.Add;
        }

        [TearDown]
        public void TearDown()
        {
            preview.Dispose();
            World.Destroy(world);

            foreach (Object obj in created)
                Object.DestroyImmediate(obj);

            created.Clear();
        }

        [Test]
        public void ForwardALeftDragWhileRotationIsEnabled()
        {
            // Arrange
            var input = new CharacterPreviewPointerInput(PointerEventData.InputButton.Left, POSITION, TRAVEL);

            // Act
            preview.Drag(input);

            // Assert
            Assert.AreEqual(1, drags.Count);
            Assert.AreEqual(PointerEventData.InputButton.Left, drags[0].Button);
            Assert.AreEqual(POSITION, drags[0].Position);
            Assert.AreEqual(TRAVEL, drags[0].Delta);
        }

        [Test]
        public void DropALeftDragWhileRotationIsDisabled()
        {
            // Arrange
            preview.RotateEnabled = false;

            // Act
            preview.Drag(new CharacterPreviewPointerInput(PointerEventData.InputButton.Left, POSITION, TRAVEL));

            // Assert
            CollectionAssert.IsEmpty(drags);
        }

        [Test]
        public void DropALeftDragTheViewDoesNotAllow()
        {
            // Arrange
            preview.View.EnableRotating = false;

            // Act
            preview.Drag(new CharacterPreviewPointerInput(PointerEventData.InputButton.Left, POSITION, TRAVEL));

            // Assert
            CollectionAssert.IsEmpty(drags);
        }

        [Test]
        public void ForwardARightDragWhilePanningIsEnabled()
        {
            // Act
            preview.Drag(new CharacterPreviewPointerInput(PointerEventData.InputButton.Right, POSITION, TRAVEL));

            // Assert
            Assert.AreEqual(1, drags.Count);
            Assert.AreEqual(PointerEventData.InputButton.Right, drags[0].Button);
        }

        [Test]
        public void DropARightDragWhilePanningIsDisabled()
        {
            // Arrange
            preview.PanEnabled = false;

            // Act
            preview.Drag(new CharacterPreviewPointerInput(PointerEventData.InputButton.Right, POSITION, TRAVEL));

            // Assert
            CollectionAssert.IsEmpty(drags);
        }

        [Test]
        public void DropAMiddleButtonDrag()
        {
            // Act
            preview.Drag(new CharacterPreviewPointerInput(PointerEventData.InputButton.Middle, POSITION, TRAVEL));

            // Assert
            CollectionAssert.IsEmpty(drags);
        }

        [Test]
        public void ReportTheContainerOffWhileAnotherPreviewIsOnTopAndOnOnceItHides()
        {
            // Arrange
            var other = new TestPreview(CharacterPreviewTestViews.Create(created), Substitute.For<ICharacterPreviewFactory>(), world, eventBus);
            eventBus.OnAnyCharacterPreviewShow(preview);

            // Act
            eventBus.OnAnyCharacterPreviewShow(other);
            eventBus.OnAnyCharacterPreviewHide(other);

            // Assert
            CollectionAssert.AreEqual(new[] { false, true }, preview.ContainerActiveChanges);
            other.Dispose();
        }

        [Test]
        public void IgnoreItsOwnShowForTheContainerState()
        {
            // Act
            eventBus.OnAnyCharacterPreviewShow(preview);

            // Assert
            CollectionAssert.IsEmpty(preview.ContainerActiveChanges);
        }

        private class TestPreview : CharacterPreviewControllerBase
        {
            public TestPreview(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, World world, CharacterPreviewEventBus characterPreviewEventBus)
                : base(view, previewFactory, world, false, characterPreviewEventBus) { }

            public CharacterPreviewInputEventBus InputEventBus => inputEventBus;

            public CharacterPreviewView View => view;

            public List<bool> ContainerActiveChanges { get; } = new ();

            public bool RotateEnabled
            {
                set => rotateEnabled = value;
            }

            public bool PanEnabled
            {
                set => panEnabled = value;
            }

            protected override void OnContainerActiveChanged(bool isActive) =>
                ContainerActiveChanges.Add(isActive);
        }
    }
}
