using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.Backpack.BackpackBus;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using MVC;
using Runtime.Wearables;
using System;
using System.Threading;

namespace DCL.Backpack
{
    internal sealed class BackpackWearableEquip
    {
        private readonly IWearablesProvider provider;
        private readonly IReadOnlyEquippedWearables equipped;
        private readonly SmartWearableCache smartWearableCache;
        private readonly IMVCManager mvcManager;
        private readonly BackpackCommandBus commands;
        private readonly Action<int, bool> setLoading;
        private CancellationTokenSource? pending;
        private int pendingSlot;

        public BackpackWearableEquip(IWearablesProvider provider, IReadOnlyEquippedWearables equipped, SmartWearableCache smartWearableCache,
            IMVCManager mvcManager, BackpackCommandBus commands, Action<int, bool> setLoading)
        {
            this.provider = provider;
            this.equipped = equipped;
            this.smartWearableCache = smartWearableCache;
            this.mvcManager = mvcManager;
            this.commands = commands;
            this.setLoading = setLoading;
        }

        public bool IsBusy => pending != null;

        public async UniTask EquipAsync(int slot, string itemId)
        {
            if (IsBusy) return;

            using var attempt = new CancellationTokenSource();
            pending = attempt;
            pendingSlot = slot;
            CancellationToken ct = attempt.Token;
            bool applied = false;
            try
            {
                setLoading(slot, true);
                IWearable? shape = equipped.Wearable(WearableCategories.Categories.BODY_SHAPE);
                if (shape == null) return;

                BodyShape bodyShape = BodyShape.FromStringSafe(shape.GetUrn());
                IWearable? wearable = await provider.GetForEquipAsync(itemId, bodyShape, ct);
                if (ct.IsCancellationRequested || wearable == null) return;

                await AuthorizeSmartWearableAsync(wearable, ct);
                if (ct.IsCancellationRequested || equipped.Wearable(WearableCategories.Categories.BODY_SHAPE)?.GetUrn().Equals(shape.GetUrn()) != true) return;

                applied = commands.SendCommand(new BackpackEquipWearableCommand(wearable));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.WEARABLE); }
            finally
            {
                if (pending == attempt)
                {
                    pending = null;
                    setLoading(slot, false);
                    if (!applied)
                    {
                        ReportHub.LogWarning(ReportCategory.WEARABLE, $"Couldn't equip wearable {itemId}");
                        NotificationsBusController.Instance.AddNotification(new ServerErrorNotification("Couldn't equip this wearable. Try again."));
                    }
                }
            }
        }

        public void Cancel()
        {
            if (pending == null) return;

            var attempt = pending;
            pending = null;
            setLoading(pendingSlot, false);
            attempt.Cancel();
        }

        private async UniTask AuthorizeSmartWearableAsync(IWearable wearable, CancellationToken ct)
        {
            bool requiresAuthorization = await smartWearableCache.RequiresAuthorizationAsync(wearable, ct);
            string id = SmartWearableCache.GetCacheId(wearable);
            if (ct.IsCancellationRequested || !requiresAuthorization || smartWearableCache.AuthorizedSmartWearables.Contains(id)) return;

            bool authorized = await SmartWearableAuthorizationPopupController.RequestAuthorizationAsync(mvcManager, wearable, ct);
            if (ct.IsCancellationRequested) return;

            // Declining prevents the portable experience from running; the wearable can still be equipped.
            if (authorized)
                smartWearableCache.AuthorizedSmartWearables.Add(id);
            else
                smartWearableCache.KilledPortableExperiences.Add(id);
        }
    }
}
