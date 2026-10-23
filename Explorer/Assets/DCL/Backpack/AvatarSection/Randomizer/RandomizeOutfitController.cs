using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.Backpack.AvatarSection.Outfits.Commands;
using DCL.Backpack.BackpackBus;
using DCL.Diagnostics;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using Utility;
using Random = System.Random;

namespace DCL.Backpack.AvatarSection.Randomizer
{
    /// <summary>
    ///     Drives the randomize, undo and redo buttons under the backpack avatar preview.
    ///     A roll equips a random outfit from the owned and base wearables, keeping the body shape and colors.
    /// </summary>
    public class RandomizeOutfitController : IDisposable
    {
        private const int OWNED_PAGE_SIZE = 200;
        private const int MAX_REPICK_ROUNDS = 3;
        private const float LOCKED_ALPHA = 0.2f;

        private readonly RandomizeOutfitView view;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IEquippedWearables equippedWearables;
        private readonly IWearablesProvider wearablesProvider;
        private readonly OutfitRandomizer randomizer = new (new Random());
        private readonly List<ITrimmedWearable> ownedWearables = new ();
        private readonly Stack<OutfitSnapshot> undoStack = new ();
        private readonly Stack<OutfitSnapshot> redoStack = new ();

        private CancellationTokenSource? randomizeCts;
        private bool isRolling;
        private bool isApplying;

        private bool isBusy => isRolling || isApplying;

        public RandomizeOutfitController(RandomizeOutfitView view,
            BackpackCommandBus backpackCommandBus,
            IBackpackEventBus backpackEventBus,
            IEquippedWearables equippedWearables,
            IWearablesProvider wearablesProvider)
        {
            this.view = view;
            this.backpackCommandBus = backpackCommandBus;
            this.backpackEventBus = backpackEventBus;
            this.equippedWearables = equippedWearables;
            this.wearablesProvider = wearablesProvider;

            view.UndoButton.onClick.AddListener(Undo);
            view.RandomizeButton.onClick.AddListener(Randomize);
            view.RedoButton.onClick.AddListener(Redo);
            backpackEventBus.EquipOutfitCompletedEvent += OnEquipOutfitCompleted;
            RefreshButtons();
        }

        public void Dispose()
        {
            randomizeCts.SafeCancelAndDispose();
            view.UndoButton.onClick.RemoveListener(Undo);
            view.RandomizeButton.onClick.RemoveListener(Randomize);
            view.RedoButton.onClick.RemoveListener(Redo);
            backpackEventBus.EquipOutfitCompletedEvent -= OnEquipOutfitCompleted;
        }

        /// <summary>
        ///     Forgets the history and the cached owned wearables, so the next session starts clean and sees new purchases.
        /// </summary>
        public void Reset()
        {
            randomizeCts.SafeCancelAndDispose();
            randomizeCts = null;
            isRolling = false;
            isApplying = false;
            undoStack.Clear();
            redoStack.Clear();
            ownedWearables.Clear();
            RefreshButtons();
        }

        private void Randomize()
        {
            randomizeCts = randomizeCts.SafeRestart();
            RandomizeAsync(randomizeCts.Token).Forget();
        }

        private void Undo()
        {
            if (isBusy || undoStack.Count == 0)
            {
                LogHistory("undo ignored");
                return;
            }

            redoStack.Push(CaptureCurrent());
            Apply(undoStack.Pop());
            LogHistory("undo");
            RefreshButtons();
        }

        private void Redo()
        {
            if (isBusy || redoStack.Count == 0)
            {
                LogHistory("redo ignored");
                return;
            }

            undoStack.Push(CaptureCurrent());
            Apply(redoStack.Pop());
            LogHistory("redo");
            RefreshButtons();
        }

        private void OnEquipOutfitCompleted()
        {
            if (!isApplying) return;

            isApplying = false;
            LogHistory("outfit applied");
            RefreshButtons();
        }

        private void LogHistory(string action)
        {
            ReportHub.Log(ReportCategory.BACKPACK,
                $"[RandomizeOutfit] {action}: undo={undoStack.Count} redo={redoStack.Count} rolling={isRolling} applying={isApplying} equipped={CaptureCurrent().Wearables.Count}");
        }

