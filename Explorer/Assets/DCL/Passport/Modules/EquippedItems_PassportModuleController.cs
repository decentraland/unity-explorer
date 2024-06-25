using CommunicationData.URLHelpers;
using DCL.Profiles;
using System.Collections.Generic;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class EquippedItems_PassportModuleController : IPassportModuleController
    {
        private const int EQUIPPED_ITEMS_POOL_DEFAULT_CAPACITY = 28;

        private readonly EquippedItems_PassportModuleView view;
        private readonly IObjectPool<EquippedItem_PassportFieldView> equippedItemsPool;
        private readonly List<EquippedItem_PassportFieldView> instantiatedEquippedItems = new();

        private Profile currentProfile;

        public EquippedItems_PassportModuleController(EquippedItems_PassportModuleView view)
        {
            this.view = view;

            equippedItemsPool = new ObjectPool<EquippedItem_PassportFieldView>(
                InstantiateEquippedItemPrefab,
                defaultCapacity: EQUIPPED_ITEMS_POOL_DEFAULT_CAPACITY,
                actionOnGet: buttonView => { buttonView.gameObject.SetActive(true); },
                actionOnRelease: buttonView => { buttonView.gameObject.SetActive(false); }
            );
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            LoadEquippedItems();

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.EquippedItemsContainer);
        }

        public void Clear()
        {
            foreach (EquippedItem_PassportFieldView equippedItem in instantiatedEquippedItems)
                equippedItemsPool.Release(equippedItem);

            instantiatedEquippedItems.Clear();
        }

        public void Dispose() =>
            Clear();

        private EquippedItem_PassportFieldView InstantiateEquippedItemPrefab()
        {
            EquippedItem_PassportFieldView equippedItemView = UnityEngine.Object.Instantiate(view.equippedItemPrefab, view.EquippedItemsContainer);
            return equippedItemView;
        }

        private void LoadEquippedItems()
        {
            foreach (URN itemUrn in currentProfile.Avatar.Wearables)
                AddEquippedItem(itemUrn);
        }

        private void AddEquippedItem(URN itemUrn)
        {
            var newEquippedItem = equippedItemsPool.Get();
            newEquippedItem.transform.parent = view.EquippedItemsContainer;
            // TODO (Santi): Setup the newEquippedItem with the itemUrn

            instantiatedEquippedItems.Add(newEquippedItem);
        }
    }
}
