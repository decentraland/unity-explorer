using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.Backpack.BackpackBus;
using DCL.Web3Authentication.Identities;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;
using ParamPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearableByParamIntention>;

namespace DCL.Backpack
{
    public class BackpackGridController
    {
        private const string PAGE_NUMBER = "pageNumber";
        private const string PAGE_SIZE = "pageSize";

        private const int CURRENT_PAGE_SIZE = 16;
        private static readonly string CURRENT_PAGE_SIZE_STR = CURRENT_PAGE_SIZE.ToString();

        private readonly BackpackGridView view;
        private readonly BackpackCommandBus commandBus;
        private readonly BackpackEventBus eventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;
        private readonly IBackpackEquipStatusController backpackEquipStatusController;

        private readonly List<(string, string)> requestParameters;
        private readonly List<IWearable> results = new (CURRENT_PAGE_SIZE);

        private IObjectPool<BackpackItemView> gridItemsPool;
        private readonly Dictionary<string, BackpackItemView> usedPoolItems;
        private World world;
        private CancellationTokenSource cts;

        public BackpackGridController(
            BackpackGridView view,
            BackpackCommandBus commandBus,
            BackpackEventBus eventBus,
            IWeb3IdentityCache web3IdentityCache,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            IBackpackEquipStatusController backpackEquipStatusController)
        {
            this.view = view;
            this.commandBus = commandBus;
            this.eventBus = eventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.backpackEquipStatusController = backpackEquipStatusController;

            usedPoolItems = new ();
            eventBus.EquipEvent += OnEquip;
            eventBus.UnEquipEvent += OnUnequip;
            requestParameters = new List<(string, string)>();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in Entity playerEntity)
        {
            world = builder.World;
        }

        public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct)
        {
            BackpackItemView backpackItem = (await assetsProvisioner.ProvideMainAssetAsync(view.BackpackItem, ct: ct)).Value;

            gridItemsPool = new ObjectPool<BackpackItemView>(
                () => CreateBackpackItem(backpackItem),
                _ => { },
                defaultCapacity: CURRENT_PAGE_SIZE
            );
        }

        private void SetGridElements(IWearable[] gridWearables)
        {
            ClearPoolElements();

            for (var i = 0; i < gridWearables.Length; i++)
            {
                BackpackItemView backpackItemView = gridItemsPool.Get();
                usedPoolItems.Add(gridWearables[i].GetUrn(), backpackItemView);

                backpackItemView.OnSelectItem += SelectItem;
                backpackItemView.EquipButton.onClick.AddListener(() => { commandBus.SendCommand(new BackpackEquipCommand(backpackItemView.ItemId)); });
                backpackItemView.UnEquipButton.onClick.AddListener(() => { commandBus.SendCommand(new BackpackUnEquipCommand(backpackItemView.ItemId)); });
                backpackItemView.ItemId = gridWearables[i].GetUrn();
                backpackItemView.RarityBackground.sprite = rarityBackgrounds.GetTypeImage(gridWearables[i].GetRarity());
                backpackItemView.FlapBackground.color = rarityColors.GetColor(gridWearables[i].GetRarity());
                backpackItemView.CategoryImage.sprite = categoryIcons.GetTypeImage(gridWearables[i].GetCategory());
                backpackItemView.EquippedIcon.SetActive(backpackEquipStatusController.IsWearableEquipped(gridWearables[i]));

                backpackItemView.SetEquipButtonsState();
                WaitForThumbnailAsync(gridWearables[i], backpackItemView, cts.Token).Forget();
            }
        }

        private BackpackItemView CreateBackpackItem(BackpackItemView backpackItem)
        {
            BackpackItemView backpackItemView = Object.Instantiate(backpackItem, view.gameObject.transform);
            return backpackItemView;
        }

        public void RequestPage(int pageNumber)
        {
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();

            requestParameters.Clear();
            requestParameters.Add((PAGE_NUMBER, pageNumber.ToString()));
            requestParameters.Add((PAGE_SIZE, CURRENT_PAGE_SIZE_STR));

            results.Clear();

            var wearablesPromise = ParamPromise.Create(world,
                new GetWearableByParamIntention(requestParameters, "0x8e41609eD5e365Ac23C28d9625Bd936EA9C9E22c", results),
                PartitionComponent.TOP_PRIORITY);

            AwaitWearablesPromiseAsync(wearablesPromise, cts.Token).Forget();
        }

        private async UniTaskVoid AwaitWearablesPromiseAsync(ParamPromise wearablesPromise, CancellationToken ct)
        {
            AssetPromise<IWearable[], GetWearableByParamIntention> uniTaskAsync = await wearablesPromise.ToUniTaskAsync(world, cancellationToken: ct);

            if (!uniTaskAsync.Result!.Value.Succeeded)
                return;

            SetGridElements(uniTaskAsync.Result.Value.Asset);
        }

        private async UniTaskVoid WaitForThumbnailAsync(IWearable itemWearable, BackpackItemView itemView, CancellationToken ct)
        {
            itemView.LoadingView.StartLoadingAnimation(itemView.FullBackpackItem);

            do { await UniTask.Delay(500, cancellationToken: ct); }
            while (itemWearable.WearableThumbnail == null);

            itemView.WearableThumbnail.sprite = itemWearable.WearableThumbnail.Value.Asset;
            itemView.LoadingView.FinishLoadingAnimation(itemView.FullBackpackItem);
        }

        private void ClearPoolElements()
        {
            foreach (var backpackItemView in usedPoolItems)
            {
                backpackItemView.Value.EquipButton.onClick.RemoveAllListeners();
                backpackItemView.Value.UnEquipButton.onClick.RemoveAllListeners();
                backpackItemView.Value.OnSelectItem -= SelectItem;
                backpackItemView.Value.EquippedIcon.SetActive(false);
                gridItemsPool.Release(backpackItemView.Value);
            }

            usedPoolItems.Clear();
        }

        private void SelectItem(string itemId)
        {
            commandBus.SendCommand(new BackpackSelectCommand(itemId));
        }

        private void OnUnequip(IWearable unequippedWearable)
        {
            if (usedPoolItems.TryGetValue(unequippedWearable.GetUrn(), out BackpackItemView backpackItemView))
            {
                backpackItemView.EquippedIcon.SetActive(false);
                backpackItemView.SetEquipButtonsState();
            }
        }

        private void OnEquip(IWearable equippedWearable)
        {
            if (usedPoolItems.TryGetValue(equippedWearable.GetUrn(), out BackpackItemView backpackItemView))
            {
                backpackItemView.EquippedIcon.SetActive(true);
                backpackItemView.SetEquipButtonsState();
            }
        }
    }
}
