using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Web3.Identities;
using MVC;
using Runtime.Wearables;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;
using Utility.PortableExperiences;

namespace DCL.UI.PortableExperiences.SummaryPopup
{
    public class PortableExperiencesSummaryController : ControllerBase<PortableExperiencesSummaryView>
    {
        private static readonly InputMapComponent.Kind[] BLOCKED_INPUTS = { InputMapComponent.Kind.Shortcuts, InputMapComponent.Kind.InWorldCamera, InputMapComponent.Kind.Player };

        private readonly IInputBlock inputBlock;
        private readonly IPortableExperiencesLifecycle pxLifecycle;
        private readonly IPortableExperiencesStatus globalPxStatus;
        private readonly ILocalPortableExperiencesStatus localPxStatus;
        private readonly IWearableStorage wearableStorage;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly ISelfProfile selfProfile;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;

        private readonly List<string> globalPxIds = new ();
        private readonly List<string> localPxIds = new ();
        private readonly List<SmartWearableEntry> smartWearables = new ();

        private readonly Action<string> smartWearableRemoveRequested;
        private readonly Action<string> localPxRemoveRequested;

        private CancellationTokenSource panelCts = new ();
        private CancellationTokenSource publishCts = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public PortableExperiencesSummaryController(
            ViewFactoryMethod viewFactory,
            IInputBlock inputBlock,
            IPortableExperiencesLifecycle pxLifecycle,
            SmartWearableCache smartWearableCache,
            IPortableExperiencesStatus globalPxStatus,
            ILocalPortableExperiencesStatus localPxStatus,
            IWearableStorage wearableStorage,
            IThumbnailProvider thumbnailProvider,
            ISelfProfile selfProfile,
            ProfileChangesBus profileChangesBus,
            IWeb3IdentityCache web3IdentityCache,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons)
            : base(viewFactory)
        {
            this.inputBlock = inputBlock;
            this.pxLifecycle = pxLifecycle;
            this.globalPxStatus = globalPxStatus;
            this.localPxStatus = localPxStatus;
            this.wearableStorage = wearableStorage;
            this.thumbnailProvider = thumbnailProvider;
            this.selfProfile = selfProfile;
            this.profileChangesBus = profileChangesBus;
            this.web3IdentityCache = web3IdentityCache;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;

            smartWearableRemoveRequested = OnSmartWearableRemoveRequested;
            localPxRemoveRequested = OnLocalPxRemoveRequested;

            pxLifecycle.PortableExperienceLoaded += OnPortableExperienceLoaded;
            pxLifecycle.PortableExperienceUnloaded += OnPortableExperienceUnloaded;
            web3IdentityCache.OnIdentityCleared += OnIdentityCleared;

            foreach (string id in globalPxStatus.RunningPortableExperiences)
                globalPxIds.Add(id);

            foreach (string id in localPxStatus.RunningPortableExperiences)
                localPxIds.Add(id);

            foreach (string id in smartWearableCache.RunningSmartWearables)
                AddSmartWearable(id);
        }

        public override void Dispose()
        {
            pxLifecycle.PortableExperienceLoaded -= OnPortableExperienceLoaded;
            pxLifecycle.PortableExperienceUnloaded -= OnPortableExperienceUnloaded;
            web3IdentityCache.OnIdentityCleared -= OnIdentityCleared;

            panelCts.SafeCancelAndDispose();
            publishCts.SafeCancelAndDispose();
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.globalPxLoopList.InitListView(0, OnGetGlobalPxItemByIndex);
            viewInstance.smartWearableLoopList.InitListView(0, OnGetSmartWearableItemByIndex);
            viewInstance.localPxLoopList.InitListView(0, OnGetLocalPxItemByIndex);
        }

        protected override void OnBeforeViewShow()
        {
            DisableShortcutsInput();

            panelCts = panelCts.SafeRestart();

            RefreshList(viewInstance!.globalPxLoopList, globalPxIds.Count, resetScroll: true);
            RefreshList(viewInstance.smartWearableLoopList, smartWearables.Count, resetScroll: true);
            RefreshList(viewInstance.localPxLoopList, localPxIds.Count, resetScroll: true);
        }

