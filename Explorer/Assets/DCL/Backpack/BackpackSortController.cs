using System;

namespace DCL.Backpack
{
    public class BackpackSortController
    {
        public event Action<BackpackGridSort> OnSortChanged;

		private readonly BackpackSortDropdownView view;
        private BackpackGridSort currentSort;

        public BackpackSortController(BackpackSortDropdownView view)
        {
			this.view = view;
            currentSort = new BackpackGridSort(NftOrderByOperation.Date, false);

            view.sortNewest.onValueChanged.AddListener(OnSortNewest);
            view.sortOldest.onValueChanged.AddListener(OnSortOldest);
            view.sortRarest.onValueChanged.AddListener(OnSortRarest);
            view.sortLessRares.onValueChanged.AddListener(OnSortLessRare);
            view.sortNameAz.onValueChanged.AddListener(OnSortNameAz);
            view.sortNameZa.onValueChanged.AddListener(OnSortNameZa);
        }

        private void OnSortNewest(bool isOn)
        {
            if (isOn)
            {
                currentSort.OrderByOperation = NftOrderByOperation.Date;
                currentSort.SortAscending = false;
                OnSortChanged?.Invoke(currentSort);
            }
        }

        private void OnSortOldest(bool isOn)
        {
            if (isOn)
            {
                currentSort.OrderByOperation = NftOrderByOperation.Date;
                currentSort.SortAscending = true;
                OnSortChanged?.Invoke(currentSort);
            }
        }

        private void OnSortRarest(bool isOn)
        {
            if (isOn)
            {
                currentSort.OrderByOperation = NftOrderByOperation.Rarity;
                currentSort.SortAscending = false;
                OnSortChanged?.Invoke(currentSort);
            }
        }

        private void OnSortLessRare(bool isOn)
        {
            if (isOn)
            {
                currentSort.OrderByOperation = NftOrderByOperation.Rarity;
                currentSort.SortAscending = true;
                OnSortChanged?.Invoke(currentSort);
            }
        }

        private void OnSortNameAz(bool isOn)
        {
            if (isOn)
            {
                currentSort.OrderByOperation = NftOrderByOperation.Name;
                currentSort.SortAscending = true;
                OnSortChanged?.Invoke(currentSort);
            }
        }

        private void OnSortNameZa(bool isOn)
        {
            if (isOn)
            {
                currentSort.OrderByOperation = NftOrderByOperation.Name;
                currentSort.SortAscending = false;
                OnSortChanged?.Invoke(currentSort);
            }
        }
    }
}
