using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.Backpack.BackpackBus;
using DCL.Profiles;
using DCL.Web3Authentication;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;
using ParamPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearableByParamIntention>;

namespace DCL.Backpack
{
    public class BackpackGridController
    {
        private readonly BackpackGridView view;
        private readonly BackpackCommandBus commandBus;
        private readonly BackpackEventBus eventBus;
        private readonly IWeb3Authenticator web3Authenticator;

        private IObjectPool<BackpackItemView> gridItemsPool;
        private readonly Dictionary<string, BackpackItemView> usedPoolItems;
        private World world;

        public BackpackGridController(BackpackGridView view, BackpackCommandBus commandBus, BackpackEventBus eventBus, IWeb3Authenticator web3Authenticator)
        {
            this.view = view;
            this.commandBus = commandBus;
            this.eventBus = eventBus;
            this.web3Authenticator = web3Authenticator;

            usedPoolItems = new ();
            eventBus.EquipEvent += OnEquip;
            eventBus.UnEquipEvent += OnUnequip;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in Entity playerEntity)
        {
            world = builder.World;
        }

        public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct)
        {
            BackpackItemView backpackItem = (await assetsProvisioner.ProvideInstanceAsync(view.BackpackItem, ct: ct)).Value;

            gridItemsPool = new ObjectPool<BackpackItemView>(
                () => CreateBackpackItem(backpackItem),
                _ => { },
                defaultCapacity: 16
            );
        }

        public void SetGridElements(IWearable[] gridWearables)
        {
            ClearPoolElements();
            for (var i = 0; i < gridWearables.Length; i++)
            {
                BackpackItemView backpackItemView = gridItemsPool.Get();
                backpackItemView.ItemId = gridWearables[i].GetUrn();
                usedPoolItems.Add(backpackItemView.ItemId, backpackItemView);
            }
        }

        private BackpackItemView CreateBackpackItem(BackpackItemView backpackItem)
        {
            BackpackItemView backpackItemView = Object.Instantiate(backpackItem, view.gameObject.transform);
            backpackItem.OnSelectItem += ()=>SelectItem(backpackItem.ItemId);
            backpackItem.EquipButton.onClick.AddListener(() => commandBus.SendCommand(new BackpackEquipCommand(backpackItemView.ItemId)));
            return backpackItemView;
        }

        public void RequestPage(int pageNumber)
        {
            //Reuse params array and review types URLParameter
            ParamPromise wearablesPromise = ParamPromise.Create(world, new GetWearableByParamIntention(new[] { ("pageNumber", string.Format("{0}", pageNumber)), ("pageSize", "16") }, web3Authenticator.Identity.EphemeralAccount.Address, new List<IWearable>()), PartitionComponent.TOP_PRIORITY);
            AwaitWearablesPromise(wearablesPromise).Forget();
        }

        private async UniTaskVoid AwaitWearablesPromise(ParamPromise wearablesPromise)
        {
            AssetPromise<IWearable[],GetWearableByParamIntention> uniTaskAsync = await wearablesPromise.ToUniTaskAsync(world);

            if (!uniTaskAsync.Result.Value.Succeeded)
                return;

            //TODO Temporary, will create the correct flow in next PR
            SetGridElements(uniTaskAsync.Result.Value.Asset);
        }

        private void ClearPoolElements()
        {
            foreach (var backpackItemView in usedPoolItems)
            {
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
            if (usedPoolItems.ContainsKey(unequippedWearable.GetUrn()))
                usedPoolItems[unequippedWearable.GetUrn()].EquippedIcon.SetActive(false);
        }

        private void OnEquip(IWearable equippedWearable)
        {
            if (usedPoolItems.ContainsKey(equippedWearable.GetUrn()))
                usedPoolItems[equippedWearable.GetUrn()].EquippedIcon.SetActive(true);
        }

    }
}
