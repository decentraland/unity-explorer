using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Backpack
{
    public class BackpackGridController
    {
        private readonly BackpackGridView view;
        private readonly BackpackCommandBus commandBus;
        private readonly BackpackEventBus eventBus;

        private IObjectPool<BackpackItemView> gridItemsPool;

        public BackpackGridController(BackpackGridView view, BackpackCommandBus commandBus, BackpackEventBus eventBus)
        {
            this.view = view;
            this.commandBus = commandBus;
            this.eventBus = eventBus;

            eventBus.EquipEvent += OnEquip;
            eventBus.UnEquipEvent += OnUnequip;
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
            for (var i = 0; i < gridWearables.Length; i++)
            {
                BackpackItemView backpackItemView = gridItemsPool.Get();
                backpackItemView.ItemId = gridWearables[i].GetUrn();
                
            }
        }

        private BackpackItemView CreateBackpackItem(BackpackItemView backpackItem)
        {
            BackpackItemView backpackItemView = Object.Instantiate(backpackItem, view.gameObject.transform);
            backpackItem.OnSelectItem += SelectItem;
            return backpackItemView;
        }

        private void SelectItem()
        {
            commandBus.SendCommand(new BackpackCommand(BackpackCommandType.SelectCommand, "", ""));
        }

        private void OnUnequip(IWearable unequippedWearable)
        {

        }

        private void OnEquip(IWearable equippedWearable)
        {

        }

    }
}
