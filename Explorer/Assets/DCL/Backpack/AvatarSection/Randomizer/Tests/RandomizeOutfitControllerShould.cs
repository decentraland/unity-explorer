using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits.Commands;
using DCL.Backpack.AvatarSection.Randomizer;
using DCL.Backpack.BackpackBus;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace DCL.Backpack.Tests
{
    [TestFixture]
    public class RandomizeOutfitControllerShould
    {
        private const string SHIRT_A = "urn:decentraland:off-chain:base-avatars:shirt_a";
        private const string SHIRT_B = "urn:decentraland:off-chain:base-avatars:shirt_b";
        private const string PANTS_A = "urn:decentraland:off-chain:base-avatars:pants_a";
        private const string PANTS_B = "urn:decentraland:off-chain:base-avatars:pants_b";
        private const string STARTING_SHIRT = "urn:decentraland:off-chain:base-avatars:starting_shirt";
        private const string OWNED_PREFIX = "urn:decentraland:matic:collections-v2:0xabc:";
        private const int OWNED_PAGE_SIZE = 30;

        private static readonly Color EYES = Color.blue;
        private static readonly Color HAIR = Color.red;
        private static readonly Color SKIN = Color.yellow;

        private readonly Dictionary<URN, IWearable> fullWearables = new ();
        private readonly Dictionary<string, IWearable?> equipped = new ();
        private readonly List<BackpackEquipOutfitCommand> sentCommands = new ();
        private readonly List<ITrimmedWearable> baseInventory = new ();
        private readonly List<ITrimmedWearable> ownedInventory = new ();
        private readonly List<ServedRequest> requests = new ();

        private GameObject viewObject = null!;
        private RandomizeOutfitView view = null!;
        private BackpackCommandBus commandBus = null!;
        private IBackpackEventBus eventBus = null!;
        private IEquippedWearables equippedWearables = null!;
        private IWearablesProvider wearablesProvider = null!;
        private IWearableStorage wearableStorage = null!;
        private RandomizeOutfitController controller = null!;
        private bool completeEquipsImmediately;

        [SetUp]
        public void SetUp()
        {
            fullWearables.Clear();
            equipped.Clear();
            sentCommands.Clear();
            baseInventory.Clear();
            ownedInventory.Clear();
            requests.Clear();
            completeEquipsImmediately = true;

            foreach ((string urn, string category) in new[]
                     {
                         (SHIRT_A, WearableCategories.Categories.UPPER_BODY), (SHIRT_B, WearableCategories.Categories.UPPER_BODY),
                         (PANTS_A, WearableCategories.Categories.LOWER_BODY), (PANTS_B, WearableCategories.Categories.LOWER_BODY),
                         (STARTING_SHIRT, WearableCategories.Categories.UPPER_BODY), (BodyShape.MALE, WearableCategories.Categories.BODY_SHAPE),
                         (BodyShape.FEMALE, WearableCategories.Categories.BODY_SHAPE),
                     })
                fullWearables[new URN(urn)] = RandomizerTestWearables.Full(urn, category);

            equipped[WearableCategories.Categories.BODY_SHAPE] = fullWearables[new URN(BodyShape.MALE)];
            equipped[WearableCategories.Categories.UPPER_BODY] = fullWearables[new URN(STARTING_SHIRT)];

            view = CreateView();

            equippedWearables = Substitute.For<IEquippedWearables>();
            equippedWearables.Items().Returns(equipped);
            equippedWearables.GetColors().Returns((HAIR, EYES, SKIN));
            equippedWearables.ForceRenderCategories.Returns(Array.Empty<string>());

            wearablesProvider = Substitute.For<IWearablesProvider>();
            wearablesProvider.GetTrimmedByParamsAsync(Arg.Any<IWearablesProvider.Params>(), Arg.Any<CancellationToken>(),
                                  Arg.Any<List<ITrimmedWearable>?>(), Arg.Any<CommonLoadingArguments?>(), Arg.Any<bool>())
                             .Returns(call => Serve(call.ArgAt<IWearablesProvider.Params>(0), call.ArgAt<List<ITrimmedWearable>?>(2)));

            baseInventory.Add(RandomizerTestWearables.Trimmed(SHIRT_A, WearableCategories.Categories.UPPER_BODY));
            baseInventory.Add(RandomizerTestWearables.Trimmed(SHIRT_B, WearableCategories.Categories.UPPER_BODY));
            baseInventory.Add(RandomizerTestWearables.Trimmed(PANTS_A, WearableCategories.Categories.LOWER_BODY));
            baseInventory.Add(RandomizerTestWearables.Trimmed(PANTS_B, WearableCategories.Categories.LOWER_BODY));
            GivenPointersResolve();

            wearableStorage = Substitute.For<IWearableStorage>();

            eventBus = Substitute.For<IBackpackEventBus>();
            commandBus = new BackpackCommandBus();
            commandBus.EquipOutfitMessageReceived += OnEquipOutfitSent;

            controller = CreateController(new Random(23));
        }

        [TearDown]
        public void TearDown()
        {
            controller.Dispose();
            commandBus.EquipOutfitMessageReceived -= OnEquipOutfitSent;
            Object.DestroyImmediate(viewObject);
        }

        [Test]
        public void KeepRandomizeLockedUntilTheAvatarIsReady()
        {
            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.IsFalse(view.RandomizeButton.interactable);
            Assert.IsEmpty(sentCommands);
        }

        [Test]
        public void KeepRandomizeLockedUntilTheProfileOutfitIsEquipped()
        {
            // Act
            controller.SetAvatarReady(true);

            // Assert
            Assert.IsFalse(view.RandomizeButton.interactable);

            // Act
            eventBus.EquipOutfitCompletedEvent += Raise.Event<Action>();

            // Assert
            Assert.IsTrue(view.RandomizeButton.interactable);
        }

        [Test]
        public void EquipARandomOutfitKeepingBodyShapeAndColors()
        {
            // Arrange
            GivenAvatarReady();

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.AreEqual(1, sentCommands.Count);
            BackpackEquipOutfitCommand command = sentCommands[0];
            Assert.AreEqual((string)BodyShape.MALE, command.BodyShape);
            Assert.AreEqual(EYES, command.EyesColor);
            Assert.AreEqual(HAIR, command.HairColor);
            Assert.AreEqual(SKIN, command.SkinColor);
            Assert.AreEqual(2, command.Wearables.Count);
            CollectionAssert.DoesNotContain(command.Wearables, STARTING_SHIRT);
        }

        [Test]
        public void UndoBackToTheOutfitBeforeTheFirstRoll()
        {
            // Arrange
            GivenAvatarReady();
            view.RandomizeClicked.Invoke();
            view.RandomizeClicked.Invoke();
            view.RandomizeClicked.Invoke();

            // Act
            view.UndoClicked.Invoke();
            view.UndoClicked.Invoke();
            view.UndoClicked.Invoke();

            // Assert
            CollectionAssert.AreEquivalent(new[] { STARTING_SHIRT }, sentCommands[^1].Wearables);
            Assert.IsFalse(view.UndoButton.interactable);
            Assert.AreEqual(0.2f, view.UndoCanvasGroup.alpha, 0.001f);
            Assert.IsTrue(view.RedoButton.interactable);
        }

        [Test]
        public void RedoUpToTheLastRolledOutfit()
        {
            // Arrange
            GivenAvatarReady();
            view.RandomizeClicked.Invoke();
            view.RandomizeClicked.Invoke();
            IReadOnlyCollection<string?> lastRoll = sentCommands[^1].Wearables;
            view.UndoClicked.Invoke();
            view.UndoClicked.Invoke();

            // Act
            view.RedoClicked.Invoke();
            view.RedoClicked.Invoke();

            // Assert
            CollectionAssert.AreEquivalent(lastRoll, sentCommands[^1].Wearables);
            Assert.IsFalse(view.RedoButton.interactable);
            Assert.IsTrue(view.UndoButton.interactable);
        }

        [Test]
        public void ClearRedoOnANewRoll()
        {
            // Arrange
            GivenAvatarReady();
            view.RandomizeClicked.Invoke();
            view.UndoClicked.Invoke();

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.IsFalse(view.RedoButton.interactable);
            Assert.AreEqual(0.2f, view.RedoCanvasGroup.alpha, 0.001f);
        }

        [Test]
        public void LockAllButtonsUntilTheOutfitIsApplied()
        {
            // Arrange
            GivenAvatarReady();
            completeEquipsImmediately = false;

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.IsFalse(view.RandomizeButton.interactable);
            Assert.IsFalse(view.UndoButton.interactable);

            // Act
            eventBus.EquipOutfitCompletedEvent += Raise.Event<Action>();

            // Assert
            Assert.IsTrue(view.RandomizeButton.interactable);
            Assert.IsTrue(view.UndoButton.interactable);
        }

        [Test]
        public void LockAllButtonsWhileAnotherSectionIsShown()
        {
            // Arrange
            GivenAvatarReady();
            view.RandomizeClicked.Invoke();

            // Act
            controller.SetSectionShown(false);
            view.UndoClicked.Invoke();

            // Assert
            Assert.AreEqual(1, sentCommands.Count);
            Assert.IsFalse(view.RandomizeButton.interactable);
            Assert.AreEqual(0.2f, view.RandomizeIconCanvasGroup.alpha, 0.001f);
            Assert.AreEqual(0.2f, view.RandomizeLabelCanvasGroup.alpha, 0.001f);
            Assert.IsFalse(view.UndoButton.interactable);

            // Act
            controller.SetSectionShown(true);

            // Assert
            Assert.IsTrue(view.RandomizeButton.interactable);
            Assert.IsTrue(view.UndoButton.interactable);
        }

        [Test]
        public void ForgetTheHistoryOnReset()
        {
            // Arrange
            GivenAvatarReady();
            view.RandomizeClicked.Invoke();

            // Act
            controller.Reset();

            // Assert
            Assert.IsFalse(view.UndoButton.interactable);
            Assert.IsFalse(view.RandomizeButton.interactable);
        }

        [Test]
        public void KeepTheOutfitWhenNothingResolves()
        {
            // Arrange
            wearablesProvider.GetDTOsByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), Arg.Any<BodyShape>(), Arg.Any<CancellationToken>(), Arg.Any<List<IWearable>?>())
                             .Returns(UniTask.FromResult<IReadOnlyCollection<IWearable>?>(null));
            GivenAvatarReady();

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.IsEmpty(sentCommands);
            Assert.IsFalse(view.UndoButton.interactable);
            Assert.IsTrue(view.RandomizeButton.interactable);
        }

        [Test]
        public void KeepTheOutfitWhenTheBaseWearablesFailToLoad()
        {
            // Arrange
            baseInventory.Clear();
            GivenAvatarReady();

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.IsEmpty(sentCommands);
            Assert.IsTrue(view.RandomizeButton.interactable);
        }

        [Test]
        public void DropARollWhenTheBodyShapeChangesWhileItLoads()
        {
            // Arrange
            GivenAvatarReady();
            var pointersGate = new UniTaskCompletionSource();
            GivenPointersResolve(pointersGate);
            view.RandomizeClicked.Invoke();

            // Act
            equipped[WearableCategories.Categories.BODY_SHAPE] = fullWearables[new URN(BodyShape.FEMALE)];
            pointersGate.TrySetResult();

            // Assert
            Assert.IsEmpty(sentCommands);
            Assert.IsFalse(view.UndoButton.interactable);
            Assert.IsTrue(view.RandomizeButton.interactable);
        }

        [Test]
        public void RepickACorePickThatFailsToLoad()
        {
            // Arrange
            baseInventory.RemoveAt(3);
            fullWearables.Remove(new URN(SHIRT_A));
            GivenAvatarReady();

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.AreEqual(1, sentCommands.Count);
            CollectionAssert.AreEquivalent(new[] { SHIRT_B, PANTS_A }, sentCommands[0].Wearables);
        }

        [Test]
        public void FetchTheBaseWearablesOncePerSession()
        {
            // Arrange
            GivenAvatarReady();

            // Act
            view.RandomizeClicked.Invoke();
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.AreEqual(1, CountRequests(r => r.IsBase));

            // Act
            controller.Reset();
            GivenAvatarReady();
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.AreEqual(2, CountRequests(r => r.IsBase));
        }

        [Test]
        public void CacheTheOwnedTotalsForTheSession()
        {
            // Arrange
            GivenAvatarReady();

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.AreEqual(OutfitRandomizer.CATEGORIES.Count, CountRequests(r => r.IsTotal));

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.AreEqual(OutfitRandomizer.CATEGORIES.Count, CountRequests(r => r.IsTotal));
        }

        [Test]
        public void PickTheOwnedWearableAtTheRandomIndexOnAShortLastPage()
        {
            // Arrange
            string[] owned = GivenOwned(WearableCategories.Categories.UPPER_BODY, 45);
            UseRandom(new ScriptedRandom(37));
            GivenAvatarReady();

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            ServedRequest page = SingleRequest(r => r.IsPage && r.Category == WearableCategories.Categories.UPPER_BODY);
            Assert.AreEqual(2, page.PageNumber);
            Assert.AreEqual(OWNED_PAGE_SIZE, page.PageSize);
            CollectionAssert.Contains(sentCommands[0].Wearables, owned[37]);
        }

        [Test]
        public void RepickFromTheFetchedPageWithoutANewRequest()
        {
            // Arrange
            const string HIDING_PANTS = OWNED_PREFIX + "hiding_pants";
            const string PLAIN_PANTS = OWNED_PREFIX + "plain_pants";
            GivenOwned(HIDING_PANTS, WearableCategories.Categories.LOWER_BODY, WearableCategories.Categories.UPPER_BODY);
            GivenOwned(PLAIN_PANTS, WearableCategories.Categories.LOWER_BODY);
            UseRandom(new ScriptedRandom(0));
            GivenAvatarReady();

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.AreEqual(1, CountRequests(r => r.IsPage && r.Category == WearableCategories.Categories.LOWER_BODY));
            CollectionAssert.Contains(sentCommands[0].Wearables, PLAIN_PANTS);
            CollectionAssert.DoesNotContain(sentCommands[0].Wearables, HIDING_PANTS);
        }

        [Test]
        public void FallBackToBaseWhenThePageHasNothingUsable()
        {
            // Arrange
            GivenOwned(WearableCategories.Categories.UPPER_BODY, 3, isSmart: true);
            UseRandom(new ScriptedRandom(0));
            GivenAvatarReady();

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.AreEqual(1, CountRequests(r => r.IsPage && r.Category == WearableCategories.Categories.UPPER_BODY));
            CollectionAssert.Contains(sentCommands[0].Wearables, SHIRT_A);
            Assert.AreEqual(0, CountRequests(r => r.IsPage && r.Category == WearableCategories.Categories.LOWER_BODY));
        }

        [Test]
        public void FetchTheChosenOptionalCategoriesInOneRequest()
        {
            // Arrange
            GivenOwned(WearableCategories.Categories.HAT, 3);
            GivenOwned(WearableCategories.Categories.MASK, 2);
            UseRandom(new ScriptedRandom(0));
            GivenAvatarReady();

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            ServedRequest combined = SingleRequest(r => r.Categories.Length > 0);
            CollectionAssert.AreEquivalent(new[] { WearableCategories.Categories.HAT, WearableCategories.Categories.MASK }, combined.Categories);
            Assert.AreEqual(0, CountRequests(r => r.IsPage && (r.Category == WearableCategories.Categories.HAT || r.Category == WearableCategories.Categories.MASK)));
        }

        [Test]
        public void SkipTheOptionalRequestWhenTheOddsSkipEveryOptionalCategory()
        {
            // Arrange
            GivenOwned(WearableCategories.Categories.HAT, 3);
            UseRandom(new ScriptedRandom(0, nextDouble: 0.9));
            GivenAvatarReady();

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.AreEqual(1, sentCommands.Count);
            Assert.AreEqual(0, CountRequests(r => r.Categories.Length > 0));
        }

        [Test]
        public void KeepTheRequestCountIndependentOfTheInventorySize()
        {
            // Act
            int smallInventoryRequests = RequestsOnASecondRoll(100);
            int largeInventoryRequests = RequestsOnASecondRoll(10000);

            // Assert
            Assert.Greater(smallInventoryRequests, 0);
            Assert.AreEqual(smallInventoryRequests, largeInventoryRequests);
        }

        [Test]
        public void ResolveOnlyTheDTOsOfTheRolledWearables()
        {
            // Arrange
            GivenAvatarReady();

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.AreEqual(1, sentCommands.Count);
            _ = wearablesProvider.Received().GetDTOsByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), Arg.Any<BodyShape>(), Arg.Any<CancellationToken>(), Arg.Any<List<IWearable>?>());
            _ = wearablesProvider.DidNotReceive().GetByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), Arg.Any<BodyShape>(), Arg.Any<CancellationToken>(), Arg.Any<List<IWearable>?>());
        }

        private RandomizeOutfitController CreateController(Random random) =>
            new (view,
                new OutfitRandomizer(random),
                commandBus,
                eventBus,
                equippedWearables,
                wearablesProvider,
                new CacheOutfitWearablesCommand(wearablesProvider, wearableStorage),
                wearableStorage,
                Substitute.For<IOwnedNftFilter>());

        private void UseRandom(Random random)
        {
            controller.Dispose();
            controller = CreateController(random);
        }

        private int RequestsOnASecondRoll(int ownedUpperBodies)
        {
            ownedInventory.Clear();
            GivenOwned(WearableCategories.Categories.UPPER_BODY, ownedUpperBodies);
            UseRandom(new ScriptedRandom(0));
            GivenAvatarReady();
            view.RandomizeClicked.Invoke();
            requests.Clear();

            view.RandomizeClicked.Invoke();
            return requests.Count;
        }

        private void GivenAvatarReady()
        {
            controller.SetAvatarReady(true);
            eventBus.EquipOutfitCompletedEvent += Raise.Event<Action>();
        }

        private string[] GivenOwned(string category, int count, bool isSmart = false)
        {
            var urns = new string[count];

            for (var i = 0; i < count; i++)
            {
                urns[i] = $"{OWNED_PREFIX}{category}_{i}";
                GivenOwned(urns[i], category, isSmart);
            }

            return urns;
        }

        private void GivenOwned(string urn, string category, params string[] hides) =>
            GivenOwned(urn, category, false, hides);

        private void GivenOwned(string urn, string category, bool isSmart, params string[] hides)
        {
            ownedInventory.Add(RandomizerTestWearables.Trimmed(urn, category, isSmart));
            fullWearables[new URN(urn)] = RandomizerTestWearables.Full(urn, category, hides);
        }

        private UniTask<(IReadOnlyList<ITrimmedWearable> results, int totalAmount)> Serve(IWearablesProvider.Params parameters, List<ITrimmedWearable>? results)
        {
            var request = new ServedRequest(parameters);
            requests.Add(request);
            results ??= new List<ITrimmedWearable>();

            var matching = new List<ITrimmedWearable>();

            if (request.IsBase)
                matching.AddRange(baseInventory);
            else
                foreach (ITrimmedWearable wearable in ownedInventory)
                {
                    string category = wearable.GetCategory();

                    if (category == parameters.Category || Array.IndexOf(request.Categories, category) >= 0)
                        matching.Add(wearable);
                }

            int start = (parameters.PageNumber - 1) * parameters.PageSize;

            for (int i = start; i < matching.Count && i < start + parameters.PageSize; i++)
                results.Add(matching[i]);

            return UniTask.FromResult<(IReadOnlyList<ITrimmedWearable>, int)>((results, matching.Count));
        }

        private int CountRequests(Predicate<ServedRequest> match)
        {
            var count = 0;

            foreach (ServedRequest request in requests)
                if (match(request))
                    count++;

            return count;
        }

        private ServedRequest SingleRequest(Predicate<ServedRequest> match)
        {
            Assert.AreEqual(1, CountRequests(match));
            return requests.Find(match);
        }

        private void OnEquipOutfitSent(BackpackEquipOutfitCommand command)
        {
            sentCommands.Add(command);
            ApplyToEquipped(command);

            if (completeEquipsImmediately)
                eventBus.EquipOutfitCompletedEvent += Raise.Event<Action>();
        }

        private void ApplyToEquipped(BackpackEquipOutfitCommand command)
        {
            IWearable? bodyShape = equipped[WearableCategories.Categories.BODY_SHAPE];
            equipped.Clear();
            equipped[WearableCategories.Categories.BODY_SHAPE] = bodyShape;

            foreach (string? urn in command.Wearables)
            {
                if (urn == null) continue;

                IWearable wearable = fullWearables[new URN(urn)];
                equipped[wearable.GetCategory()] = wearable;
            }
        }

        private void GivenPointersResolve(UniTaskCompletionSource? gate = null)
        {
            wearablesProvider.GetDTOsByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), Arg.Any<BodyShape>(), Arg.Any<CancellationToken>(), Arg.Any<List<IWearable>?>())
                             .Returns(call => ResolvePointersAsync(call.ArgAt<IReadOnlyCollection<URN>>(0), call.ArgAt<List<IWearable>?>(3), gate));
        }

        private async UniTask<IReadOnlyCollection<IWearable>?> ResolvePointersAsync(IReadOnlyCollection<URN> pointers, List<IWearable>? results, UniTaskCompletionSource? gate)
        {
            if (gate != null)
                await gate.Task;

            results ??= new List<IWearable>();

            foreach (URN pointer in pointers)
                if (fullWearables.TryGetValue(pointer, out IWearable wearable))
                    results.Add(wearable);

            return results;
        }

        private RandomizeOutfitView CreateView()
        {
            viewObject = new GameObject(nameof(RandomizeOutfitControllerShould));
            RandomizeOutfitView randomizeView = viewObject.AddComponent<RandomizeOutfitView>();
            randomizeView.UndoButton = CreateButton("Undo", out CanvasGroup undoGroup);
            randomizeView.UndoCanvasGroup = undoGroup;
            randomizeView.RandomizeButton = CreateButton("Randomize", out CanvasGroup randomizeIconGroup);
            randomizeView.RandomizeIconCanvasGroup = randomizeIconGroup;
            CreateButton("RandomizeLabel", out CanvasGroup randomizeLabelGroup);
            randomizeView.RandomizeLabelCanvasGroup = randomizeLabelGroup;
            randomizeView.RedoButton = CreateButton("Redo", out CanvasGroup redoGroup);
            randomizeView.RedoCanvasGroup = redoGroup;
            return randomizeView;
        }

        private Button CreateButton(string name, out CanvasGroup canvasGroup)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(viewObject.transform, false);
            canvasGroup = buttonObject.AddComponent<CanvasGroup>();
            return buttonObject.AddComponent<Button>();
        }

        /// <summary>
        ///     A copy of the request parameters, taken when served because the controller pools the categories list.
        /// </summary>
        private readonly struct ServedRequest
        {
            public readonly string? Category;
            public readonly string[] Categories;
            public readonly int PageSize;
            public readonly int PageNumber;
            private readonly IWearablesProvider.CollectionType collectionType;

            public ServedRequest(IWearablesProvider.Params parameters)
            {
                Category = parameters.Category;
                Categories = parameters.Categories == null ? Array.Empty<string>() : new List<string>(parameters.Categories).ToArray();
                PageSize = parameters.PageSize;
                PageNumber = parameters.PageNumber;
                collectionType = parameters.CollectionType;
            }

            public bool IsBase => collectionType == IWearablesProvider.CollectionType.Base;
            public bool IsTotal => !IsBase && PageSize == 1;
            public bool IsPage => !IsBase && PageSize > 1;
        }
    }
}
