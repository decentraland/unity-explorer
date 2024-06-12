using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.UI;
using UnityEngine;

namespace DCL.Backpack.Breadcrumb
{
    public class BackpackBreadCrumbController
    {
        private readonly BackpackBreadCrumbView view;
        private readonly IBackpackCommandBus commandBus;
        private readonly NftTypeIconSO categoryIcons;
        private readonly ColorPickerController colorPickerController;

        public BackpackBreadCrumbController(BackpackBreadCrumbView view, IBackpackEventBus eventBus, IBackpackCommandBus commandBus, NftTypeIconSO categoryIcons, ColorToggleView colorToggle, ColorPresetsSO hairColors, ColorPresetsSO eyesColors, ColorPresetsSO bodyshapeColors)
        {
            this.view = view;
            this.commandBus = commandBus;
            this.categoryIcons = categoryIcons;
            colorPickerController = new ColorPickerController(view.ColorPickerView, colorToggle, hairColors, eyesColors, bodyshapeColors);
            colorPickerController.OnColorChanged += OnColorChanged;
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
            SetAllButtonColor(true);
        }

        private void OnExitSearch() =>
            commandBus.SendCommand(new BackpackSearchCommand(""));

        private void OnExitFilter() =>
            commandBus.SendCommand(new BackpackFilterCategoryCommand(""));

        private void OnColorChanged(Color newColor, string category) =>
            commandBus.SendCommand(new BackpackChangeColorCommand(newColor, category));

        private void OnSearch(string searchString)
        {
            if (string.IsNullOrEmpty(searchString))
            {
                view.SearchButton.gameObject.SetActive(false);
                SetAllButtonColor(true);
            }
            else
            {
                view.SearchButton.gameObject.SetActive(true);
                view.SearchButton.CategoryName.text = searchString;
                SetAllButtonColor(false);
            }
        }

        private void OnFilterCategory(string category)
        {
            colorPickerController.SetColorPickerStatus(category.ToLower());
            if (string.IsNullOrEmpty(category))
            {
                view.FilterButton.gameObject.SetActive(false);
                SetAllButtonColor(true);
            }
            else
            {
                view.FilterButton.gameObject.SetActive(true);
                view.FilterButton.Icon.sprite = categoryIcons.GetTypeImage(category.ToLower());
                view.FilterButton.CategoryName.text = WearablesConstants.READABLE_CATEGORIES[category.ToLower()];
                SetAllButtonColor(false);
            }
        }

        private void SetAllButtonColor(bool isSelected)
        {
            view.AllButtonArrow.SetActive(!isSelected);
            view.AllButton.BackgroundImage.color = isSelected ? view.AllButton.SelectedBackgroundColor : view.AllButton.UnselectedBackgroundColor;
            view.AllButton.CategoryName.color = isSelected ? view.AllButton.SelectedFontColor : view.AllButton.UnselectedFontColor;
            view.AllButton.Icon.color = isSelected ? view.AllButton.SelectedIconColor : view.AllButton.UnselectedIconColor;
        }
    }
}