        protected override void OnViewClose()
        {
            RestoreInput();
            panelCts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            viewInstance!.closeButton.OnClickAsync(ct);

        private void DisableShortcutsInput() =>
            inputBlock.Disable(BLOCKED_INPUTS);

        private void RestoreInput() =>
            inputBlock.Enable(BLOCKED_INPUTS);

        private void OnPortableExperienceLoaded(string id)
        {
            if (Contains(globalPxStatus.RunningPortableExperiences, id))
            {
                if (IndexOf(globalPxIds, id) < 0)
                {
                    globalPxIds.Add(id);
                    RefreshGlobalPxListIfLive();
                }
            }
            else if (Contains(localPxStatus.RunningPortableExperiences, id))
            {
                if (IndexOf(localPxIds, id) < 0)
                {
                    localPxIds.Add(id);
                    RefreshLocalPxListIfLive();
                }
            }
            else
            {
                if (AddSmartWearable(id))
                    RefreshSmartWearableListIfLive();
            }
        }

        private void OnPortableExperienceUnloaded(string id)
        {
            int index = IndexOf(globalPxIds, id);

            if (index >= 0)
            {
                globalPxIds.RemoveAt(index);
                RefreshGlobalPxListIfLive();
                return;
            }

            index = IndexOf(localPxIds, id);

            if (index >= 0)
            {
                localPxIds.RemoveAt(index);
                RefreshLocalPxListIfLive();
                return;
            }

            index = IndexOfSmartWearable(id);

            if (index >= 0)
            {
                smartWearables.RemoveAt(index);
                RefreshSmartWearableListIfLive();
            }
        }

        private void OnIdentityCleared()
        {
            globalPxIds.Clear();
            localPxIds.Clear();
            smartWearables.Clear();

            RefreshGlobalPxListIfLive();
            RefreshLocalPxListIfLive();
            RefreshSmartWearableListIfLive();
        }

        private bool AddSmartWearable(string id)
        {
            if (IndexOfSmartWearable(id) >= 0) return false;

            if (!wearableStorage.TryGetElement(id, out IWearable wearable))
                ReportHub.LogWarning(ReportCategory.PORTABLE_EXPERIENCE, $"Running Smart Wearable '{id}' not found in the wearable storage, showing a degraded entry.");

            smartWearables.Add(new SmartWearableEntry(id, wearable));
            return true;
        }

        private LoopListViewItem2? OnGetGlobalPxItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= globalPxIds.Count) return null;

            LoopListViewItem2 item = listView.NewListViewItem(listView.ItemPrefabDataList[0].mItemPrefab.name);
            item.GetComponent<GlobalPxEntryView>().Configure(globalPxIds[index]);
            return item;
        }

