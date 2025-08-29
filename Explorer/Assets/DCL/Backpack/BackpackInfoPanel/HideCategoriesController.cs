using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Backpack
{
    public class HideCategoriesController
    {
        private const int MAX_HIDE_CATEGORIES = 13;

        private readonly HideCategoryGridView view;
        private readonly IReadOnlyEquippedWearables equippedWearables;
        private readonly NftTypeIconSO categoryIcons;

        private readonly List<HideCategoryView> usedHides = new (MAX_HIDE_CATEGORIES);
        private readonly HashSet<string> hidingList = new (MAX_HIDE_CATEGORIES);

        private IObjectPool<HideCategoryView>? hidesPool;

        public HideCategoriesController(
            HideCategoryGridView view,
            IBackpackEventBus backpackEventBus,
            IReadOnlyEquippedWearables equippedWearables,
            NftTypeIconSO categoryIcons)
        {
            this.view = view;
            this.equippedWearables = equippedWearables;
            this.categoryIcons = categoryIcons;

            backpackEventBus.SelectWearableEvent += SetHideCategories;
        }

        public async UniTask InitializeAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct)
        {
            HideCategoryView hideCategoryView = (await assetsProvisioner.ProvideMainAssetAsync(view.HideCategory, ct: ct)).Value;

            hidesPool = new ObjectPool<HideCategoryView>(
                () => CreateCategoryHide(hideCategoryView),
                defaultCapacity: MAX_HIDE_CATEGORIES,
                actionOnGet: hideView => hideView.gameObject.SetActive(true),
                actionOnRelease: hideView => hideView.gameObject.SetActive(false)
            );
        }

        private HideCategoryView CreateCategoryHide(HideCategoryView categoryHide)
        {
            HideCategoryView categoryHideItem = Object.Instantiate(categoryHide, view.transform);
            return categoryHideItem;
        }

        private void SetHideCategories(IWearable wearable)
        {
            if (hidesPool == null)
            {
                view.HideHeader.SetActive(false);
                return;
            }

            ClearPool();

            IWearable? bodyShapeWearable = equippedWearables.Wearable(WearablesConstants.Categories.BODY_SHAPE);

            if (bodyShapeWearable == null)
            {
                view.HideHeader.SetActive(false);
                return;
            }

            URN bodyShapeUrn = bodyShapeWearable.GetUrn();
            wearable.GetHidingList(bodyShapeUrn, hidingList);
            view.HideHeader.SetActive(hidingList.Count > 0);

            foreach (string category in hidingList)
            {
                HideCategoryView hideCategoryView = hidesPool.Get();
                usedHides.Add(hideCategoryView);

                hideCategoryView.transform.SetParent(view.HideCategoriesContainer, false);
                hideCategoryView.transform.SetAsLastSibling();
                hideCategoryView.categoryText.text = WearableComponentsUtils.CATEGORIES_TO_READABLE[category];
                hideCategoryView.categoryImage.sprite = categoryIcons.GetTypeImage(category);
            }
        }

        private void ClearPool()
        {
            if (hidesPool == null) return;

            foreach (var usedHide in usedHides)
                hidesPool.Release(usedHide);

            hidingList.Clear();
            usedHides.Clear();
        }
    }
}
