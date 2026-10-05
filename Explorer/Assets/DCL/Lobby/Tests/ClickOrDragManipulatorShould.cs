using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class ClickOrDragManipulatorShould
    {
        private static readonly int MOUSE = PointerId.mousePointerId;

        private VisualElement root = null!;
        private VisualElement target = null!;
        private ClickOrDragManipulator manipulator = null!;
        private GameObject documentGameObject = null!;
        private PanelSettings panelSettings = null!;

        private int clicks;
        private int dragStarts;
        private int dragEnds;
        private Vector2 lastPosition;
        private Vector2 lastDelta;

        [SetUp]
        public void SetUp()
        {
            clicks = 0;
            dragStarts = 0;
            dragEnds = 0;
            lastPosition = default(Vector2);
            lastDelta = default(Vector2);

            manipulator = new ClickOrDragManipulator
            {
                Clicked = () => clicks++,
                DragStarted = position =>
                {
                    dragStarts++;
                    lastPosition = position;
                },
                Dragged = (position, delta) =>
                {
                    lastPosition = position;
                    lastDelta = delta;
                },
                DragEnded = position =>
                {
                    dragEnds++;
                    lastPosition = position;
                },
            };

            target = new VisualElement();
            target.AddManipulator(manipulator);

            documentGameObject = new GameObject(nameof(ClickOrDragManipulatorShould));
            var document = documentGameObject.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            document.panelSettings = panelSettings;
            root = document.rootVisualElement;
            root.Add(target);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(documentGameObject);
            Object.DestroyImmediate(panelSettings);
        }

        [Test]
        public void ClickOnAReleaseWithinTheThreshold()
        {
            // Act
            Press(100f);
            Release(104f);

            // Assert
            Assert.AreEqual(1, clicks);
            Assert.AreEqual(0, dragStarts);
        }

        [Test]
        public void NotClickAfterADrag()
        {
            // Act
            Press(100f);
            Move(120f);
            Release(120f);

            // Assert
            Assert.AreEqual(0, clicks);
            Assert.AreEqual(1, dragStarts);
            Assert.AreEqual(1, dragEnds);
            Assert.AreEqual(new Vector2(120f, 10f), lastPosition);
        }

        [Test]
        public void StartTheDragOnlyPastTheThreshold()
        {
            // Arrange
            Press(100f);

            // Act
            Move(105f);

            // Assert
            Assert.AreEqual(0, dragStarts, "Within the threshold the press is still a click");

            // Act
            Move(110f);

            // Assert
            Assert.AreEqual(1, dragStarts);
            Assert.AreEqual(new Vector2(110f, 10f), lastPosition);
            Assert.AreEqual(new Vector2(10f, 0f), lastDelta, "The first travel is measured from the press");
        }

        [Test]
        public void ReportTheTravelSinceThePreviousMove()
        {
            // Arrange
            Press(100f);
            Move(120f);

            // Act
            Move(125f);

            // Assert
            Assert.AreEqual(new Vector2(5f, 0f), lastDelta);
        }

        [Test]
        public void HoldThePointerWhilePressedAndLetItGoOnTheRelease()
        {
            // Act
            Press(100f);

            // Assert
            Assert.IsTrue(target.HasPointerCapture(MOUSE));

            // Act
            Release(100f);

            // Assert
            Assert.IsFalse(target.HasPointerCapture(MOUSE));
        }

        [Test]
        public void DropAStalePressWhoseReleaseWentElsewhere()
        {
            // Arrange
            Press(100f);
            target.ReleasePointer(MOUSE);
            Release(root, 100f);

            // Act
            Move(150f);

            // Assert
            Assert.AreEqual(0, dragStarts, "A move after the lost release does not start a drag");
            Assert.AreEqual(0, clicks);

            // Act
            Press(300f);
            Release(300f);

            // Assert
            Assert.AreEqual(1, clicks, "The target still takes presses");
        }

        [Test]
        public void IgnoreTheRightButton()
        {
            // Act
            Press(100f, button: 1);

            // Assert
            Assert.IsFalse(target.HasPointerCapture(MOUSE));

            // Act
            Release(100f, button: 1);

            // Assert
            Assert.AreEqual(0, clicks);
        }

        [Test]
        public void EndTheDragWhenTheCaptureIsLost()
        {
            // Arrange
            Press(100f);
            Move(120f);

            // Act
            target.ReleasePointer(MOUSE);

            // Assert
            Assert.AreEqual(1, dragEnds);
            Assert.AreEqual(0, clicks);
        }

        [Test]
        public void EndADragOnCancelWithoutAClick()
        {
            // Arrange
            Press(100f);
            Move(120f);

            // Act
            manipulator.Cancel();

            // Assert
            Assert.AreEqual(1, dragEnds);
            Assert.AreEqual(0, clicks);
            Assert.IsFalse(target.HasPointerCapture(MOUSE));
        }

        [Test]
        public void ForgetAPlainPressOnCancel()
        {
            // Arrange
            Press(100f);

            // Act
            manipulator.Cancel();
            Release(100f);

            // Assert
            Assert.AreEqual(0, clicks);
            Assert.AreEqual(0, dragEnds);
        }

        private void Press(float x, int button = 0) =>
            Press(target, x, button);

        private void Release(float x, int button = 0) =>
            Release(target, x, button);

        private void Move(float x) =>
            Move(target, x);

        private static void Press(VisualElement element, float x, int button = 0)
        {
            using PointerDownEvent evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = button, mousePosition = new Vector2(x, 10f) });
            evt.target = element;
            element.SendEvent(evt);
        }

        private static void Move(VisualElement element, float x)
        {
            using PointerMoveEvent evt = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = new Vector2(x, 10f) });
            evt.target = element;
            element.SendEvent(evt);
        }

        private static void Release(VisualElement element, float x, int button = 0)
        {
            using PointerUpEvent evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = button, mousePosition = new Vector2(x, 10f) });
            evt.target = element;
            element.SendEvent(evt);
        }
    }
}
