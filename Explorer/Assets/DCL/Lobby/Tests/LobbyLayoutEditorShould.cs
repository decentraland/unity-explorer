using DCL.Prefs;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    /// <summary>
    ///     The hierarchy gets no layout pass here, so every grid has an empty extent and no drag lands on one: the drop
    ///     geometry is covered by <see cref="LobbyGridElementShould" /> and <see cref="LobbyLayoutShould" />.
    /// </summary>
    [TestFixture]
    public class LobbyLayoutEditorShould
    {
        private const string EDITING_CLASS = "lobby--editing";
        private const string DRAGGING_CLASS = "lobby-widget-handle--dragging";
        private const string LIFTED_CLASS = "lobby__widget--lifted";

        private static readonly FieldInfo PREFS_FIELD = typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!;

        private GameObject documentGameObject = null!;
        private PanelSettings panelSettings = null!;
        private VisualElement root = null!;
        private LobbyWidgetHost host = null!;
        private LobbyLayoutEditor editor = null!;

        [SetUp]
        public void SetUp()
        {
            PREFS_FIELD.SetValue(null, new InMemoryDCLPlayerPrefs());

            // The handles capture the pointer, which takes a panel
            documentGameObject = new GameObject(nameof(LobbyLayoutEditorShould));
            var document = documentGameObject.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            document.panelSettings = panelSettings;
            root = BuildHierarchy();
            document.rootVisualElement.Add(root);

            host = new LobbyWidgetHost();
            host.Show(root);
            editor = new LobbyLayoutEditor(host);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(documentGameObject);
            Object.DestroyImmediate(panelSettings);
            PREFS_FIELD.SetValue(null, null);
        }

        [Test]
        public void CoverEveryPlacedWidgetWithAHandleOnShow()
        {
            //Act
            editor.Show(root);

            //Assert
            Assert.IsTrue(editor.IsEditing);
            Assert.IsTrue(root.ClassListContains(EDITING_CLASS));
            Assert.AreEqual(6, root.Query<LobbyWidgetHandleElement>().ToList().Count);

            LobbyWidgetHandleElement friends = Handle(LobbyWidgetId.FriendsOnline4x2);
            Vector2 size = LobbyGridElement.SpanToSize(LobbyWidgetId.FriendsOnline4x2.Span());
            Assert.AreSame(root.Q("TopLeftGrid"), friends.parent);
            Assert.AreEqual(0f, friends.style.left.value.value);
            Assert.AreEqual(3 * LobbyGridElement.CellPitch.y, friends.style.top.value.value);
            Assert.AreEqual(size.x, friends.style.width.value.value);
            Assert.AreEqual(size.y, friends.style.height.value.value);
            Assert.AreEqual("Friends", friends.Q<Label>().text);
        }

        [Test]
        public void PutTheHandlesOverTheSections()
        {
            //Act
            editor.Show(root);

            //Assert
            VisualElement grid = root.Q("TopLeftGrid");
            Assert.Less(grid.IndexOf(root.Q("Welcome")), grid.IndexOf(Handle(LobbyWidgetId.Landing4x3)));
            Assert.Less(grid.IndexOf(root.Q("Friends")), grid.IndexOf(Handle(LobbyWidgetId.FriendsOnline4x2)));
        }

        [Test]
        public void ShowTheAnchorDotsOfEveryGridOnlyWhileEditing()
        {
            //Act
            editor.Show(root);

            //Assert
            Assert.IsTrue(root.Q<LobbyGridElement>("TopLeftGrid").IsEditing);
            Assert.IsTrue(root.Q<LobbyGridElement>("TopRightGrid").IsEditing);
            Assert.IsTrue(root.Q<LobbyGridElement>("BottomLeftGrid").IsEditing);
            Assert.IsTrue(root.Q<LobbyGridElement>("BottomRightGrid").IsEditing);

            //Act
            editor.Hide();

            //Assert
            Assert.IsFalse(root.Q<LobbyGridElement>("TopLeftGrid").IsEditing);
            Assert.IsFalse(root.Q<LobbyGridElement>("TopRightGrid").IsEditing);
            Assert.IsFalse(root.Q<LobbyGridElement>("BottomLeftGrid").IsEditing);
            Assert.IsFalse(root.Q<LobbyGridElement>("BottomRightGrid").IsEditing);
        }

        [Test]
        public void GiveAnUnplacedWidgetNoHandle()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.JumpBackIn6x2);
            host.Apply();

            //Act
            editor.Show(root);

            //Assert
            Assert.IsNull(root.Q<LobbyWidgetHandleElement>(LobbyWidgetId.JumpBackIn6x2.ToString()));
            Assert.AreEqual(5, root.Query<LobbyWidgetHandleElement>().ToList().Count);
        }

        [Test]
        public void LeaveNothingBehindOnHide()
        {
            //Arrange
            editor.Show(root);

            //Act
            editor.Hide();

            //Assert
            Assert.IsFalse(editor.IsEditing);
            Assert.IsFalse(root.ClassListContains(EDITING_CLASS));
            Assert.AreEqual(0, root.Query<LobbyWidgetHandleElement>().ToList().Count);
            Assert.AreEqual(0, root.Q("LayoutDragLayer").childCount);
            Assert.IsNull(root.Q("DropMarker"));
        }

        [Test]
        public void LiftTheWidgetOntoTheDragLayerWhileDragging()
        {
            //Arrange
            editor.Show(root);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.Landing4x3);

            //Act
            Press(handle, 100f);
            Move(handle, 130f);

            //Assert
            VisualElement welcome = root.Q("Welcome");
            Assert.AreSame(root.Q("LayoutDragLayer"), welcome.parent);
            Assert.IsTrue(welcome.ClassListContains(LIFTED_CLASS));
            Assert.IsTrue(handle.ClassListContains(DRAGGING_CLASS));
            Assert.AreSame(root.Q("TopLeftGrid"), handle.parent, "The handle marks the origin until the drop");
        }

        [Test]
        public void KeepTheWidgetWhereItWasOnADropOutsideEveryGrid()
        {
            //Arrange
            editor.Show(root);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.Landing4x3);

            //Act
            Press(handle, 100f);
            Move(handle, 130f);
            Release(handle, 130f);

            //Assert
            Assert.IsTrue(host.Layout!.TryGetPlacement(LobbyWidgetId.Landing4x3, out LobbyWidgetPlacement placement));
            Assert.AreEqual(LobbyGridCorner.TopLeft, placement.Corner);
            Assert.AreEqual(Vector2Int.zero, placement.Cell);
            Assert.IsFalse(DCLPlayerPrefs.HasKey(DCLPrefKeys.LOBBY_LAYOUT), "A drop that does not take stores nothing");
            VisualElement welcome = root.Q("Welcome");
            Assert.AreSame(root.Q("TopLeftGrid"), welcome.parent, "The widget lands back on its grid");
            Assert.IsFalse(welcome.ClassListContains(LIFTED_CLASS));
            Assert.IsNull(root.Q("DropMarker"));
            Assert.IsFalse(handle.ClassListContains(DRAGGING_CLASS));
            Assert.AreSame(root.Q("TopLeftGrid"), handle.parent);
        }

        [Test]
        public void DropADragCutShortByHideWhereTheWidgetCameFrom()
        {
            //Arrange
            editor.Show(root);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.RecommendedPlaces6x2);
            Press(handle, 100f);
            Move(handle, 130f);

            //Act
            editor.Hide();

            //Assert
            Assert.IsTrue(host.Layout!.TryGetPlacement(LobbyWidgetId.RecommendedPlaces6x2, out LobbyWidgetPlacement placement));
            Assert.AreEqual(LobbyGridCorner.BottomRight, placement.Corner);
            VisualElement featuredPlaces = root.Q("FeaturedPlaces");
            Assert.AreSame(root.Q("BottomRightGrid"), featuredPlaces.parent);
            Assert.IsFalse(featuredPlaces.ClassListContains(LIFTED_CLASS));
            Assert.IsNull(handle.parent);
            Assert.IsFalse(handle.ClassListContains(DRAGGING_CLASS));
        }

        private LobbyWidgetHandleElement Handle(LobbyWidgetId widgetId) =>
            root.Q<LobbyWidgetHandleElement>(widgetId.ToString()) ?? throw new AssertionException($"{widgetId} has no handle");

        private static void Press(VisualElement element, float x)
        {
            using PointerDownEvent evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(x, 10f) });
            evt.target = element;
            element.SendEvent(evt);
        }

        private static void Move(VisualElement element, float x)
        {
            using PointerMoveEvent evt = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = new Vector2(x, 10f) });
            evt.target = element;
            element.SendEvent(evt);
        }

        private static void Release(VisualElement element, float x)
        {
            using PointerUpEvent evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(x, 10f) });
            evt.target = element;
            element.SendEvent(evt);
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
            unplaced.Add(new VisualElement { name = "Welcome" });
            unplaced.Add(new VisualElement { name = "LiveEvents" });
            unplaced.Add(new VisualElement { name = "UpcomingEvents" });
            unplaced.Add(new VisualElement { name = "Friends" });
            unplaced.Add(new VisualElement { name = "RecentPlaces" });
            unplaced.Add(new VisualElement { name = "FeaturedPlaces" });

            lobby.Add(new VisualElement { name = "LayoutDragLayer" });
            return lobby;
        }
    }
}