        private LoopListViewItem2? OnGetSmartWearableItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= smartWearables.Count) return null;

            SmartWearableEntry entry = smartWearables[index];
            LoopListViewItem2 item = listView.NewListViewItem(listView.ItemPrefabDataList[0].mItemPrefab.name);
            var entryView = item.GetComponent<SmartWearableEntryView>();

            entryView.RemoveRequested = smartWearableRemoveRequested;

            IWearable? wearable = entry.Wearable;

            if (wearable != null)
            {
                entryView.Configure(
                    entry.Id,
                    wearable.GetName(),
                    rarityBackgrounds.GetTypeImage(wearable.GetRarity()),
                    rarityColors.GetColor(wearable.GetRarity()),
                    categoryIcons.GetTypeImage(wearable.GetCategory()));

                entryView.LoadThumbnail(thumbnailProvider, wearable, panelCts.Token);
            }
            else
                entryView.Configure(entry.Id, entry.Id, rarityBackgrounds.GetTypeImage(null), rarityColors.GetColor(null), categoryIcons.GetTypeImage(null));

            return item;
        }

        private LoopListViewItem2? OnGetLocalPxItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= localPxIds.Count) return null;

            string id = localPxIds[index];
            LoopListViewItem2 item = listView.NewListViewItem(listView.ItemPrefabDataList[0].mItemPrefab.name);
            var entryView = item.GetComponent<LocalPxEntryView>();

            entryView.RemoveRequested = localPxRemoveRequested;
            entryView.Configure(id);
            entryView.SetRemoveInteractable(pxLifecycle.CanKillPortableExperience(id));
            return item;
        }

        private void OnSmartWearableRemoveRequested(string id)
        {
            // Not Kill: the killed marker only clears on an unequip event this flow never raises, so it would block a future re-equip.
            pxLifecycle.UnloadPortableExperience(id);

            publishCts = publishCts.SafeRestart();
            UnequipAndPublishAsync(id, publishCts.Token).Forget();
        }

        private async UniTaskVoid UnequipAndPublishAsync(string id, CancellationToken ct)
        {
            try
            {
                Profile? profile = await selfProfile.ProfileAsync(ct);
                if (ct.IsCancellationRequested || profile == null) return;

                URN urnToRemove = id;
                var wearables = new List<URN>(profile.Avatar.Wearables.Count);

                foreach (URN urn in profile.Avatar.Wearables)
                    if (!urn.Shorten().Equals(urnToRemove))
                        wearables.Add(urn);

                if (wearables.Count == profile.Avatar.Wearables.Count) return;

                Profile updated = new ProfileBuilder().From(profile).WithWearables(wearables).Build();

                Profile? saved = await selfProfile.UpdateProfileAsync(updated, ct);
                if (ct.IsCancellationRequested || saved == null) return;

                profileChangesBus.PushUpdate(saved);
            }
            catch (OperationCanceledException) { }
            catch (IdenticalProfileUpdateException) { ReportHub.LogWarning(ReportCategory.PORTABLE_EXPERIENCE, $"Unequipping Smart Wearable '{id}' produced an identical profile, nothing was deployed."); }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.PORTABLE_EXPERIENCE); }
        }

        private void OnLocalPxRemoveRequested(string id)
        {
            if (pxLifecycle.CanKillPortableExperience(id))
                pxLifecycle.KillPortableExperience(id);
        }

        private void RefreshGlobalPxListIfLive()
        {
            if (!IsViewLive()) return;

            RefreshList(viewInstance!.globalPxLoopList, globalPxIds.Count, resetScroll: false);
        }

        private void RefreshSmartWearableListIfLive()
        {
            if (!IsViewLive()) return;

            RefreshList(viewInstance!.smartWearableLoopList, smartWearables.Count, resetScroll: false);
        }

        private void RefreshLocalPxListIfLive()
        {
            if (!IsViewLive()) return;

            RefreshList(viewInstance!.localPxLoopList, localPxIds.Count, resetScroll: false);
        }

        // True only in the window where item binds may run, so they never touch the disposed panelCts.
        private bool IsViewLive() =>
            State is ControllerState.ViewShowing or ControllerState.ViewFocused;

        private static void RefreshList(LoopListView2 loopList, int count, bool resetScroll)
        {
            loopList.SetListItemCount(count, false);
            loopList.RefreshAllShownItem();

            if (resetScroll && count > 0)
                loopList.MovePanelToItemIndex(0, 0);
        }

        private int IndexOfSmartWearable(string id)
        {
            for (var i = 0; i < smartWearables.Count; i++)
                if (string.Equals(smartWearables[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return i;

            return -1;
        }

        private static int IndexOf(List<string> ids, string id)
        {
            for (var i = 0; i < ids.Count; i++)
                if (string.Equals(ids[i], id, StringComparison.OrdinalIgnoreCase))
                    return i;

            return -1;
        }

        private static bool Contains(IReadOnlyCollection<string> ids, string id)
        {
            foreach (string current in ids)
                if (string.Equals(current, id, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        private readonly struct SmartWearableEntry
        {
            public readonly string Id;

            // Null when the id was not found in the wearable storage.
            public readonly IWearable? Wearable;

            public SmartWearableEntry(string id, IWearable? wearable)
            {
                Id = id;
                Wearable = wearable;
            }
        }
    }
}
