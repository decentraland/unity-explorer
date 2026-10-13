using DCL.Prefs;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyWidgetHostShould
    {
        private static readonly FieldInfo PREFS_FIELD = typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!;

        private VisualElement root = null!;
        private LobbyWidgetHost host = null!;

        [SetUp]
        public void SetUp()
        {
            PREFS_FIELD.SetValue(null, new InMemoryDCLPlayerPrefs());
            root = BuildHierarchy();
            host = new LobbyWidgetHost();
        }

        [TearDown]
        public void TearDown()
        {
            PREFS_FIELD.SetValue(null, null);
        }

        [Test]
        public void ReadTheGridSizesFromTheHierarchy()
        {
            //Act
            host.Show(root);

            //Assert
            Assert.AreEqual(new Vector2Int(4, 6), host.Layout!.GridSize(LobbyGridCorner.TopLeft));
            Assert.AreEqual(new Vector2Int(6, 2), host.Layout.GridSize(LobbyGridCorner.BottomRight));
        }

        [Test]
        public void MoveTheSectionsOntoTheirGridsByDefault()
        {
            //Act
            host.Show(root);

            //Assert
            VisualElement welcome = root.Q("Welcome");
            VisualElement friends = root.Q("Friends");
            VisualElement liveEvents = root.Q("LiveEvents");
            Assert.AreSame(root.Q("TopLeftGrid"), welcome.parent);
            Assert.AreSame(root.Q("TopLeftGrid"), friends.parent);
            Assert.AreSame(root.Q("TopRightGrid"), liveEvents.parent);
            Assert.AreSame(root.Q("TopRightGrid"), root.Q("UpcomingEvents").parent);
            Assert.AreSame(root.Q("BottomLeftGrid"), root.Q("RecentPlaces").parent);
            Assert.AreSame(root.Q("BottomRightGrid"), root.Q("FeaturedPlaces").parent);
            Assert.AreSame(root.Q("UnplacedWidgets"), root.Q("RecentPlaces4x2").parent, "The four-column places have no room by default");
            Assert.AreSame(root.Q("UnplacedWidgets"), root.Q("FeaturedPlaces4x2").parent);
            Assert.AreEqual(2, root.Q("UnplacedWidgets").childCount);

            Assert.AreEqual(0f, welcome.style.left.value.value);
            Assert.AreEqual(0f, welcome.style.top.value.value);
            Assert.AreEqual(LobbyGridElement.SpanToSize(LobbyWidgetId.Landing4x3.Span()).x, welcome.style.width.value.value);
            Assert.AreEqual(LobbyGridElement.SpanToSize(LobbyWidgetId.Landing4x3.Span()).y, welcome.style.height.value.value);
            Assert.AreEqual(3 * LobbyGridElement.CellPitch.y, friends.style.top.value.value);
            Assert.AreEqual(LobbyGridElement.CellPitch.x, liveEvents.style.left.value.value);
            Assert.AreEqual(3 * LobbyGridElement.CellPitch.y, root.Q("UpcomingEvents").style.top.value.value);
        }

        [Test]
        public void ParkAnUnplacedSectionInTheHiddenContainer()
        {
            //Arrange
            var stored = new LobbyLayout(new Vector2Int(4, 6), new Vector2Int(4, 6), new Vector2Int(6, 2), new Vector2Int(6, 2));
            stored.ResetToDefault();
            stored.Remove(LobbyWidgetId.LiveEvents3x3);
            LobbyLayoutStorage.Save(stored);

            //Act
            host.Show(root);

            //Assert
            VisualElement liveEvents = root.Q("LiveEvents");
            Assert.AreSame(root.Q("UnplacedWidgets"), liveEvents.parent);
            Assert.AreEqual(9, root.Q("TopLeftGrid").childCount + root.Q("TopRightGrid").childCount + root.Q("BottomLeftGrid").childCount + root.Q("BottomRightGrid").childCount, "Five sections and the outline of each of the four grids");
        }

        [Test]
        public void FollowTheLayoutWhenItChanges()
        {
            //Arrange
            host.Show(root);
            VisualElement welcome = root.Q("Welcome");

            //Act
            host.Layout!.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopRight, new Vector2Int(0, 1));
            host.Layout.Remove(LobbyWidgetId.FriendsOnline4x2);
            host.Apply();

            //Assert
            Assert.AreSame(root.Q("TopRightGrid"), welcome.parent);
            Assert.AreEqual(LobbyGridElement.CellPitch.y, welcome.style.top.value.value);
            Assert.AreSame(root.Q("UnplacedWidgets"), root.Q("Friends").parent);
        }

        [Test]
        public void ShowASectionAtAnotherCellUntilApplied()
        {
            //Arrange
            host.Show(root);
            VisualElement welcome = root.Q("Welcome");

            //Act
            host.ShowAt(LobbyWidgetId.Landing4x3, new Vector2Int(0, 2));

            //Assert
            Assert.AreSame(root.Q("TopLeftGrid"), welcome.parent);
            Assert.AreEqual(2 * LobbyGridElement.CellPitch.y, welcome.style.top.value.value);
            Assert.IsTrue(host.Layout!.TryGetPlacement(LobbyWidgetId.Landing4x3, out LobbyWidgetPlacement placement));
            Assert.AreEqual(Vector2Int.zero, placement.Cell, "Showing a section elsewhere leaves the layout alone");

            //Act
            host.Apply();

            //Assert
            Assert.AreEqual(0f, welcome.style.top.value.value);
        }

        [Test]
        public void KeepTheSectionsReachableFromTheRoot()
        {
            //Act
            host.Show(root);

            //Assert
            Assert.IsNotNull(root.Q("WelcomeText"));
            Assert.IsNotNull(root.Q("RecentPlaces"));
        }

        [Test]
        public void RefuseToLayOutBeforeItIsShown()
        {
            Assert.Throws<System.InvalidOperationException>(() => host.Apply());
            Assert.Throws<System.InvalidOperationException>(() => host.ShowAt(LobbyWidgetId.Landing4x3, Vector2Int.zero));
        }

        private static VisualElement BuildHierarchy()
        {
            var lobby = new VisualElement { name = "Lobby" };
            lobby.Add(new LobbyGridElement { name = "TopLeftGrid", Corner = LobbyGridCorner.TopLeft, Columns = 4, Rows = 6 });
            lobby.Add(new LobbyGridElement { name = "TopRightGrid", Corner = LobbyGridCorner.TopRight, Columns = 4, Rows = 6 });
            lobby.Add(new LobbyGridElement { name = "BottomLeftGrid", Corner = LobbyGridCorner.BottomLeft, Columns = 6, Rows = 2 });
            lobby.Add(new LobbyGridElement { name = "BottomRightGrid", Corner = LobbyGridCorner.BottomRight, Columns = 6, Rows = 2 });

            var unplaced = new VisualElement { name = "UnplacedWidgets" };
            lobby.Add(unplaced);

            var welcome = new VisualElement { name = "Welcome" };
            welcome.Add(new Label { name = "WelcomeText" });
            unplaced.Add(welcome);
            unplaced.Add(new VisualElement { name = "LiveEvents" });
            unplaced.Add(new VisualElement { name = "UpcomingEvents" });
            unplaced.Add(new VisualElement { name = "Friends" });
            unplaced.Add(new VisualElement { name = "RecentPlaces" });
            unplaced.Add(new VisualElement { name = "FeaturedPlaces" });
            unplaced.Add(new VisualElement { name = "RecentPlaces4x2" });
            unplaced.Add(new VisualElement { name = "FeaturedPlaces4x2" });

            return lobby;
        }
    }
}