        private async UniTaskVoid RandomizeAsync(CancellationToken ct)
        {
            isRolling = true;
            RefreshButtons();

            try
            {
                if (ownedWearables.Count == 0)
                    await LoadOwnedWearablesAsync(ct);

                if (ct.IsCancellationRequested) return;

                OutfitSnapshot current = CaptureCurrent();
                BodyShape bodyShape = BodyShape.FromStringSafe(current.BodyShape);
                randomizer.BuildCatalog(ownedWearables, bodyShape);

                var outfit = new List<string>();
                await RollAsync(bodyShape, outfit, ct);

                if (ct.IsCancellationRequested) return;

                undoStack.Push(current);
                redoStack.Clear();
                Apply(new OutfitSnapshot(current.BodyShape, outfit, current.EyesColor, current.HairColor, current.SkinColor, Array.Empty<string>()));
                LogHistory("randomize");
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.BACKPACK)); }
            finally
            {
                if (!ct.IsCancellationRequested)
                {
                    isRolling = false;
                    RefreshButtons();
                }
            }
        }

        private async UniTask LoadOwnedWearablesAsync(CancellationToken ct)
        {
            using PooledObject<List<ITrimmedWearable>> _ = ListPool<ITrimmedWearable>.Get(out List<ITrimmedWearable> loaded);
            using PooledObject<List<ITrimmedWearable>> __ = ListPool<ITrimmedWearable>.Get(out List<ITrimmedWearable> page);

            var parameters = new IWearablesProvider.Params(OWNED_PAGE_SIZE, 1);
            int total = int.MaxValue;

            while (loaded.Count < total)
            {
                page.Clear();
                (IReadOnlyList<ITrimmedWearable> results, int totalAmount) = await wearablesProvider.GetTrimmedByParamsAsync(parameters, ct, page);
                total = totalAmount;

                if (results.Count == 0)
                    break;

                loaded.AddRange(results);
                parameters.PageNumber++;
            }

            ownedWearables.AddRange(loaded);
        }

        private async UniTask RollAsync(BodyShape bodyShape, List<string> outfit, CancellationToken ct)
        {
            var picks = new Dictionary<string, URN>();
            randomizer.Pick(picks);

            var wearablesByCategory = new Dictionary<string, IWearable>();
            var hidesByCategory = new Dictionary<string, HashSet<string>>();
            var tried = new HashSet<URN>();
            var dropped = new HashSet<string>();
            var hidden = new HashSet<string>();
            var pending = new List<URN>(picks.Values);

            for (var round = 0; round <= MAX_REPICK_ROUNDS; round++)
            {
                await ResolveAsync(pending, bodyShape, wearablesByCategory, hidesByCategory, ct);

                if (ct.IsCancellationRequested) return;

                foreach (URN urn in pending)
                    tried.Add(urn);

                OutfitRandomizer.ResolveConflicts(hidesByCategory, dropped, hidden);

                pending.Clear();

                foreach (string category in dropped)
                {
                    wearablesByCategory.Remove(category);
                    hidesByCategory.Remove(category);

                    // A core category hidden by a kept pick is covered on the avatar; one dropped for hiding a kept pick is left bare, so it gets another try.
                    if (round < MAX_REPICK_ROUNDS
                        && OutfitRandomizer.IsCoreCategory(category)
                        && !hidden.Contains(category)
                        && randomizer.TryRepick(category, tried, out URN replacement))
                        pending.Add(replacement);
                }

                if (pending.Count == 0)
                    break;
            }

            foreach (IWearable wearable in wearablesByCategory.Values)
                outfit.Add(wearable.GetUrn());
        }

        private async UniTask ResolveAsync(List<URN> urns, BodyShape bodyShape,
            Dictionary<string, IWearable> wearablesByCategory,
            Dictionary<string, HashSet<string>> hidesByCategory,
            CancellationToken ct)
        {
            if (urns.Count == 0)
                return;

            using PooledObject<List<IWearable>> _ = ListPool<IWearable>.Get(out List<IWearable> resolved);

            await wearablesProvider.GetByPointersAsync(urns, bodyShape, ct, resolved);

            foreach (IWearable wearable in resolved)
            {
                string category = wearable.GetCategory();

                if (category == WearableCategories.Categories.BODY_SHAPE)
                    continue;

                var hides = new HashSet<string>();
                wearable.GetHidingList(bodyShape, hides);
                wearablesByCategory[category] = wearable;
                hidesByCategory[category] = hides;
            }
        }

        private OutfitSnapshot CaptureCurrent()
        {
            var wearables = new List<string>();
            string bodyShape = BodyShape.MALE;

            foreach ((string category, IWearable? wearable) in equippedWearables.Items())
            {
                if (wearable == null)
                    continue;

                if (category == WearableCategories.Categories.BODY_SHAPE)
                    bodyShape = wearable.GetUrn();
                else
                    wearables.Add(wearable.GetUrn());
            }

            (Color hair, Color eyes, Color skin) = equippedWearables.GetColors();

            return new OutfitSnapshot(bodyShape, wearables, eyes, hair, skin, new List<string>(equippedWearables.ForceRenderCategories));
        }

        private void Apply(OutfitSnapshot snapshot)
        {
            isApplying = true;

            backpackCommandBus.SendCommand(new BackpackEquipOutfitCommand(
                snapshot.BodyShape,
                snapshot.Wearables,
                snapshot.EyesColor,
                snapshot.HairColor,
                snapshot.SkinColor,
                snapshot.ForceRender,
                useFullUrns: true));
        }

        private void RefreshButtons()
        {
            view.RandomizeButton.interactable = !isBusy;
            SetLocked(view.UndoButton, view.UndoCanvasGroup, isBusy || undoStack.Count == 0);
            SetLocked(view.RedoButton, view.RedoCanvasGroup, isBusy || redoStack.Count == 0);
        }

        private static void SetLocked(Button button, CanvasGroup canvasGroup, bool locked)
        {
            button.interactable = !locked;
            canvasGroup.alpha = locked ? LOCKED_ALPHA : 1f;
        }

        private readonly struct OutfitSnapshot
        {
            public readonly string BodyShape;
            public readonly IReadOnlyCollection<string> Wearables;
            public readonly Color EyesColor;
            public readonly Color HairColor;
            public readonly Color SkinColor;
            public readonly IReadOnlyCollection<string> ForceRender;

            public OutfitSnapshot(string bodyShape, IReadOnlyCollection<string> wearables, Color eyesColor, Color hairColor, Color skinColor, IReadOnlyCollection<string> forceRender)
            {
                BodyShape = bodyShape;
                Wearables = wearables;
                EyesColor = eyesColor;
                HairColor = hairColor;
                SkinColor = skinColor;
                ForceRender = forceRender;
            }
        }
    }
}
