using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits.Commands;
using DCL.Backpack.BackpackBus;
using DCL.Diagnostics;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.Backpack.AvatarSection.Randomizer
{
    /// <summary>
    ///     Drives the randomize, undo and redo buttons under the backpack avatar preview.
    ///     A roll equips a random outfit from the owned and base wearables, keeping the body shape and colors.
    /// </summary>
    public class RandomizeOutfitController : IDisposable
    {
        /// <summary>
        ///     How many times a roll retries the core categories left bare by a conflict or a failed load before giving up on them.
        /// </summary>
        private const int MAX_REPICK_ROUNDS = 3;

        /// <summary>
        ///     Size of the owned wearables page a roll fetches per category; the rest of the page is the category's repick pool.
        /// </summary>
        private const int OWNED_PAGE_SIZE = 30;

        /// <summary>
        ///     Large enough for every base wearable to arrive in one page.
        /// </summary>
        private const int BASE_PAGE_SIZE = 1000;

        private const IWearablesProvider.CollectionType OWNED_COLLECTIONS = IWearablesProvider.CollectionType.OnChain | IWearablesProvider.CollectionType.ThirdParty;

        private readonly RandomizeOutfitView view;
        private readonly OutfitRandomizer randomizer;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IEquippedWearables equippedWearables;
        private readonly IWearablesProvider wearablesProvider;
        private readonly CacheOutfitWearablesCommand cacheOutfitWearablesCommand;
        private readonly Func<URN, bool> isPendingGift;
        private readonly List<ITrimmedWearable> baseWearables = new ();
        private readonly Dictionary<string, int> ownedTotals = new ();
        private readonly Stack<BackpackEquipOutfitCommand> undoStack = new ();
        private readonly Stack<BackpackEquipOutfitCommand> redoStack = new ();

        private CancellationTokenSource? randomizeCts;
        private bool isAvatarReady;
        private bool isSectionShown = true;
        private bool isRolling;
        private bool isApplying;

        private bool isBusy => isRolling || isApplying || !isSectionShown;

        public RandomizeOutfitController(RandomizeOutfitView view,
            OutfitRandomizer randomizer,
            BackpackCommandBus backpackCommandBus,
            IBackpackEventBus backpackEventBus,
            IEquippedWearables equippedWearables,
            IWearablesProvider wearablesProvider,
            CacheOutfitWearablesCommand cacheOutfitWearablesCommand,
            IWearableStorage wearableStorage,
            IOwnedNftFilter ownedNftFilter)
        {
            this.view = view;
            this.randomizer = randomizer;
            this.backpackCommandBus = backpackCommandBus;
            this.backpackEventBus = backpackEventBus;
            this.equippedWearables = equippedWearables;
            this.wearablesProvider = wearablesProvider;
            this.cacheOutfitWearablesCommand = cacheOutfitWearablesCommand;
            isPendingGift = urn => ownedNftFilter.IsFullyPending(wearableStorage, urn);

            view.UndoClicked.AddListener(Undo);
            view.RandomizeClicked.AddListener(Randomize);
            view.RedoClicked.AddListener(Redo);
            backpackEventBus.EquipOutfitCompletedEvent += OnEquipOutfitCompleted;
            RefreshButtons();
        }

        public void Dispose()
        {
            randomizeCts.SafeCancelAndDispose();
            view.UndoClicked.RemoveListener(Undo);
            view.RandomizeClicked.RemoveListener(Randomize);
            view.RedoClicked.RemoveListener(Redo);
            backpackEventBus.EquipOutfitCompletedEvent -= OnEquipOutfitCompleted;
        }

        /// <summary>
        ///     Unlocks Randomize, keeping every button held until the next <see cref="IBackpackEventBus.EquipOutfitCompletedEvent" />.
        ///     Call it right before sending the profile's own equip command, so the first undo step is the complete starting outfit.
        /// </summary>
        public void SetAvatarReady(bool ready)
        {
            isAvatarReady = ready;
            isApplying = ready;
            RefreshButtons();
        }

        /// <summary>
        ///     Locks every button while another backpack section is shown, keeping the bar on screen.
        /// </summary>
        public void SetSectionShown(bool shown)
        {
            isSectionShown = shown;
            RefreshButtons();
        }

        /// <summary>
        ///     Forgets the history, the base wearables and the owned totals, so the next session starts clean and sees new purchases.
        ///     Randomize locks again until the next session reports its avatar ready.
        /// </summary>
        public void Reset()
        {
            randomizeCts.SafeCancelAndDispose();
            randomizeCts = null;
            isAvatarReady = false;
            isRolling = false;
            isApplying = false;
            undoStack.Clear();
            redoStack.Clear();
            baseWearables.Clear();
            ownedTotals.Clear();
            RefreshButtons();
        }

        private void Randomize()
        {
            if (isBusy || !isAvatarReady) return;

            randomizeCts = randomizeCts.SafeRestart();
            RandomizeAsync(randomizeCts.Token).Forget();
        }

        private void Undo()
        {
            if (isBusy || undoStack.Count == 0) return;

            redoStack.Push(CaptureCurrent());
            Apply(undoStack.Pop());
            RefreshButtons();
        }

        private void Redo()
        {
            if (isBusy || redoStack.Count == 0) return;

            undoStack.Push(CaptureCurrent());
            Apply(redoStack.Pop());
            RefreshButtons();
        }

        private void OnEquipOutfitCompleted()
        {
            if (!isApplying) return;

            isApplying = false;
            RefreshButtons();
        }

        private async UniTaskVoid RandomizeAsync(CancellationToken ct)
        {
            isRolling = true;
            RefreshButtons();

            try
            {
                if (baseWearables.Count == 0 && !await TryLoadBaseWearablesAsync(ct))
                    return;

                if (ownedTotals.Count == 0)
                    await LoadOwnedTotalsAsync(ct);

                if (ct.IsCancellationRequested) return;

                BackpackEquipOutfitCommand current = CaptureCurrent();
                BodyShape bodyShape = BodyShape.FromStringSafe(current.BodyShape);
                randomizer.BuildCatalog(baseWearables, bodyShape, isPendingGift);

                using PooledObject<HashSet<string>> _ = HashSetPool<string>.Get(out HashSet<string> chosen);
                randomizer.ChooseCategories(ownedTotals, chosen);
                await LoadOwnedPagesAsync(chosen, ct);

                if (ct.IsCancellationRequested) return;

                var outfit = new List<string>();
                await RollAsync(bodyShape, chosen, outfit, ct);

                if (ct.IsCancellationRequested) return;

                // The rest of the backpack stays usable while the roll loads, so the outfit is captured again to keep edits made meanwhile
                BackpackEquipOutfitCommand latest = CaptureCurrent();

                // The picks were filtered and resolved for the old body shape
                if (!string.Equals(latest.BodyShape, current.BodyShape, StringComparison.OrdinalIgnoreCase))
                    return;

                if (outfit.Count == 0)
                {
                    ReportHub.LogWarning(ReportCategory.BACKPACK, "Randomize resolved no wearables, the current outfit is kept");
                    return;
                }

                undoStack.Push(latest);
                redoStack.Clear();
                Apply(new BackpackEquipOutfitCommand(latest.BodyShape, outfit, latest.EyesColor, latest.HairColor, latest.SkinColor, Array.Empty<string>(), useFullUrns: true));
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

        private async UniTask<bool> TryLoadBaseWearablesAsync(CancellationToken ct)
        {
            var parameters = new IWearablesProvider.Params(BASE_PAGE_SIZE, 1) { CollectionType = IWearablesProvider.CollectionType.Base };
            await wearablesProvider.GetTrimmedByParamsAsync(parameters, ct, baseWearables);

            if (baseWearables.Count > 0)
                return true;

            ReportHub.LogWarning(ReportCategory.BACKPACK, "Base wearables failed to load, the outfit is kept");
            return false;
        }

        /// <summary>
        ///     One single-item request per category, read only for the total it reports.
        /// </summary>
        private async UniTask LoadOwnedTotalsAsync(CancellationToken ct)
        {
            IReadOnlyList<string> categories = OutfitRandomizer.CATEGORIES;
            var requests = new UniTask<(IReadOnlyList<ITrimmedWearable> results, int totalAmount)>[categories.Count];

            for (var i = 0; i < categories.Count; i++)
                requests[i] = wearablesProvider.GetTrimmedByParamsAsync(OwnedPage(categories[i], 1, 1), ct);

            (IReadOnlyList<ITrimmedWearable> results, int totalAmount)[] totals = await UniTask.WhenAll(requests);

            for (var i = 0; i < categories.Count; i++)
                ownedTotals[categories[i]] = totals[i].totalAmount;
        }

        /// <summary>
        ///     Fetches, for each chosen category with owned wearables, the page holding a random owned index, so the request count does not grow with the inventory.
        ///     The optional categories share one request.
        /// </summary>
        private async UniTask LoadOwnedPagesAsync(HashSet<string> chosen, CancellationToken ct)
        {
            using PooledObject<List<UniTask>> a = ListPool<UniTask>.Get(out List<UniTask> requests);
            using PooledObject<List<string>> b = ListPool<string>.Get(out List<string> optional);
            var optionalTotal = 0;

            foreach (string category in chosen)
            {
                if (!ownedTotals.TryGetValue(category, out int total) || total == 0)
                    continue;

                if (OutfitRandomizer.IsCoreCategory(category))
                    requests.Add(LoadOwnedPageAsync(category, randomizer.PickIndex(total), ct));
                else
                {
                    optional.Add(category);
                    optionalTotal += total;
                }
            }

            if (optional.Count > 0)
                requests.Add(LoadOptionalPageAsync(optional, randomizer.PickIndex(optionalTotal), ct));

            await UniTask.WhenAll(requests);
        }

        private async UniTask LoadOwnedPageAsync(string category, int index, CancellationToken ct)
        {
            using PooledObject<List<ITrimmedWearable>> _ = ListPool<ITrimmedWearable>.Get(out List<ITrimmedWearable> page);

            await wearablesProvider.GetTrimmedByParamsAsync(OwnedPage(category, OWNED_PAGE_SIZE, (index / OWNED_PAGE_SIZE) + 1), ct, page);
            randomizer.AddCustomPage(category, page, index % OWNED_PAGE_SIZE);
        }

        /// <summary>
        ///     A category missing from the page is left to the base wearables.
        /// </summary>
        private async UniTask LoadOptionalPageAsync(List<string> categories, int index, CancellationToken ct)
        {
            using PooledObject<List<ITrimmedWearable>> _ = ListPool<ITrimmedWearable>.Get(out List<ITrimmedWearable> page);

            IWearablesProvider.Params parameters = OwnedPage(null, OWNED_PAGE_SIZE, (index / OWNED_PAGE_SIZE) + 1);
            parameters.Categories = categories;
            await wearablesProvider.GetTrimmedByParamsAsync(parameters, ct, page);

            foreach (string category in categories)
                randomizer.AddCustomPage(category, page, index % OWNED_PAGE_SIZE);
        }

        private static IWearablesProvider.Params OwnedPage(string? category, int pageSize, int pageNumber) =>
            new (pageSize, pageNumber) { CollectionType = OWNED_COLLECTIONS, Category = category };

        private async UniTask RollAsync(BodyShape bodyShape, IReadOnlyCollection<string> chosen, List<string> outfit, CancellationToken ct)
        {
            using PooledObject<Dictionary<string, URN>> a = DictionaryPool<string, URN>.Get(out Dictionary<string, URN> picks);
            using PooledObject<Dictionary<string, IWearable>> b = DictionaryPool<string, IWearable>.Get(out Dictionary<string, IWearable> wearablesByCategory);
            using PooledObject<Dictionary<string, HashSet<string>>> c = DictionaryPool<string, HashSet<string>>.Get(out Dictionary<string, HashSet<string>> hidesByCategory);
            using PooledObject<HashSet<URN>> d = HashSetPool<URN>.Get(out HashSet<URN> tried);
            using PooledObject<HashSet<string>> e = HashSetPool<string>.Get(out HashSet<string> dropped);
            using PooledObject<HashSet<string>> f = HashSetPool<string>.Get(out HashSet<string> hidden);
            using PooledObject<List<URN>> g = ListPool<URN>.Get(out List<URN> pending);

            try
            {
                randomizer.Pick(chosen, picks);
                pending.AddRange(picks.Values);

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

                        if (hidesByCategory.Remove(category, out HashSet<string>? hides))
                            HashSetPool<string>.Release(hides);

                        // A core category hidden by a kept pick is covered on the avatar; one dropped for hiding a kept pick is left bare, so it gets another try.
                        if (round < MAX_REPICK_ROUNDS
                            && OutfitRandomizer.IsCoreCategory(category)
                            && !hidden.Contains(category)
                            && randomizer.TryRepick(category, tried, out URN replacement))
                            pending.Add(replacement);
                    }

                    // A core pick that failed to load never reaches the conflict check, so its slot is bare too
                    foreach (string category in picks.Keys)
                    {
                        if (round < MAX_REPICK_ROUNDS
                            && OutfitRandomizer.IsCoreCategory(category)
                            && !wearablesByCategory.ContainsKey(category)
                            && !dropped.Contains(category)
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
            finally
            {
                foreach (HashSet<string> hides in hidesByCategory.Values)
                    HashSetPool<string>.Release(hides);
            }
        }

        private async UniTask ResolveAsync(List<URN> urns, BodyShape bodyShape,
            Dictionary<string, IWearable> wearablesByCategory,
            Dictionary<string, HashSet<string>> hidesByCategory,
            CancellationToken ct)
        {
            if (urns.Count == 0)
                return;

            using PooledObject<List<IWearable>> _ = ListPool<IWearable>.Get(out List<IWearable> resolved);

            await cacheOutfitWearablesCommand.ExecuteAsync(urns, bodyShape, ct, resolved, useFullUrns: true, dtoOnly: true);

            foreach (IWearable wearable in resolved)
            {
                string category = wearable.GetCategory();

                if (category == WearableCategories.Categories.BODY_SHAPE)
                    continue;

                if (!hidesByCategory.TryGetValue(category, out HashSet<string>? hides))
                {
                    hides = HashSetPool<string>.Get();
                    hidesByCategory[category] = hides;
                }

                hides.Clear();
                wearable.GetHidingList(bodyShape, hides);
                wearablesByCategory[category] = wearable;
            }
        }

        private BackpackEquipOutfitCommand CaptureCurrent()
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

            return new BackpackEquipOutfitCommand(bodyShape, wearables, eyes, hair, skin, new List<string>(equippedWearables.ForceRenderCategories), useFullUrns: true);
        }

        private void Apply(BackpackEquipOutfitCommand command)
        {
            isApplying = true;
            backpackCommandBus.SendCommand(command);
        }

        private void RefreshButtons() =>
            view.SetState(!isBusy && isAvatarReady, !isBusy && undoStack.Count > 0, !isBusy && redoStack.Count > 0);
    }
}
