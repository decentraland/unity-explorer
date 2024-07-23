using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.UI
{
    public class PageSelectorController
    {
        private const int MAX_CONCURRENT_SHOWN_PAGES = 5;
        public event Action<int> OnSetPage;

        private readonly PageSelectorView view;
        private int currentPage;
        private int totalPages;

        private readonly PageButtonView referencePageButtonView;
        private readonly IObjectPool<PageButtonView> pagesPool;
        private readonly List<PageButtonView> usedPoolItems = new (MAX_CONCURRENT_SHOWN_PAGES);

        public PageSelectorController(PageSelectorView view, PageButtonView referencePageButtonView)
        {
            this.view = view;
            this.referencePageButtonView = referencePageButtonView;

            view.NextPage.onClick.AddListener(NextPageClicked);
            view.PreviousPage.onClick.AddListener(PreviousPageClicked);

            pagesPool = new ObjectPool<PageButtonView>(
                CreatePageButton,
                defaultCapacity: MAX_CONCURRENT_SHOWN_PAGES,
                actionOnGet: buttonView => { buttonView.gameObject.SetActive(true); },
                actionOnRelease: buttonView => { buttonView.gameObject.SetActive(false); }
            );
        }

        private PageButtonView CreatePageButton()
        {
            PageButtonView backpackItemView = Object.Instantiate(referencePageButtonView, view.PagesContainer);
            return backpackItemView;
        }

        private void NextPageClicked()
        {
            if (currentPage >= totalPages)
                currentPage = 1;
            else
                currentPage++;

            SetCurrentPage(currentPage);
            OnSetPage?.Invoke(currentPage);
        }

        private void PreviousPageClicked()
        {
            if (currentPage <= 1)
                currentPage = totalPages;
            else
                currentPage--;

            SetCurrentPage(currentPage);
            OnSetPage?.Invoke(currentPage);
        }

        public void SetActive(bool isActive)
        {
            view.gameObject.SetActive(isActive);
        }

        public void Configure(int maxElements, int pageSize)
        {
            totalPages = (maxElements + pageSize - 1) / pageSize;
            view.gameObject.SetActive(totalPages > 1);
            currentPage = 1;
            SetCurrentPage(currentPage);
        }

        private void SetCurrentPage(int selectedPage)
        {
            ClearPool();
            currentPage = selectedPage;

            //Calculate the starting page index and address both cases where the selected page is near the start or the end of the list
            //and when pages are less then the max pages to show
            int startingPage = Math.Max(selectedPage - (MAX_CONCURRENT_SHOWN_PAGES / 2), 1);
            if (totalPages > MAX_CONCURRENT_SHOWN_PAGES && totalPages - selectedPage <= 2)
               startingPage -= 2 - (totalPages - selectedPage);

            for (var i = 0; i < Math.Min(totalPages, MAX_CONCURRENT_SHOWN_PAGES); i++)
            {
                int pageIndex = startingPage + i;

                if (pageIndex <= totalPages) //This fixes an issue when clicking on the pages themselves really quick which would basically load non-existent pages
                    ConfigurePageButton(selectedPage, pageIndex);
            }
        }

        private void ConfigurePageButton(int selectedPage, int pageIndex)
        {
            PageButtonView pageButtonView = pagesPool.Get();
            usedPoolItems.Add(pageButtonView);
            pageButtonView.PageIndex = pageIndex;
            pageButtonView.PageText.text = pageButtonView.PageIndex.ToString();
            pageButtonView.SelectedBackground.SetActive(selectedPage == pageIndex);
            pageButtonView.PageButton.onClick.RemoveAllListeners();
            pageButtonView.PageButton.onClick.AddListener(() => ClickedOnPage(ref pageIndex));
            pageButtonView.gameObject.transform.SetAsLastSibling();
        }

        private void ClickedOnPage(ref int pageIndex)
        {
            SetCurrentPage(pageIndex);
            OnSetPage?.Invoke(pageIndex);
        }

        private void ClearPool()
        {
            foreach (PageButtonView pageButtonView in usedPoolItems)
                pagesPool.Release(pageButtonView);

            usedPoolItems.Clear();
        }
    }
}
