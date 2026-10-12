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
            VisualElement events = root.Q("Events");
            Assert.AreSame(root.Q("TopLeftGrid"), welcome.parent);
            Assert.AreSame(root.Q("TopLeftGrid"), friends.parent);
            Assert.AreSame(root.Q("TopRightGrid"), events.parent);
            Assert.AreSame(root.Q("BottomLeftGrid"), root.Q("RecentPlaces").parent);
            Assert.AreSame(root.Q("BottomRightGrid"), root.Q("FeaturedPlaces").parent);
            Assert.AreEqual(0, root.Q("UnplacedWidgets").childCount);

            Assert.AreEqual(0f, welcome.style.left.value.value);
            Assert.AreEqual(0f, welcome.style.top.value.value);
            Assert.AreEqual(LobbyGridElement.SpanToSize(LobbyWidgetId.Landing4x3.Span()).x, welcome.style.width.value.value);
            Assert.AreEqual(3 * LobbyGridElement.CellPitch.y, friends.style.top.value.value);
            Assert.AreEqual(LobbyGridElement.CellPitch.x, events.style.left.value.value);
        }

        [Test]
        public void ParkAnUnplacedSectionInTheHiddenContainer()
        {
            //Arrange
            var stored = new LobbyLayout(new Vector2Int(4, 6), new Vector2Int(4, 6), new Vector2Int(6, 2), new Vector2Int(6, 2));
            stored.ResetToDefault();
            stored.Remove(LobbyWidgetId.Events3x4);
            LobbyLayoutStorage.Save(stored);

            //Act
            host.Show(root);

            //Assert
            VisualElement events = root.Q("Events");
            Assert.AreSame(root.Q("UnplacedWidgets"), events.parent);
            Assert.AreEqual(4, root.Q("TopLeftGrid").childCount + root.Q("BottomLeftGrid").childCount + root.Q("BottomRightGrid").childCount);
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
            unplaced.Add(new VisualElement { name = "Events" });
            unplaced.Add(new VisualElement { name = "Friends" });
            unplaced.Add(new VisualElement { name = "RecentPlaces" });
            unplaced.Add(new VisualElement { name = "FeaturedPlaces" });

            return lobby;
        }
    }
}
