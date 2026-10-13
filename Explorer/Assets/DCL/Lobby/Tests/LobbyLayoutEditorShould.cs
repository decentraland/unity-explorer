using DCL.Prefs;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    /// <summary>
    ///     The hierarchy gets no layout pass here, so every grid and the modal have an empty extent and no drag lands on
    ///     one: the drop geometry is covered by <see cref="LobbyGridElementShould" /> and <see cref="LobbyLayoutShould" />.
    /// </summary>
    [TestFixture]
    public class LobbyLayoutEditorShould
    {
        private const string EDITING_CLASS = "lobby--editing";
        private const string DRAGGING_CLASS = "lobby-widget-handle--dragging";
        private const string LIFTED_CLASS = "lobby__widget--lifted";
        private const string SHELVED_CLASS = "lobby-widget-handle--shelved";

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
            Assert.AreEqual(8, root.Query<LobbyWidgetHandleElement>().ToList().Count);

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
        public void ShieldTheSectionsOfEveryGridFromThePointerUnderTheHandles()
        {
            //Act
            editor.Show(root);

            //Assert
            Assert.AreEqual(4, root.Query("GridShield").ToList().Count);

            VisualElement grid = root.Q("TopLeftGrid");
            VisualElement shield = grid.Q("GridShield");
            Assert.AreEqual(PickingMode.Position, shield.pickingMode);
            Assert.Less(grid.IndexOf(root.Q("Welcome")), grid.IndexOf(shield));
            Assert.Less(grid.IndexOf(root.Q("Friends")), grid.IndexOf(shield));
            Assert.Less(grid.IndexOf(shield), grid.IndexOf(Handle(LobbyWidgetId.Landing4x3)));
            Assert.Less(grid.IndexOf(shield), grid.IndexOf(Handle(LobbyWidgetId.FriendsOnline4x2)));
        }

        [Test]
        public void KeepTheShieldBetweenASectionMovedOntoAnotherGridAndItsHandle()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.Landing4x3);
            host.Apply();
            editor.Show(root);

            //Act
            host.Layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopRight, new Vector2Int(0, 1));
            host.Apply();
            editor.Show(root);

            //Assert
            VisualElement grid = root.Q("TopRightGrid");
            VisualElement shield = grid.Q("GridShield");
            Assert.Less(grid.IndexOf(root.Q("Welcome")), grid.IndexOf(shield));
            Assert.Less(grid.IndexOf(shield), grid.IndexOf(Handle(LobbyWidgetId.Landing4x3)));
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
        public void ListEveryUnplacedWidgetInTheModalOnShow()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.JumpBackIn6x2);
            host.Layout.Remove(LobbyWidgetId.LiveEvents3x3);
            host.Apply();

            //Act
            editor.Show(root);

            //Assert
            VisualElement items = root.Q("WidgetModalItems");
            Assert.AreEqual(4, items.childCount, "The four-column places are unplaced by default");
            Assert.AreSame(Handle(LobbyWidgetId.LiveEvents3x3), items[0], "The rows follow the order of the widget ids");
            Assert.AreSame(Handle(LobbyWidgetId.JumpBackIn6x2), items[1]);
            Assert.AreSame(Handle(LobbyWidgetId.JumpBackIn4x2), items[2]);
            Assert.AreSame(Handle(LobbyWidgetId.RecommendedPlaces4x2), items[3]);
            Assert.AreEqual(8, root.Query<LobbyWidgetHandleElement>().ToList().Count, "Every widget has one handle, on its grid or in the modal");

            LobbyWidgetHandleElement jumpBackIn = Handle(LobbyWidgetId.JumpBackIn6x2);
            Assert.IsTrue(jumpBackIn.IsShelved);
            Assert.IsTrue(jumpBackIn.ClassListContains(SHELVED_CLASS));
            Assert.AreEqual("Jump Back In", jumpBackIn.Q<Label>("Label").text);
            Assert.AreEqual("6 x 2", jumpBackIn.Q<Label>("Size").text);
            LobbyWidgetPreviewElement preview = jumpBackIn.Preview;
            Assert.AreSame(jumpBackIn.Q("PreviewSlot"), preview.parent, "The row shows the scaled preview of the widget");
            Assert.AreEqual(LobbyWidgetHandleElement.PREVIEW_SCALE, preview.style.scale.value.value.x);
            Assert.AreEqual("Jump Back In", preview.Q<Label>("Title").text);
            Assert.AreEqual(3, preview.Q("Cards").childCount);
            Assert.AreEqual(LobbyGridElement.SpanToSize(LobbyWidgetId.JumpBackIn6x2.Span()).y * LobbyWidgetHandleElement.PREVIEW_SCALE, jumpBackIn.Q("PreviewSlot").style.height.value.value);
            Assert.IsNull(Handle(LobbyWidgetId.Landing4x3).Preview.parent, "A placed handle shows no preview");
            Assert.AreEqual(StyleKeyword.Null, jumpBackIn.style.width.keyword, "A row takes the size of its stylesheet");
            Assert.AreEqual(StyleKeyword.Null, jumpBackIn.style.height.keyword);
            Assert.AreEqual(StyleKeyword.Null, jumpBackIn.style.left.keyword);
            Assert.AreEqual(StyleKeyword.Null, jumpBackIn.style.top.keyword);
            Assert.IsFalse(Handle(LobbyWidgetId.Landing4x3).IsShelved);
        }

        [Test]
        public void MoveTheHandleBetweenTheModalAndTheGridAsTheLayoutChanges()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.Landing4x3);
            host.Apply();
            editor.Show(root);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.Landing4x3);
            Assert.AreSame(root.Q("WidgetModalItems"), handle.parent);

            //Act
            host.Layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopRight, new Vector2Int(0, 1));
            host.Apply();
            editor.Show(root);

            //Assert
            Assert.AreSame(root.Q("TopRightGrid"), handle.parent);
            Assert.IsFalse(handle.IsShelved);
            Assert.IsFalse(handle.ClassListContains(SHELVED_CLASS));
            Vector2 size = LobbyGridElement.SpanToSize(LobbyWidgetId.Landing4x3.Span());
            Assert.AreEqual(size.x, handle.style.width.value.value);
            Assert.AreEqual(size.y, handle.style.height.value.value);
            Assert.AreEqual(LobbyGridElement.CellPitch.y, handle.style.top.value.value);
            Assert.AreEqual(2, root.Q("WidgetModalItems").childCount, "Only the four-column places stay in the modal");
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
            Assert.AreEqual(0, root.Q("WidgetModalItems").childCount);
            Assert.AreEqual(0, root.Q("LayoutDragLayer").childCount);
            Assert.IsNull(root.Q("DropMarker"));
            Assert.IsNull(root.Q("GridShield"));
        }

        [Test]
        public void EmptyTheModalOnHideWhateverIsUnplaced()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.FriendsOnline4x2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.FriendsOnline4x2);

            //Act
            editor.Hide();

            //Assert
            Assert.AreEqual(0, root.Q("WidgetModalItems").childCount);
            Assert.IsNull(handle.parent);
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
        public void LiftTheWidgetOffTheModalWhileDragging()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.UpcomingEvents3x2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.UpcomingEvents3x2);

            //Act
            Press(handle, 100f);
            Move(handle, 130f);

            //Assert
            LobbyWidgetPreviewElement preview = handle.Preview;
            Vector2 size = LobbyGridElement.SpanToSize(LobbyWidgetId.UpcomingEvents3x2.Span());
            Assert.AreSame(root.Q("LayoutDragLayer"), preview.parent, "The preview is lifted, not the section");
            Assert.AreSame(root.Q("UnplacedWidgets"), root.Q("UpcomingEvents").parent);
            Assert.IsTrue(preview.ClassListContains(LIFTED_CLASS));
            Assert.AreEqual(1f, preview.style.scale.value.value.x, "The preview is lifted at full size");
            Assert.AreEqual(130f - (size.x * 0.5f), preview.style.left.value.value, "The widget hangs from its middle");
            Assert.AreEqual(10f - (size.y * 0.5f), preview.style.top.value.value);
            Assert.IsTrue(handle.ClassListContains(DRAGGING_CLASS));
            Assert.AreSame(root.Q("WidgetModalItems"), handle.parent, "The row marks the origin until the drop");
        }

        [Test]
        public void ReturnTheWidgetToTheModalOnADropOutsideEveryGrid()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.UpcomingEvents3x2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.UpcomingEvents3x2);

            //Act
            Press(handle, 100f);
            Move(handle, 130f);
            Release(handle, 130f);

            //Assert
            Assert.IsFalse(host.Layout.TryGetPlacement(LobbyWidgetId.UpcomingEvents3x2, out _));
            Assert.IsFalse(DCLPlayerPrefs.HasKey(DCLPrefKeys.LOBBY_LAYOUT), "A drop that does not take stores nothing");
            LobbyWidgetPreviewElement preview = handle.Preview;
            Assert.AreSame(handle.Q("PreviewSlot"), preview.parent, "The preview goes back into its row");
            Assert.AreEqual(LobbyWidgetHandleElement.PREVIEW_SCALE, preview.style.scale.value.value.x);
            Assert.IsFalse(preview.ClassListContains(LIFTED_CLASS));
            Assert.AreEqual(0, root.Q("LayoutDragLayer").childCount);
            Assert.IsFalse(handle.ClassListContains(DRAGGING_CLASS));
            Assert.IsTrue(handle.IsShelved);
            Assert.AreSame(root.Q("WidgetModalItems"), handle.parent);
        }

        [Test]
        public void TakeTheWidgetOffItsGridWithTheButtonOfItsHandle()
        {
            //Arrange
            editor.Show(root);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.UpcomingEvents3x2);

            //Act
            Submit(handle.Q<Button>("Remove"));

            //Assert
            Assert.IsFalse(host.Layout!.TryGetPlacement(LobbyWidgetId.UpcomingEvents3x2, out _));
            Assert.IsTrue(DCLPlayerPrefs.HasKey(DCLPrefKeys.LOBBY_LAYOUT), "Taking a widget off its grid is stored at once");
            Assert.AreSame(root.Q("UnplacedWidgets"), root.Q("UpcomingEvents").parent);
            Assert.IsTrue(handle.IsShelved);
            Assert.AreSame(root.Q("WidgetModalItems"), handle.parent);
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

        // A submit is reported as a click like a keyboard press; unlike a pointer click it needs no laid-out panel
        private static void Submit(Button button)
        {
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }

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
            unplaced.Add(new VisualElement { name = "RecentPlaces4x2" });
            unplaced.Add(new VisualElement { name = "FeaturedPlaces4x2" });

            var modal = new VisualElement { name = "WidgetModal" };
            modal.Add(new VisualElement { name = "WidgetModalItems" });
            lobby.Add(modal);
            lobby.Add(new VisualElement { name = "LayoutDragLayer" });
            return lobby;
        }
    }
}
