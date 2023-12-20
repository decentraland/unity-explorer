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
                () => Object.Instantiate(backpackItem, view.gameObject.transform),
                _ => { },
                defaultCapacity: 16
            );
        }

        private void OnUnequip(IWearable unequippedWearable)
        {

        }

        private void OnEquip(IWearable equippedWearable)
        {

        }

    }
}
