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
        private const string CARD_DRAGGING_CLASS = "lobby-widget-card--dragging";
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
            Assert.AreEqual(6, root.Query<LobbyWidgetHandleElement>().ToList().Count, "Only the placed widgets have a handle");

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
        public void ListEveryFamilyWithNoSizePlacedInTheModalOnShow()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.JumpBackIn6x2);
            host.Layout.Remove(LobbyWidgetId.LiveEvents3x3);
            host.Apply();

            //Act
            editor.Show(root);

            //Assert
            VisualElement items = root.Q("WidgetModalItems");
            Assert.AreEqual(3, items.childCount, "The four-column places belong to placed families, so they get no card");
            Assert.AreSame(Card(LobbyWidgetFamily.LiveEvents), items[0], "The cards follow the order of the families");
            Assert.AreSame(Card(LobbyWidgetFamily.JumpBackIn), items[1]);
            Assert.AreSame(Card(LobbyWidgetFamily.GoTo), items[2], "The go-to row is off the grids by default");
            Assert.AreEqual(4, root.Query<LobbyWidgetHandleElement>().ToList().Count, "The unplaced widgets have no handle");

            LobbyWidgetCardElement liveEvents = Card(LobbyWidgetFamily.LiveEvents);
            Assert.AreEqual("Live Events", liveEvents.Q<Label>("Title").text);
            Assert.AreEqual("3 x 3", liveEvents.Q<Label>("Size").text, "A family of one size states it");
            Assert.IsNull(liveEvents.Q("Sizes"));

            LobbyWidgetCardElement jumpBackIn = Card(LobbyWidgetFamily.JumpBackIn);
            Assert.AreEqual("Jump Back In", jumpBackIn.Q<Label>("Title").text);
            Assert.IsNull(jumpBackIn.Q("Size"), "A family of several sizes has a button per size instead");
            VisualElement sizes = jumpBackIn.Q("Sizes");
            Assert.AreEqual(2, sizes.childCount);
            Assert.AreEqual("6x2", ((Label)sizes[0]).text);
            Assert.AreEqual(LobbyWidgetId.JumpBackIn6x2.ToString(), sizes[0].name);
            Assert.AreEqual("4x2", ((Label)sizes[1]).text);
            Assert.AreEqual(LobbyWidgetId.JumpBackIn4x2.ToString(), sizes[1].name);

            LobbyWidgetPreviewElement preview = jumpBackIn.Preview(LobbyWidgetId.JumpBackIn6x2);
            Assert.AreSame(jumpBackIn.Q("PreviewSlot"), preview.parent, "The card shows the scaled preview of the first size");
            Assert.AreEqual(LobbyWidgetCardElement.PREVIEW_SCALE, preview.style.scale.value.value.x);
            Assert.AreEqual("Jump Back In", preview.Q<Label>("Title").text);
            Assert.AreEqual(3, preview.Q("Cards").childCount);
            Assert.IsNull(jumpBackIn.Preview(LobbyWidgetId.JumpBackIn4x2).parent, "The other sizes wait out of the hierarchy");
            Vector2 slotSize = LobbyGridElement.SpanToSize(LobbyWidgetId.JumpBackIn6x2.Span()) * LobbyWidgetCardElement.PREVIEW_SCALE;
            Assert.AreEqual(slotSize.x, jumpBackIn.Q("PreviewSlot").style.width.value.value);
            Assert.AreEqual(slotSize.y, jumpBackIn.Q("PreviewSlot").style.height.value.value);
        }

        [Test]
        public void ListNoCardWhileAnySizeOfTheFamilyIsPlaced()
        {
            //Act
            editor.Show(root);

            //Assert
            Assert.AreEqual(1, root.Q("WidgetModalItems").childCount, "Every family but the go-to row has a size placed by default");
            Assert.AreSame(Card(LobbyWidgetFamily.GoTo), root.Q("WidgetModalItems")[0]);

            //Act
            host.Layout!.Remove(LobbyWidgetId.RecommendedPlaces6x2);
            host.Apply();
            editor.Show(root);

            //Assert
            VisualElement items = root.Q("WidgetModalItems");
            Assert.AreEqual(2, items.childCount);
            Assert.AreSame(Card(LobbyWidgetFamily.RecommendedPlaces), items[0]);
            Assert.AreSame(Card(LobbyWidgetFamily.GoTo), items[1]);
        }

        [Test]
        public void MoveTheWidgetBetweenTheModalAndTheGridAsTheLayoutChanges()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.Landing4x3);
            host.Apply();
            editor.Show(root);
            LobbyWidgetCardElement card = Card(LobbyWidgetFamily.Landing);
            Assert.AreSame(root.Q("WidgetModalItems"), card.parent);
            Assert.IsNull(root.Q<LobbyWidgetHandleElement>(LobbyWidgetId.Landing4x3.ToString()));

            //Act
            host.Layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopRight, new Vector2Int(0, 1));
            host.Apply();
            editor.Show(root);

            //Assert
            Assert.IsNull(card.parent);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.Landing4x3);
            Assert.AreSame(root.Q("TopRightGrid"), handle.parent);
            Vector2 size = LobbyGridElement.SpanToSize(LobbyWidgetId.Landing4x3.Span());
            Assert.AreEqual(size.x, handle.style.width.value.value);
            Assert.AreEqual(size.y, handle.style.height.value.value);
            Assert.AreEqual(LobbyGridElement.CellPitch.y, handle.style.top.value.value);
            Assert.AreEqual(1, root.Q("WidgetModalItems").childCount, "Only the go-to row is left off the grids");
        }

        [Test]
        public void LeaveNothingBehindOnHide()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.Landing4x3);
            host.Apply();
            editor.Show(root);

            //Act
            editor.Hide();

            //Assert
            Assert.IsFalse(editor.IsEditing);
            Assert.IsFalse(root.ClassListContains(EDITING_CLASS));
            Assert.AreEqual(0, root.Query<LobbyWidgetHandleElement>().ToList().Count);
            Assert.AreEqual(0, root.Query<LobbyWidgetCardElement>().ToList().Count);
            Assert.AreEqual(0, root.Q("WidgetModalItems").childCount);
            Assert.AreEqual(0, root.Q("LayoutDragLayer").childCount);
            Assert.IsNull(root.Q("DropMarker"));
            Assert.IsNull(root.Q("GridShield"));
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
        public void LiftTheFirstSizeOffTheCardWhileDragging()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.UpcomingEvents3x2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetCardElement card = Card(LobbyWidgetFamily.UpcomingEvents);

            //Act
            Press(card, 100f);
            Move(card, 130f);

            //Assert
            LobbyWidgetPreviewElement preview = card.Preview(LobbyWidgetId.UpcomingEvents3x2);
            Vector2 size = LobbyGridElement.SpanToSize(LobbyWidgetId.UpcomingEvents3x2.Span());
            Assert.AreSame(root.Q("LayoutDragLayer"), preview.parent, "The preview is lifted, not the section");
            Assert.AreSame(root.Q("UnplacedWidgets"), root.Q("UpcomingEvents").parent);
            Assert.IsTrue(preview.ClassListContains(LIFTED_CLASS));
            Assert.AreEqual(1f, preview.style.scale.value.value.x, "The preview is lifted at full size");
            Assert.AreEqual(130f - (size.x * 0.5f), preview.style.left.value.value, "The widget hangs from its middle");
            Assert.AreEqual(10f - (size.y * 0.5f), preview.style.top.value.value);
            Assert.IsTrue(card.ClassListContains(CARD_DRAGGING_CLASS));
            Assert.AreSame(root.Q("WidgetModalItems"), card.parent, "The card marks the origin until the drop");
        }

        [Test]
        public void LiftTheSizePickedOnTheCardWhileDragging()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.JumpBackIn6x2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetCardElement card = Card(LobbyWidgetFamily.JumpBackIn);
            VisualElement sizeButton = card.Q(LobbyWidgetId.JumpBackIn4x2.ToString());

            //Act
            Press(sizeButton, 100f);
            Move(sizeButton, 130f);

            //Assert
            LobbyWidgetPreviewElement preview = card.Preview(LobbyWidgetId.JumpBackIn4x2);
            Vector2 size = LobbyGridElement.SpanToSize(LobbyWidgetId.JumpBackIn4x2.Span());
            Assert.AreSame(root.Q("LayoutDragLayer"), preview.parent, "The preview of the picked size is lifted");
            Assert.AreEqual(1f, preview.style.scale.value.value.x);
            Assert.AreEqual(130f - (size.x * 0.5f), preview.style.left.value.value);
            Assert.AreSame(card.Q("PreviewSlot"), card.Preview(LobbyWidgetId.JumpBackIn6x2).parent, "The card keeps showing its first size");
            Assert.IsTrue(card.ClassListContains(CARD_DRAGGING_CLASS));
            Assert.AreEqual(1, root.Q("LayoutDragLayer").childCount, "The card itself does not drag along");
        }

        [Test]
        public void ReturnTheSizeToTheCardOnADropOutsideEveryGrid()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.JumpBackIn6x2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetCardElement card = Card(LobbyWidgetFamily.JumpBackIn);
            VisualElement sizeButton = card.Q(LobbyWidgetId.JumpBackIn4x2.ToString());

            //Act
            Press(sizeButton, 100f);
            Move(sizeButton, 130f);
            Release(sizeButton, 130f);

            //Assert
            Assert.IsFalse(host.Layout.TryGetPlacement(LobbyWidgetId.JumpBackIn4x2, out _));
            Assert.IsFalse(DCLPlayerPrefs.HasKey(DCLPrefKeys.LOBBY_LAYOUT), "A drop that does not take stores nothing");
            LobbyWidgetPreviewElement preview = card.Preview(LobbyWidgetId.JumpBackIn4x2);
            Assert.IsNull(preview.parent, "The lifted preview goes away");
            Assert.IsFalse(preview.ClassListContains(LIFTED_CLASS));
            Assert.AreSame(card.Q("PreviewSlot"), card.Preview(LobbyWidgetId.JumpBackIn6x2).parent);
            Assert.AreEqual(LobbyWidgetCardElement.PREVIEW_SCALE, card.Preview(LobbyWidgetId.JumpBackIn6x2).style.scale.value.value.x);
            Assert.AreEqual(0, root.Q("LayoutDragLayer").childCount);
            Assert.IsFalse(card.ClassListContains(CARD_DRAGGING_CLASS));
            Assert.AreSame(root.Q("WidgetModalItems"), card.parent);
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
            Assert.IsNull(handle.parent);
            Assert.AreSame(root.Q("WidgetModalItems"), Card(LobbyWidgetFamily.UpcomingEvents).parent);
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

        [Test]
        public void DropACardDragCutShortByHideBackIntoTheCard()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.FriendsOnline4x2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetCardElement card = Card(LobbyWidgetFamily.FriendsOnline);
            Press(card, 100f);
            Move(card, 130f);

            //Act
            editor.Hide();

            //Assert
            Assert.IsFalse(host.Layout.TryGetPlacement(LobbyWidgetId.FriendsOnline4x2, out _));
            LobbyWidgetPreviewElement preview = card.Preview(LobbyWidgetId.FriendsOnline4x2);
            Assert.AreSame(card.Q("PreviewSlot"), preview.parent);
            Assert.IsFalse(preview.ClassListContains(LIFTED_CLASS));
            Assert.AreEqual(0, root.Q("LayoutDragLayer").childCount);
            Assert.IsNull(card.parent);
            Assert.IsFalse(card.ClassListContains(CARD_DRAGGING_CLASS));
        }

        private LobbyWidgetHandleElement Handle(LobbyWidgetId widgetId) =>
            root.Q<LobbyWidgetHandleElement>(widgetId.ToString()) ?? throw new AssertionException($"{widgetId} has no handle");

        private LobbyWidgetCardElement Card(LobbyWidgetFamily family) =>
            root.Q<LobbyWidgetCardElement>(family.ToString()) ?? throw new AssertionException($"{family} has no card");

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
            unplaced.Add(new VisualElement { name = "GoTo" });

            var modal = new VisualElement { name = "WidgetModal" };
            modal.Add(new ScrollView { name = "WidgetModalItems" });
            lobby.Add(modal);
            lobby.Add(new VisualElement { name = "LayoutDragLayer" });
            return lobby;
        }
    }
}
