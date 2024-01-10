using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI
{
    public class PageSelectorController
    {
        public event Action<int> OnSetPage;

        private readonly PageSelectorView view;
        private int currentPage;
        private int totalPages;

        public PageSelectorController(PageSelectorView view)
        {
            this.view = view;

            view.NextPage.onClick.AddListener(NextPageClicked);
            view.PreviousPage.onClick.AddListener(PreviousPageClicked);
        }

        private void NextPageClicked()
        {
            if (currentPage >= totalPages)
                return;

            currentPage++;
            OnSetPage?.Invoke(currentPage);
        }

        private void PreviousPageClicked()
        {
            if (currentPage <= 1)
                return;

            currentPage--;
            OnSetPage?.Invoke(currentPage);
        }

        public void Configure(int maxElements, int pageSize)
        {
            totalPages = (maxElements + pageSize - 1) / pageSize;
            currentPage = 1;
            for(int i=0; i<totalPages; i++)
            {
                //GameObject newPage = Object.Instantiate(view.pagePrefab, view.transform);
                //newPage.SetActive(true);
            }
        }
    }
}
