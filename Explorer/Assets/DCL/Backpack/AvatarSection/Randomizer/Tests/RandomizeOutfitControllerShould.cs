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

        private static readonly Color EYES = Color.blue;
        private static readonly Color HAIR = Color.red;
        private static readonly Color SKIN = Color.yellow;

        private readonly Dictionary<URN, IWearable> fullWearables = new ();
        private readonly Dictionary<string, IWearable?> equipped = new ();
        private readonly List<BackpackEquipOutfitCommand> sentCommands = new ();

        private GameObject viewObject = null!;
        private RandomizeOutfitView view = null!;
        private BackpackCommandBus commandBus = null!;
        private IBackpackEventBus eventBus = null!;
        private IWearablesProvider wearablesProvider = null!;
        private RandomizeOutfitController controller = null!;
        private bool completeEquipsImmediately;

        [SetUp]
        public void SetUp()
        {
            fullWearables.Clear();
            equipped.Clear();
            sentCommands.Clear();
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

            IEquippedWearables equippedWearables = Substitute.For<IEquippedWearables>();
            equippedWearables.Items().Returns(equipped);
            equippedWearables.GetColors().Returns((HAIR, EYES, SKIN));
            equippedWearables.ForceRenderCategories.Returns(Array.Empty<string>());

            wearablesProvider = Substitute.For<IWearablesProvider>();
            GivenOwnedWearables(
                RandomizerTestWearables.Trimmed(SHIRT_A, WearableCategories.Categories.UPPER_BODY),
                RandomizerTestWearables.Trimmed(SHIRT_B, WearableCategories.Categories.UPPER_BODY),
                RandomizerTestWearables.Trimmed(PANTS_A, WearableCategories.Categories.LOWER_BODY),
                RandomizerTestWearables.Trimmed(PANTS_B, WearableCategories.Categories.LOWER_BODY));
            GivenPointersResolve();

            IWearableStorage wearableStorage = Substitute.For<IWearableStorage>();

            eventBus = Substitute.For<IBackpackEventBus>();
            commandBus = new BackpackCommandBus();
            commandBus.EquipOutfitMessageReceived += OnEquipOutfitSent;

            controller = new RandomizeOutfitController(view,
                new OutfitRandomizer(new Random(23)),
                commandBus,
                eventBus,
                equippedWearables,
                wearablesProvider,
                new CacheOutfitWearablesCommand(wearablesProvider, wearableStorage),
                wearableStorage,
                Substitute.For<IOwnedNftFilter>());
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
            wearablesProvider.GetByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), Arg.Any<BodyShape>(), Arg.Any<CancellationToken>(), Arg.Any<List<IWearable>?>())
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
        public void KeepTheOutfitWhenTheOwnedWearablesFailToLoad()
        {
            // Arrange
            GivenOwnedWearables();
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
            GivenOwnedWearables(
                RandomizerTestWearables.Trimmed(SHIRT_A, WearableCategories.Categories.UPPER_BODY),
                RandomizerTestWearables.Trimmed(SHIRT_B, WearableCategories.Categories.UPPER_BODY),
                RandomizerTestWearables.Trimmed(PANTS_A, WearableCategories.Categories.LOWER_BODY));
            fullWearables.Remove(new URN(SHIRT_A));
            GivenAvatarReady();

            // Act
            view.RandomizeClicked.Invoke();

            // Assert
            Assert.AreEqual(1, sentCommands.Count);
            CollectionAssert.AreEquivalent(new[] { SHIRT_B, PANTS_A }, sentCommands[0].Wearables);
        }

        private void GivenAvatarReady()
        {
            controller.SetAvatarReady(true);
            eventBus.EquipOutfitCompletedEvent += Raise.Event<Action>();
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

        private void GivenOwnedWearables(params ITrimmedWearable[] owned)
        {
            wearablesProvider.GetTrimmedByParamsAsync(Arg.Any<IWearablesProvider.Params>(), Arg.Any<CancellationToken>(),
                                  Arg.Any<List<ITrimmedWearable>?>(), Arg.Any<CommonLoadingArguments?>(), Arg.Any<bool>())
                             .Returns(call =>
                              {
                                  (IReadOnlyList<ITrimmedWearable> results, int totalAmount) page = call.ArgAt<IWearablesProvider.Params>(0).PageNumber == 1
                                      ? (owned, owned.Length)
                                      : (Array.Empty<ITrimmedWearable>(), 0);

                                  return UniTask.FromResult(page);
                              });
        }

        private void GivenPointersResolve(UniTaskCompletionSource? gate = null)
        {
            wearablesProvider.GetByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), Arg.Any<BodyShape>(), Arg.Any<CancellationToken>(), Arg.Any<List<IWearable>?>())
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
            randomizeView.RandomizeButton = CreateButton("Randomize", out _);
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
    }
}
