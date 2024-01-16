using DCL.Backpack.BackpackBus;
using System;
using UnityEngine;

namespace DCL.Backpack.Breadcrumb
{
    public class BackpackBreadCrumbController
    {
        private readonly BackpackBreadCrumbView view;
        private readonly IBackpackCommandBus commandBus;
        private readonly NftTypeIconSO categoryIcons;

        public BackpackBreadCrumbController(BackpackBreadCrumbView view, IBackpackEventBus eventBus, IBackpackCommandBus commandBus, NftTypeIconSO categoryIcons)
        {
            this.view = view;
            this.commandBus = commandBus;
            this.categoryIcons = categoryIcons;

            eventBus.FilterCategoryEvent += OnFilterCategory;
            eventBus.SearchEvent += OnSearch;

            view.SearchButton.ExitButton.onClick.AddListener(OnExitSearch);
            view.FilterButton.ExitButton.onClick.AddListener(OnExitFilter);
            view.AllButton.NavigateButton.onClick.AddListener(OnAllFilter);
        }

        private void OnAllFilter()
        {
            OnExitSearch();
            OnExitFilter();
        }

        private void OnExitSearch()
        {
            commandBus.SendCommand(new BackpackSearchCommand(""));
        }

        private void OnExitFilter()
        {
            commandBus.SendCommand(new BackpackFilterCategoryCommand(""));
        }

        private void OnSearch(string searchString)
        {
            if (string.IsNullOrEmpty(searchString))
            {
                view.SearchButton.gameObject.SetActive(false);
            }
            else
            {
                view.SearchButton.gameObject.SetActive(true);
                view.SearchButton.CategoryName.text = searchString;
            }
        }

        private void OnFilterCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                view.FilterButton.gameObject.SetActive(false);
            }
            else
            {
                view.FilterButton.gameObject.SetActive(true);
                view.FilterButton.Icon.sprite = categoryIcons.GetTypeImage(category.ToLower());
                view.FilterButton.CategoryName.text = category;
            }
        }
    }
}
