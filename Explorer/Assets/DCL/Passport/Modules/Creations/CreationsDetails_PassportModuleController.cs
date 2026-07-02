using Cysharp.Threading.Tasks;
using DCL.Passport.Fields;
using DCL.Profiles;
using DCL.WebRequests;
using System;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Passport.Modules.Creations
{
    public class CreationsDetails_PassportModuleController : IPassportModuleController
    {
        private const int EQUIPPED_ITEMS_POOL_DEFAULT_CAPACITY = 28;
        private const int LOADING_ITEMS_POOL_DEFAULT_CAPACITY = 12;
        private readonly CreationsDetails_PassportModuleView view;
        private readonly IWebRequestController webRequestController;
        private readonly IObjectPool<EquippedItem_PassportFieldView> wearablesItemsPool;
        private readonly IObjectPool<EquippedItem_PassportFieldView> emotesItemsPool;

        public CreationsDetails_PassportModuleController(CreationsDetails_PassportModuleView view, IWebRequestController webRequestController)
        {
            this.view = view;
            this.webRequestController = webRequestController;

            wearablesItemsPool = new ObjectPool<EquippedItem_PassportFieldView>(
                () => InstantiateWearablesItemPrefab(view.CreatedWearablesContainer),
                defaultCapacity: LOADING_ITEMS_POOL_DEFAULT_CAPACITY,
                actionOnGet: loadingItemView =>
                {
                    loadingItemView.gameObject.SetActive(true);
                    loadingItemView.gameObject.transform.SetAsLastSibling();
                    loadingItemView.SetAsLoading(true);
                },
                actionOnRelease: loadingItemView =>
                {
                    loadingItemView.SetAsLoading(false);
                    loadingItemView.gameObject.SetActive(false);
                }
            );

            emotesItemsPool = new ObjectPool<EquippedItem_PassportFieldView>(
                () => InstantiateWearablesItemPrefab(view.CreatedEmotesContainer),
                defaultCapacity: LOADING_ITEMS_POOL_DEFAULT_CAPACITY,
                actionOnGet: loadingItemView =>
                {
                    loadingItemView.gameObject.SetActive(true);
                    loadingItemView.gameObject.transform.SetAsLastSibling();
                    loadingItemView.SetAsLoading(true);
                },
                actionOnRelease: loadingItemView =>
                {
                    loadingItemView.SetAsLoading(false);
                    loadingItemView.gameObject.SetActive(false);
                }
            );
        }

        private EquippedItem_PassportFieldView InstantiateWearablesItemPrefab(RectTransform parent)
        {
            EquippedItem_PassportFieldView equippedItemView = Object.Instantiate(view.equippedItemPrefab, parent);
            return equippedItemView;
        }

        public void Dispose()
        {
        }

        public void Setup(Profile profile)
        {

        }

        private async UniTaskVoid LoadCreatedWearables()
        {

        }

        private async UniTaskVoid LoadCreatedEmotes()
        {

        }

        public void Clear()
        {
        }
    }
}
