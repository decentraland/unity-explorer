using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyRailElementShould
    {
        private static readonly int MOUSE = PointerId.mousePointerId;

        private LobbyRailElement rail = null!;
        private GameObject? documentGameObject;
        private PanelSettings? panelSettings;

        [SetUp]
        public void SetUp()
        {
            rail = new LobbyRailElement { CardsPerPage = 3 };
        }

        [TearDown]
        public void TearDown()
        {
            if (documentGameObject != null)
                Object.DestroyImmediate(documentGameObject);

            if (panelSettings != null)
                Object.DestroyImmediate(panelSettings);
        }

        [Test]
        public void KeepTheCardsApartFromItsChrome()
        {
            // Act
            rail.Add(new VisualElement());
            rail.Add(new VisualElement());

            // Assert
            Assert.AreEqual(2, rail.childCount);
            Assert.AreEqual(2, rail.hierarchy.childCount, "Only the track and the dots hang from the rail itself");
        }

        [Test]
        public void ShowOneDotPerPage()
        {
            // Act
            ShowCards(7);

            // Assert
            Assert.AreEqual(3, rail.PageCount);
            Assert.AreEqual(3, VisibleDots());
        }

        [Test]
        public void HideTheDotsWhileEverythingFitsInOnePage()
        {
            // Act
            ShowCards(3);

            // Assert
            Assert.AreEqual(1, rail.PageCount);
            Assert.AreEqual(0, VisibleDots());
        }

        [Test]
        public void HideThePreviousArrowOnTheFirstPage()
        {
            // Act
            ShowCards(7);

            // Assert
            Assert.AreEqual(DisplayStyle.None, Arrow("Previous").style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, Arrow("Next").style.display.value);
        }

        [Test]
        public void HideTheNextArrowOnTheLastPage()
        {
            // Arrange
            ShowCards(7);

            // Act
            rail.SnapTo(2);

            // Assert
            Assert.AreEqual(2, rail.CurrentPage);
            Assert.AreEqual(DisplayStyle.Flex, Arrow("Previous").style.display.value);
            Assert.AreEqual(DisplayStyle.None, Arrow("Next").style.display.value);
        }

        [Test]
        public void PageOnWhenTheWheelScrollsDown()
        {
            // Arrange
            AttachToPanel();
            ShowCards(7);

            // Act
            Wheel(new Vector2(0f, 1f));

            // Assert
            Assert.AreEqual(1, rail.CurrentPage);
        }

        [Test]
        public void PageBackWhenTheWheelScrollsUp()
        {
            // Arrange
            AttachToPanel();
            ShowCards(7);
            rail.SnapTo(1);

            // Act
            Wheel(new Vector2(0f, -1f));

            // Assert
            Assert.AreEqual(0, rail.CurrentPage);
        }

        [Test]
        public void IgnoreAHorizontalWheelScroll()
        {
            // Arrange
            AttachToPanel();
            ShowCards(7);
            rail.SnapTo(1);

            // Act
            Wheel(new Vector2(1f, 0f));

            // Assert
            Assert.AreEqual(1, rail.CurrentPage);
        }

        [Test]
        public void ClampThePageToTheOnesTheCardsFill()
        {
            // Arrange
            ShowCards(7);

            // Act
            rail.SnapTo(10);

            // Assert
            Assert.AreEqual(2, rail.CurrentPage);

            // Act
            rail.SnapTo(-1);

            // Assert
            Assert.AreEqual(0, rail.CurrentPage);
        }

        [Test]
        public void RewindWhenTheCardsChange()
        {
            // Arrange
            ShowCards(7);
            rail.SnapTo(2);

            // Act
            rail.SetCardCount(4);

            // Assert
            Assert.AreEqual(0, rail.CurrentPage);
            Assert.AreEqual(2, VisibleDots());
        }

        [Test]
        public void KeepThePageWhenTheCardsChangeWithoutRewinding()
        {
            // Arrange
            ShowCards(7);
            rail.SnapTo(1);

            // Act
            rail.SetCardCount(5, rewind: false);

            // Assert
            Assert.AreEqual(1, rail.CurrentPage);
            Assert.AreEqual(2, VisibleDots());
        }

        [Test]
        public void FallBackToTheLastPageTheCardsStillFillWithoutRewinding()
        {
            // Arrange
            ShowCards(7);
            rail.SnapTo(2);

            // Act
            rail.SetCardCount(4, rewind: false);

            // Assert
            Assert.AreEqual(1, rail.CurrentPage);
        }

        [Test]
        public void GiveThePointerBackWhenADragStartedOnACardEnds()
        {
            // Arrange
            VisualElement root = AttachToPanel();
            var presses = 0;
            ShowCapturingCards(7, () => presses++);
            VisualElement viewport = rail.Q("Viewport");

            // Act
            Press(rail[0], 100f);
            Move(rail[0], 130f);

            // Assert
            Assert.IsTrue(viewport.HasPointerCapture(MOUSE), "The rail takes the pointer over past the drag threshold");

            // Act
            Release(viewport, 130f);

            // Assert
            Assert.IsFalse(viewport.HasPointerCapture(MOUSE), "The rail gives the pointer back on release");
            Assert.IsFalse(rail.ClassListContains("lobby-rail--instant"), "The drag is over");

            // Act
            Press(rail[1], 300f);
            Release(rail[1], 300f);

            // Assert
            Assert.AreEqual(2, presses, "The cards take presses again once the drag is over");
            Assert.IsNull(root.panel.GetCapturingElement(MOUSE));
        }

        [Test]
        public void HearTheReleaseThroughTheCardThatCapturedThePointer()
        {
            // Arrange
            AttachToPanel();
            var presses = 0;
            ShowCapturingCards(7, () => presses++);
            VisualElement viewport = rail.Q("Viewport");

            // Act
            Press(rail[0], 100f);
            Release(rail[0], 100f);
            Move(viewport, 150f);

            // Assert
            Assert.IsFalse(viewport.HasPointerCapture(MOUSE), "A move after the release does not start a drag");
            Assert.IsFalse(rail.ClassListContains("lobby-rail--instant"));

            // Act
            Press(rail[1], 300f);

            // Assert
            Assert.AreEqual(2, presses, "The cards still take presses");
        }

        [Test]
        public void DropAStalePressOnTheFirstMoveWithoutTheButton()
        {
            // Arrange
            VisualElement root = AttachToPanel();
            var presses = 0;
            ShowCapturingCards(7, () => presses++);
            VisualElement viewport = rail.Q("Viewport");

            // Act
            Press(rail[0], 100f);
            rail[0].ReleasePointer(MOUSE);
            Release(root, 100f);
            Move(viewport, 150f);

            // Assert
            Assert.IsFalse(viewport.HasPointerCapture(MOUSE), "A move without the button held does not start a drag");
            Assert.IsFalse(rail.ClassListContains("lobby-rail--instant"));

            // Act
            Press(rail[1], 300f);

            // Assert
            Assert.AreEqual(2, presses, "The cards still take presses");
        }

        private VisualElement AttachToPanel()
        {
            documentGameObject = new GameObject(nameof(LobbyRailElementShould));
            var document = documentGameObject.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            document.panelSettings = panelSettings;
            document.rootVisualElement.Add(rail);
            return document.rootVisualElement;
        }

        // The press counter is registered before the Clickable so its pointer capture cannot cut it off
        private void ShowCapturingCards(int count, System.Action onPress)
        {
            for (var i = 0; i < count; i++)
            {
                var card = new VisualElement();
                card.RegisterCallback<PointerDownEvent>(_ => onPress());
                card.AddManipulator(new Clickable(() => { }));
                rail.Add(card);
            }

            rail.SetCardCount(count);
        }

        private static void Press(VisualElement target, float x)
        {
            using PointerDownEvent evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(x, 10f) });
            evt.target = target;
            target.SendEvent(evt);
        }

        private static void Move(VisualElement target, float x)
        {
            using PointerMoveEvent evt = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = new Vector2(x, 10f) });
            evt.target = target;
            target.SendEvent(evt);
        }

        private static void Release(VisualElement target, float x)
        {
            using PointerUpEvent evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(x, 10f) });
            evt.target = target;
            target.SendEvent(evt);
        }

        private void Wheel(Vector2 delta)
        {
            using WheelEvent evt = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel, delta = delta, mousePosition = new Vector2(100f, 10f) });
            evt.target = rail;
            rail.SendEvent(evt);
        }

        private void ShowCards(int count)
        {
            for (var i = 0; i < count; i++)
                rail.Add(new VisualElement());

            rail.SetCardCount(count);
        }

        private VisualElement Arrow(string name) =>
            rail.Q<Button>(name);

        private int VisibleDots()
        {
            VisualElement dots = rail.Q("Dots");
            var visible = 0;

            for (var i = 0; i < dots.childCount; i++)
                if (dots[i].style.display.value == DisplayStyle.Flex)
                    visible++;

            return visible;
        }
    }
}
