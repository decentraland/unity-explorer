using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using System.Collections.Generic;
using System.Threading;
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
        private readonly Dictionary<string, BackpackItemView> usedPoolItems;

        public BackpackGridController(BackpackGridView view, BackpackCommandBus commandBus, BackpackEventBus eventBus)
        {
            this.view = view;
            this.commandBus = commandBus;
            this.eventBus = eventBus;
            usedPoolItems = new ();
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

        private void ClearPoolElements()
        {
            foreach (var backpackItemView in usedPoolItems)
                gridItemsPool.Release(backpackItemView.Value);

            usedPoolItems.Clear();
        }

        private void SelectItem(string itemId)
        {
            commandBus.SendCommand(new BackpackSelectCommand(itemId));
        }

        private void OnUnequip(IWearable unequippedWearable)
        {

        }

        private void OnEquip(IWearable equippedWearable)
        {

        }

    }
}
