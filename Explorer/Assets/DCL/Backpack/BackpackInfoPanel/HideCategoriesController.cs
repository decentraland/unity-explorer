using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Backpack
{
    public class HideCategoriesController
    {
        private const int MAX_HIDE_ROWS = 5;
        private const int MAX_HIDE_CATEGORIES = 13;
        private const int ITEMS_PER_ROW = 3;

        private readonly HideCategoryGridView view;
        private readonly IBackpackEquipStatusController backpackEquipStatusController;
        private readonly NftTypeIconSO categoryIcons;
        private readonly List<HideCategoryRowView> usedRows = new (MAX_HIDE_ROWS);
        private readonly List<HideCategoryView> usedHides = new (MAX_HIDE_CATEGORIES);
        private readonly HashSet<string> hidingList = new (MAX_HIDE_CATEGORIES);

        private IObjectPool<HideCategoryRowView>? rowsPool;
        private IObjectPool<HideCategoryView>? hidesPool;

        public HideCategoriesController(
            HideCategoryGridView view,
            IBackpackEventBus backpackEventBus,
            IBackpackEquipStatusController backpackEquipStatusController,
            NftTypeIconSO categoryIcons)
        {
            this.view = view;
            this.backpackEquipStatusController = backpackEquipStatusController;
            this.categoryIcons = categoryIcons;

            backpackEventBus.SelectWearableEvent += SetHideCategories;
        }

        public async UniTask InitializeAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct)
        {
            HideCategoryRowView hideCategoryRowView = (await assetsProvisioner.ProvideMainAssetAsync(view.HideRow, ct: ct)).Value;
            HideCategoryView hideCategoryView = (await assetsProvisioner.ProvideMainAssetAsync(view.HideCategory, ct: ct)).Value;

            rowsPool = new ObjectPool<HideCategoryRowView>(
                () => CreateCategoryRow(hideCategoryRowView),
                defaultCapacity: MAX_HIDE_ROWS,
                actionOnGet: rowView => rowView.gameObject.SetActive(true),
                actionOnRelease: rowView => rowView.gameObject.SetActive(false)
            );

            hidesPool = new ObjectPool<HideCategoryView>(
                () => CreateCategoryHide(hideCategoryView),
                defaultCapacity: MAX_HIDE_CATEGORIES,
                actionOnGet: hideView => hideView.gameObject.SetActive(true),
                actionOnRelease: hideView => hideView.gameObject.SetActive(false)
            );
        }

        private HideCategoryRowView CreateCategoryRow(HideCategoryRowView categoryRow)
        {
            HideCategoryRowView categoryRowItem = Object.Instantiate(categoryRow, view.HideCategoryRowsContainer);
            return categoryRowItem;
        }

        private HideCategoryView CreateCategoryHide(HideCategoryView categoryHide)
        {
            HideCategoryView categoryHideItem = Object.Instantiate(categoryHide, view.transform);
            return categoryHideItem;
        }

        private void SetHideCategories(IWearable wearable)
        {
            if (rowsPool == null || hidesPool == null)
            {
                view.HideHeader.SetActive(false);
                return;
            }

            ClearPools();

            IWearable? bodyShapeWearable = backpackEquipStatusController.GetEquippedWearableForCategory(WearablesConstants.Categories.BODY_SHAPE);

            if (bodyShapeWearable == null)
            {
                view.HideHeader.SetActive(false);
                return;
            }

            URN bodyShapeUrn = bodyShapeWearable.GetUrn();
            wearable.GetHidingList(bodyShapeUrn, hidingList);
            var rowsNumber = (int)Math.Ceiling((double)hidingList.Count / ITEMS_PER_ROW);
            view.HideHeader.SetActive(hidingList.Count > 0);

            for (var i = 0; i < rowsNumber; i++)
            {
                HideCategoryRowView hideCategoryRowView = rowsPool.Get();
                usedRows.Add(hideCategoryRowView);
                hideCategoryRowView.transform.SetAsLastSibling();

                for (int j = 0; j < ITEMS_PER_ROW; j++)
                {
                    int itemIndex = j + (i * ITEMS_PER_ROW);

                    if (itemIndex >= hidingList.Count)
                        return;

                    HideCategoryView hideCategoryView = hidesPool.Get();
                    usedHides.Add(hideCategoryView);
                    hideCategoryView.transform.parent = hideCategoryRowView.transform;
                    hideCategoryView.transform.SetAsLastSibling();
                    string[] hidingArray = hidingList.ToArray();
                    hideCategoryView.categoryText.text = AvatarWearableHide.CATEGORIES_TO_READABLE[hidingArray[itemIndex]];
                    hideCategoryView.categoryImage.sprite = categoryIcons.GetTypeImage(hidingArray[itemIndex]);
                }
            }
        }

        private void ClearPools()
        {
            if (rowsPool == null || hidesPool == null) return;

            foreach (var usedHide in usedHides)
                hidesPool.Release(usedHide);

            foreach (var usedRow in usedRows)
                rowsPool.Release(usedRow);

            hidingList.Clear();
            usedHides.Clear();
            usedRows.Clear();
        }
    }
}
