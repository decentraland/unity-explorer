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
        private const string SIZE_SELECTED_CLASS = "lobby-widget-card__size-toggle--selected";
        private const string LIFTED_CLASS = "lobby__widget--lifted";

        private static readonly FieldInfo PREFS_FIELD = typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!;

        private GameObject documentGameObject = null!;
        private PanelSettings panelSettings = null!;
        private VisualElement root = null!;
        private LobbyWidgetHost host = null!;
        private LobbyLayoutEditor editor = null!;

        private Button FindSaveButton() => root.Q<Button>("SaveLayout");

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
        public void FrameEveryPlacedWidgetWithAHandleOnShow()
        {
            //Act
            editor.Show(root);

            //Assert
            Assert.IsTrue(editor.IsEditing);
            Assert.IsTrue(root.ClassListContains(EDITING_CLASS));
            Assert.AreEqual(6, root.Query<LobbyWidgetHandleElement>().ToList().Count, "Only the placed widgets have a handle");

            LobbyWidgetHandleElement friends = Handle(LobbyWidgetId.FriendsOnline4By2);
            Vector2 size = LobbyGridElement.SpanToSize(LobbyWidgetId.FriendsOnline4By2.Span());
            const float OUTSET = LobbyWidgetHandleElement.OUTSET;
            Assert.AreSame(root.Q("TopLeftGrid"), friends.parent);
            Assert.AreEqual(-OUTSET, friends.style.left.value.value, "The frame reaches past the cells");
            Assert.AreEqual((3 * LobbyGridElement.CellPitch.y) - OUTSET, friends.style.top.value.value);
            Assert.AreEqual(size.x + (2 * OUTSET), friends.style.width.value.value);
            Assert.AreEqual(size.y + (2 * OUTSET), friends.style.height.value.value);
            Assert.IsNotNull(friends.Q<Button>("Remove"));
        }

        [Test]
        public void PutTheHandlesOverTheSections()
        {
            //Act
            editor.Show(root);

            //Assert
            VisualElement grid = root.Q("TopLeftGrid");
            Assert.Less(grid.IndexOf(root.Q("Welcome")), grid.IndexOf(Handle(LobbyWidgetId.Landing4By3)));
            Assert.Less(grid.IndexOf(root.Q("Friends")), grid.IndexOf(Handle(LobbyWidgetId.FriendsOnline4By2)));
        }

        [Test]
        public void PutTheUnderlayOfEachHandleRightUnderItsSection()
        {
            //Act
            editor.Show(root);

            //Assert
            VisualElement grid = root.Q("TopLeftGrid");
            LobbyWidgetHandleElement friends = Handle(LobbyWidgetId.FriendsOnline4By2);
            Assert.AreSame(grid, friends.Underlay.parent);
            Assert.AreEqual(grid.IndexOf(root.Q("Friends")) - 1, grid.IndexOf(friends.Underlay));
            Assert.AreEqual(friends.style.left.value.value, friends.Underlay.style.left.value.value);
            Assert.AreEqual(friends.style.top.value.value, friends.Underlay.style.top.value.value);
            Assert.AreEqual(friends.style.width.value.value, friends.Underlay.style.width.value.value);
            Assert.AreEqual(6, root.Query("WidgetUnderlay").ToList().Count, "One per placed widget");
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
            Assert.Less(grid.IndexOf(shield), grid.IndexOf(Handle(LobbyWidgetId.Landing4By3)));
            Assert.Less(grid.IndexOf(shield), grid.IndexOf(Handle(LobbyWidgetId.FriendsOnline4By2)));
        }

        [Test]
        public void KeepTheShieldBetweenASectionMovedOntoAnotherGridAndItsHandle()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.Landing4By3);
            host.Layout.Remove(LobbyWidgetId.LiveEvents3By3);
            host.Layout.Remove(LobbyWidgetId.UpcomingEvents3By2);
            host.Apply();
            editor.Show(root);
            editor.Hide();

            //Act
            Assert.IsTrue(host.Layout.TryPlace(LobbyWidgetId.Landing4By3, LobbyGridCorner.TopRight, new Vector2Int(0, 1)), "The events made room");
            host.Apply();
            editor.Show(root);

            //Assert
            VisualElement grid = root.Q("TopRightGrid");
            VisualElement shield = grid.Q("GridShield");
            Assert.Less(grid.IndexOf(root.Q("Welcome")), grid.IndexOf(shield));
            Assert.Less(grid.IndexOf(shield), grid.IndexOf(Handle(LobbyWidgetId.Landing4By3)));
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
            host.Layout!.Remove(LobbyWidgetId.JumpBackIn6By2);
            host.Layout.Remove(LobbyWidgetId.LiveEvents3By3);
            host.Apply();

            //Act
            editor.Show(root);

            //Assert
            VisualElement items = root.Q("WidgetModalItems");
            Assert.AreEqual(5, items.childCount, "The four-column places belong to placed families, so they get no row");
            Assert.AreSame(Card(LobbyWidgetFamily.LiveEvents), items[0], "The rows follow the order of the families");
            Assert.AreSame(Card(LobbyWidgetFamily.JumpBackIn), items[1]);
            Assert.AreSame(Card(LobbyWidgetFamily.Communities), items[2], "The communities, the gallery and the go-to row are off the grids by default");
            Assert.AreSame(Card(LobbyWidgetFamily.Gallery), items[3]);
            Assert.AreSame(Card(LobbyWidgetFamily.GoTo), items[4]);
            Assert.AreEqual(4, root.Query<LobbyWidgetHandleElement>().ToList().Count, "The unplaced widgets have no handle");

            LobbyWidgetCardElement liveEvents = Card(LobbyWidgetFamily.LiveEvents);
            Assert.AreEqual("Live Events", liveEvents.Q<Label>("Title").text);
            Assert.IsNotNull(liveEvents.Q("Grip"));
            Assert.IsNotNull(liveEvents.Q("Icon"));
            Assert.IsNull(liveEvents.Q("Sizes"), "A family of one size has no size toggles");
            Assert.AreEqual(LobbyWidgetId.LiveEvents3By3, liveEvents.SelectedVariant);
            Assert.IsNull(liveEvents.Q<LobbyWidgetPreviewElement>(), "The rows show no preview");

            LobbyWidgetCardElement jumpBackIn = Card(LobbyWidgetFamily.JumpBackIn);
            Assert.AreEqual("Jump Back In", jumpBackIn.Q<Label>("Title").text);
            VisualElement sizes = jumpBackIn.Q("Sizes");
            Assert.AreEqual(2, sizes.childCount, "A family of several sizes has a toggle per size");
            Assert.AreEqual("x2", ((Label)sizes[0]).text, "A size reads as the cards it shows, the smaller first");
            Assert.AreEqual(LobbyWidgetId.JumpBackIn4By2.ToString(), sizes[0].name);
            Assert.AreEqual("x3", ((Label)sizes[1]).text);
            Assert.AreEqual(LobbyWidgetId.JumpBackIn6By2.ToString(), sizes[1].name);
            Assert.AreEqual(LobbyWidgetId.JumpBackIn4By2, jumpBackIn.SelectedVariant, "The smaller size starts selected");
            Assert.IsTrue(sizes[0].ClassListContains(SIZE_SELECTED_CLASS));
            Assert.IsFalse(sizes[1].ClassListContains(SIZE_SELECTED_CLASS));

            LobbyWidgetPreviewElement preview = jumpBackIn.Preview(LobbyWidgetId.JumpBackIn6By2);
            Assert.IsNull(preview.parent, "The stand-ins wait out of the hierarchy until a drag lifts one");
            Assert.AreEqual("Jump Back In", preview.Q<Label>("Title").text);
            Assert.AreEqual(3, preview.Q("Cards").childCount);
            Vector2 size = LobbyGridElement.SpanToSize(LobbyWidgetId.JumpBackIn6By2.Span());
            Assert.AreEqual(size.x, preview.style.width.value.value);
            Assert.AreEqual(size.y, preview.style.height.value.value);
        }

        [Test]
        public void ListNoRowWhileAnySizeOfTheFamilyIsPlaced()
        {
            //Act
            editor.Show(root);

            //Assert
            Assert.AreEqual(3, root.Q("WidgetModalItems").childCount, "Every family but the communities, the gallery and the go-to row has a size placed by default");
            Assert.AreSame(Card(LobbyWidgetFamily.Communities), root.Q("WidgetModalItems")[0]);
            Assert.AreSame(Card(LobbyWidgetFamily.Gallery), root.Q("WidgetModalItems")[1]);
            Assert.AreSame(Card(LobbyWidgetFamily.GoTo), root.Q("WidgetModalItems")[2]);

            //Act
            editor.Hide();
            host.Layout!.Remove(LobbyWidgetId.RecommendedPlaces6By2);
            host.Apply();
            editor.Show(root);

            //Assert
            VisualElement items = root.Q("WidgetModalItems");
            Assert.AreEqual(4, items.childCount);
            Assert.AreSame(Card(LobbyWidgetFamily.RecommendedPlaces), items[0]);
            Assert.AreSame(Card(LobbyWidgetFamily.Communities), items[1]);
            Assert.AreSame(Card(LobbyWidgetFamily.Gallery), items[2]);
            Assert.AreSame(Card(LobbyWidgetFamily.GoTo), items[3]);
        }

        [Test]
        public void MoveTheWidgetBetweenTheModalAndTheGridAsTheLayoutChanges()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.Landing4By3);
            host.Layout.Remove(LobbyWidgetId.LiveEvents3By3);
            host.Layout.Remove(LobbyWidgetId.UpcomingEvents3By2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetCardElement card = Card(LobbyWidgetFamily.Landing);
            Assert.AreSame(root.Q("WidgetModalItems"), card.parent);
            Assert.IsNull(root.Q<LobbyWidgetHandleElement>(LobbyWidgetId.Landing4By3.ToString()));
            editor.Hide();

            //Act
            Assert.IsTrue(host.Layout.TryPlace(LobbyWidgetId.Landing4By3, LobbyGridCorner.TopRight, new Vector2Int(0, 1)), "The events made room");
            host.Apply();
            editor.Show(root);

            //Assert
            Assert.IsNull(card.parent);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.Landing4By3);
            Assert.AreSame(root.Q("TopRightGrid"), handle.parent);
            Vector2 size = LobbyGridElement.SpanToSize(LobbyWidgetId.Landing4By3.Span());
            Assert.AreEqual(size.x + (2 * LobbyWidgetHandleElement.OUTSET), handle.style.width.value.value);
            Assert.AreEqual(size.y + (2 * LobbyWidgetHandleElement.OUTSET), handle.style.height.value.value);
            Assert.AreEqual(LobbyGridElement.CellPitch.y - LobbyWidgetHandleElement.OUTSET, handle.style.top.value.value);
            Assert.AreEqual(5, root.Q("WidgetModalItems").childCount, "The two event rows and the three off by default are left off the grids");
        }

        [Test]
        public void LeaveNothingBehindOnHide()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.Landing4By3);
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
            Assert.IsNull(root.Q("WidgetUnderlay"));
        }

        [Test]
        public void KeepSaveDisabledUntilTheLayoutChanges()
        {
            //Act
            editor.Show(root);

            //Assert
            Assert.IsFalse(FindSaveButton().enabledSelf, "Nothing to store on entry");

            //Act
            Submit(Handle(LobbyWidgetId.UpcomingEvents3By2).Q<Button>("Remove"));

            //Assert
            Assert.IsTrue(FindSaveButton().enabledSelf);
        }

        [Test]
        public void StoreTheLayoutAndLeaveEditModeOnSave()
        {
            //Arrange
            editor.Show(root);
            Submit(Handle(LobbyWidgetId.UpcomingEvents3By2).Q<Button>("Remove"));

            //Act
            Submit(FindSaveButton());

            //Assert
            Assert.IsFalse(editor.IsEditing);
            Assert.IsFalse(root.ClassListContains(EDITING_CLASS));
            Assert.IsFalse(host.Layout!.TryGetPlacement(LobbyWidgetId.UpcomingEvents3By2, out _), "The change stands");
            Assert.IsTrue(DCLPlayerPrefs.HasKey(DCLPrefKeys.LOBBY_LAYOUT));
            LobbyLayoutStorage.Load(host.Layout);
            Assert.IsFalse(host.Layout.TryGetPlacement(LobbyWidgetId.UpcomingEvents3By2, out _), "The change was stored");
            Assert.AreSame(root.Q("UnplacedWidgets"), root.Q("UpcomingEvents").parent);
        }

        [TestCase("CancelLayout")]
        [TestCase("BackFromLayout")]
        public void PutTheLayoutBackAndLeaveEditModeOnCancel(string buttonName)
        {
            //Arrange
            editor.Show(root);
            Submit(Handle(LobbyWidgetId.UpcomingEvents3By2).Q<Button>("Remove"));

            //Act
            Submit(root.Q<Button>(buttonName));

            //Assert
            Assert.IsFalse(editor.IsEditing);
            Assert.IsFalse(root.ClassListContains(EDITING_CLASS));
            Assert.IsTrue(host.Layout!.TryGetPlacement(LobbyWidgetId.UpcomingEvents3By2, out LobbyWidgetPlacement placement));
            Assert.AreEqual(LobbyGridCorner.TopRight, placement.Corner);
            Assert.AreEqual(new Vector2Int(1, 3), placement.Cell);
            Assert.AreSame(root.Q("TopRightGrid"), root.Q("UpcomingEvents").parent, "The section is back on its grid");
            Assert.IsFalse(DCLPlayerPrefs.HasKey(DCLPrefKeys.LOBBY_LAYOUT), "Nothing is stored");
        }

        [Test]
        public void PutTheLayoutBackOnHide()
        {
            //Arrange
            editor.Show(root);
            Submit(Handle(LobbyWidgetId.FriendsOnline4By2).Q<Button>("Remove"));

            //Act
            editor.Hide();

            //Assert
            Assert.IsTrue(host.Layout!.TryGetPlacement(LobbyWidgetId.FriendsOnline4By2, out LobbyWidgetPlacement placement));
            Assert.AreEqual(LobbyGridCorner.TopLeft, placement.Corner);
            Assert.AreEqual(new Vector2Int(0, 3), placement.Cell);
            Assert.AreSame(root.Q("TopLeftGrid"), root.Q("Friends").parent);
            Assert.IsFalse(DCLPlayerPrefs.HasKey(DCLPrefKeys.LOBBY_LAYOUT));
        }

        [Test]
        public void GiveTheRequiredWidgetNoRemoveButton()
        {
            //Act
            editor.Show(root);

            //Assert
            Assert.IsNull(Handle(LobbyLayout.REQUIRED_WIDGET).Q<Button>("Remove"), "The landing stays on a grid");
            Assert.IsNotNull(Handle(LobbyWidgetId.FriendsOnline4By2).Q<Button>("Remove"));
        }

        [Test]
        public void PutTheShippedLayoutUpOnReset()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.Landing4By3);
            host.Apply();
            editor.Show(root);
            LobbyWidgetCardElement landingCard = Card(LobbyWidgetFamily.Landing);
            Assert.IsFalse(FindSaveButton().enabledSelf);

            //Act
            Submit(root.Q<Button>("ResetLayout"));

            //Assert
            Assert.IsTrue(editor.IsEditing, "Reset stays in edit mode");
            Assert.IsTrue(host.Layout.TryGetPlacement(LobbyWidgetId.Landing4By3, out LobbyWidgetPlacement placement));
            Assert.AreEqual(LobbyGridCorner.TopLeft, placement.Corner);
            Assert.AreEqual(Vector2Int.zero, placement.Cell);
            Assert.AreSame(root.Q("TopLeftGrid"), Handle(LobbyWidgetId.Landing4By3).parent);
            Assert.IsNull(landingCard.parent, "The row left the modal");
            Assert.IsTrue(FindSaveButton().enabledSelf, "The shipped layout differs from the one on entry");
            Assert.IsFalse(DCLPlayerPrefs.HasKey(DCLPrefKeys.LOBBY_LAYOUT), "Nothing is stored until Save");
        }

        [Test]
        public void LiftTheWidgetOntoTheDragLayerInAFrameWhileDragging()
        {
            //Arrange
            editor.Show(root);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.Landing4By3);

            //Act
            Press(handle, 100f);
            Move(handle, 130f);

            //Assert
            VisualElement dragLayer = root.Q("LayoutDragLayer");
            VisualElement welcome = root.Q("Welcome");
            VisualElement frame = root.Q("DragFrame");
            Assert.AreSame(dragLayer, welcome.parent);
            Assert.AreSame(dragLayer, frame.parent);
            Assert.Less(dragLayer.IndexOf(frame), dragLayer.IndexOf(welcome), "The frame draws under the widget");
            Vector2 size = LobbyGridElement.SpanToSize(LobbyWidgetId.Landing4By3.Span());
            Assert.AreEqual(size.x + (2 * LobbyWidgetHandleElement.OUTSET), frame.style.width.value.value);
            Assert.AreEqual(welcome.style.left.value.value - LobbyWidgetHandleElement.OUTSET, frame.style.left.value.value);
            Assert.IsTrue(welcome.ClassListContains(LIFTED_CLASS));
            Assert.IsTrue(handle.ClassListContains(DRAGGING_CLASS));
            Assert.IsTrue(handle.Underlay.ClassListContains(DRAGGING_CLASS), "The underlay hides with the handle");
            Assert.AreSame(root.Q("TopLeftGrid"), handle.parent, "The handle marks the origin until the drop");
        }

        [Test]
        public void LiftTheFirstSizeOffTheRowWhileDragging()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.UpcomingEvents3By2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetCardElement card = Card(LobbyWidgetFamily.UpcomingEvents);

            //Act
            Press(card, 100f);
            Move(card, 130f);

            //Assert
            LobbyWidgetPreviewElement preview = card.Preview(LobbyWidgetId.UpcomingEvents3By2);
            Vector2 size = LobbyGridElement.SpanToSize(LobbyWidgetId.UpcomingEvents3By2.Span());
            Assert.AreSame(root.Q("LayoutDragLayer"), preview.parent, "The stand-in is lifted, not the section");
            Assert.AreSame(root.Q("UnplacedWidgets"), root.Q("UpcomingEvents").parent);
            Assert.IsTrue(preview.ClassListContains(LIFTED_CLASS));
            Assert.AreEqual(130f - (size.x * 0.5f), preview.style.left.value.value, "The widget hangs from its middle");
            Assert.AreEqual(10f - (size.y * 0.5f), preview.style.top.value.value);
            Assert.IsTrue(card.ClassListContains(CARD_DRAGGING_CLASS));
            Assert.AreSame(root.Q("WidgetModalItems"), card.parent, "The row marks the origin until the drop");
        }

        [Test]
        public void SelectTheSizeOfAPressedToggle()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.JumpBackIn6By2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetCardElement card = Card(LobbyWidgetFamily.JumpBackIn);
            VisualElement sizes = card.Q("Sizes");
            VisualElement larger = card.Q(LobbyWidgetId.JumpBackIn6By2.ToString());

            //Act
            Press(larger, 100f);
            Release(larger, 100f);

            //Assert
            Assert.AreEqual(LobbyWidgetId.JumpBackIn6By2, card.SelectedVariant);
            Assert.IsTrue(sizes[1].ClassListContains(SIZE_SELECTED_CLASS));
            Assert.IsFalse(sizes[0].ClassListContains(SIZE_SELECTED_CLASS), "One size at a time");
            Assert.AreEqual(0, root.Q("LayoutDragLayer").childCount, "A click lifts nothing");
            Assert.IsFalse(card.ClassListContains(CARD_DRAGGING_CLASS));
        }

        [Test]
        public void LiftTheSelectedSizeOffTheRowWhileDragging()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.JumpBackIn6By2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetCardElement card = Card(LobbyWidgetFamily.JumpBackIn);
            VisualElement larger = card.Q(LobbyWidgetId.JumpBackIn6By2.ToString());
            Press(larger, 100f);
            Release(larger, 100f);

            //Act
            Press(card, 100f);
            Move(card, 130f);

            //Assert
            LobbyWidgetPreviewElement preview = card.Preview(LobbyWidgetId.JumpBackIn6By2);
            Vector2 size = LobbyGridElement.SpanToSize(LobbyWidgetId.JumpBackIn6By2.Span());
            Assert.AreSame(root.Q("LayoutDragLayer"), preview.parent, "The stand-in of the selected size is lifted");
            Assert.AreEqual(130f - (size.x * 0.5f), preview.style.left.value.value);
            Assert.IsNull(card.Preview(LobbyWidgetId.JumpBackIn4By2).parent, "The other size stays away");
            Assert.IsTrue(card.ClassListContains(CARD_DRAGGING_CLASS));
            Assert.AreEqual(2, root.Q("LayoutDragLayer").childCount, "The frame and the stand-in: the row itself does not drag along");
        }

        [Test]
        public void LiftTheSizeOfTheToggleADragStartsOn()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.JumpBackIn6By2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetCardElement card = Card(LobbyWidgetFamily.JumpBackIn);
            VisualElement larger = card.Q(LobbyWidgetId.JumpBackIn6By2.ToString());

            //Act
            Press(larger, 100f);
            Move(larger, 130f);

            //Assert
            Assert.AreEqual(LobbyWidgetId.JumpBackIn6By2, card.SelectedVariant, "The press selects the size before the drag carries it");
            LobbyWidgetPreviewElement preview = card.Preview(LobbyWidgetId.JumpBackIn6By2);
            Assert.AreSame(root.Q("LayoutDragLayer"), preview.parent);
            Assert.IsNull(card.Preview(LobbyWidgetId.JumpBackIn4By2).parent, "The other size stays away");
            Assert.IsTrue(card.ClassListContains(CARD_DRAGGING_CLASS));
        }

        [Test]
        public void PutTheSizeAwayOnADropOutsideEveryGrid()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.JumpBackIn6By2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetCardElement card = Card(LobbyWidgetFamily.JumpBackIn);
            VisualElement sizeButton = card.Q(LobbyWidgetId.JumpBackIn4By2.ToString());

            //Act
            Press(sizeButton, 100f);
            Move(sizeButton, 130f);
            Release(sizeButton, 130f);

            //Assert
            Assert.IsFalse(host.Layout.TryGetPlacement(LobbyWidgetId.JumpBackIn4By2, out _));
            Assert.IsFalse(FindSaveButton().enabledSelf, "A drop that does not take changes nothing");
            LobbyWidgetPreviewElement preview = card.Preview(LobbyWidgetId.JumpBackIn4By2);
            Assert.IsNull(preview.parent, "The lifted stand-in goes away");
            Assert.IsFalse(preview.ClassListContains(LIFTED_CLASS));
            Assert.AreEqual(0, root.Q("LayoutDragLayer").childCount, "The frame goes away with it");
            Assert.IsFalse(card.ClassListContains(CARD_DRAGGING_CLASS));
            Assert.AreSame(root.Q("WidgetModalItems"), card.parent);
        }

        [Test]
        public void TakeTheWidgetOffItsGridWithTheButtonOfItsHandle()
        {
            //Arrange
            editor.Show(root);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.UpcomingEvents3By2);

            //Act
            Submit(handle.Q<Button>("Remove"));

            //Assert
            Assert.IsFalse(host.Layout!.TryGetPlacement(LobbyWidgetId.UpcomingEvents3By2, out _));
            Assert.IsFalse(DCLPlayerPrefs.HasKey(DCLPrefKeys.LOBBY_LAYOUT), "Nothing is stored until Save");
            Assert.AreSame(root.Q("UnplacedWidgets"), root.Q("UpcomingEvents").parent);
            Assert.IsNull(handle.parent);
            Assert.AreSame(root.Q("WidgetModalItems"), Card(LobbyWidgetFamily.UpcomingEvents).parent);
        }

        [Test]
        public void KeepTheWidgetWhereItWasOnADropOutsideEveryGrid()
        {
            //Arrange
            editor.Show(root);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.Landing4By3);

            //Act
            Press(handle, 100f);
            Move(handle, 130f);
            Release(handle, 130f);

            //Assert
            Assert.IsTrue(host.Layout!.TryGetPlacement(LobbyWidgetId.Landing4By3, out LobbyWidgetPlacement placement));
            Assert.AreEqual(LobbyGridCorner.TopLeft, placement.Corner);
            Assert.AreEqual(Vector2Int.zero, placement.Cell);
            Assert.IsFalse(FindSaveButton().enabledSelf, "A drop that does not take changes nothing");
            VisualElement welcome = root.Q("Welcome");
            Assert.AreSame(root.Q("TopLeftGrid"), welcome.parent, "The widget lands back on its grid");
            Assert.IsFalse(welcome.ClassListContains(LIFTED_CLASS));
            Assert.IsNull(root.Q("DropMarker"));
            Assert.IsNull(root.Q("DragFrame"));
            Assert.IsFalse(handle.ClassListContains(DRAGGING_CLASS));
            Assert.AreSame(root.Q("TopLeftGrid"), handle.parent);
        }

        [Test]
        public void DropADragCutShortByHideWhereTheWidgetCameFrom()
        {
            //Arrange
            editor.Show(root);
            LobbyWidgetHandleElement handle = Handle(LobbyWidgetId.RecommendedPlaces6By2);
            Press(handle, 100f);
            Move(handle, 130f);

            //Act
            editor.Hide();

            //Assert
            Assert.IsTrue(host.Layout!.TryGetPlacement(LobbyWidgetId.RecommendedPlaces6By2, out LobbyWidgetPlacement placement));
            Assert.AreEqual(LobbyGridCorner.BottomRight, placement.Corner);
            VisualElement featuredPlaces = root.Q("FeaturedPlaces");
            Assert.AreSame(root.Q("BottomRightGrid"), featuredPlaces.parent);
            Assert.IsFalse(featuredPlaces.ClassListContains(LIFTED_CLASS));
            Assert.IsNull(handle.parent);
            Assert.IsFalse(handle.ClassListContains(DRAGGING_CLASS));
            Assert.AreEqual(0, root.Q("LayoutDragLayer").childCount);
        }

        [Test]
        public void DropARowDragCutShortByHideAway()
        {
            //Arrange
            host.Layout!.Remove(LobbyWidgetId.FriendsOnline4By2);
            host.Apply();
            editor.Show(root);
            LobbyWidgetCardElement card = Card(LobbyWidgetFamily.FriendsOnline);
            Press(card, 100f);
            Move(card, 130f);

            //Act
            editor.Hide();

            //Assert
            Assert.IsFalse(host.Layout.TryGetPlacement(LobbyWidgetId.FriendsOnline4By2, out _));
            LobbyWidgetPreviewElement preview = card.Preview(LobbyWidgetId.FriendsOnline4By2);
            Assert.IsNull(preview.parent);
            Assert.IsFalse(preview.ClassListContains(LIFTED_CLASS));
            Assert.AreEqual(0, root.Q("LayoutDragLayer").childCount);
            Assert.IsNull(card.parent);
            Assert.IsFalse(card.ClassListContains(CARD_DRAGGING_CLASS));
        }

        private LobbyWidgetHandleElement Handle(LobbyWidgetId widgetId) =>
            root.Q<LobbyWidgetHandleElement>(widgetId.ToString()) ?? throw new AssertionException($"{widgetId} has no handle");

        private LobbyWidgetCardElement Card(LobbyWidgetFamily family) =>
            root.Q<LobbyWidgetCardElement>(family.ToString()) ?? throw new AssertionException($"{family} has no row");

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
            unplaced.Add(new VisualElement { name = "Communities" });
            unplaced.Add(new VisualElement { name = "Communities4x2" });
            unplaced.Add(new VisualElement { name = "Gallery" });
            unplaced.Add(new VisualElement { name = "Gallery4x2" });
            unplaced.Add(new VisualElement { name = "GoTo" });

            var modal = new VisualElement { name = "WidgetModal" };
            modal.Add(new ScrollView { name = "WidgetModalItems" });
            modal.Add(new Button { name = "ResetLayout" });
            modal.Add(new Button { name = "CancelLayout" });
            modal.Add(new Button { name = "SaveLayout" });
            lobby.Add(modal);
            lobby.Add(new Button { name = "BackFromLayout" });
            lobby.Add(new VisualElement { name = "LayoutDragLayer" });
            return lobby;
        }
    }
}