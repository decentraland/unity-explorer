using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Runtime.Wearables;
using UI.Elements;
using UnityEngine;
using UnityEngine.UIElements;
using Utils;

namespace Configurator.Views
{
    public class WearablesView : StageView
    {
        private readonly Label _header;
        private readonly VisualElement _sidebar;
        private readonly ScrollView _itemsContainer;
        private readonly VisualElement _colorDropdownParent;
        private readonly DCLDropdownElement _colorDropdown;

        private List<CategoryDefinition> _collection;

        private readonly Dictionary<string, WearableCategoryElement> _categoryElements = new();
        private WearableCategoryElement _selectedCategoryElement;
        private WearableItemElement _selectedWearableElement;
        private Dictionary<string, EntityDefinition> _selectedItems = new();

        private readonly ColorPopupView _colorPopupView;

        public override string SelectedCategory => _selectedCategoryElement.Category;

        public event Action<string> CategoryChanged;
        public event Action<string, EntityDefinition> WearableSelected;
        public event Action<Color> ColorSelected;

        private Color[] _hairColorPresets;
        private Color[] _eyeColorPresets;
        private Color _currentHairColor;
        private Color _currentEyeColor;

        public WearablesView(VisualElement root, string title, string primaryButtonText, int primaryButtonWidth, string primaryButtonTextMobile) : 
            base(root, title, primaryButtonText, primaryButtonWidth, primaryButtonTextMobile, null, null)
        {
            _header = root.Q<Label>("CategoryHeader");
            _sidebar = root.Q<VisualElement>("Sidebar");
            _itemsContainer = root.Q<ScrollView>("Items");

            _colorDropdown = root.Q<DCLDropdownElement>("ColorDropdown");
            _colorDropdownParent = _colorDropdown.parent;
            _colorPopupView = new ColorPopupView(_colorDropdown, _colorDropdown.Q("ColorPopup"), _colorDropdown.Icon);
            _colorPopupView.ColorSelected += OnColorSelected;

            foreach (var ve in _itemsContainer.Children())
            {
                ((WearableItemElement)ve).WearableClicked = OnWearableClicked;
            }
        }

        public void SetCollection(List<CategoryDefinition> collection, string selectedCategory = null)
        {
            _collection = collection;
            _sidebar.Clear();

            var categorySet = false;
            foreach (var cd in collection)
            {
                var categoryElement = new WearableCategoryElement(cd.id, cd.title, cd.emptyThumbnail);
                categoryElement.Clicked += OnCategoryClicked;
                _sidebar.Add(categoryElement);
                _categoryElements[cd.id] = categoryElement;
                _selectedItems[cd.id] = null;

                if (categorySet || (selectedCategory != null && cd.id != selectedCategory)) continue;

                categorySet = true;
                _selectedCategoryElement = categoryElement;
                _selectedCategoryElement.SetSelected(true);
                RefreshCurrentCategory();
            }
        }

        public override void SetUsingMobileMode(bool usingMobile)
        {
            if (usingMobile == UsingMobile) return;

            base.SetUsingMobileMode(usingMobile);

            _colorPopupView.SetAutoClose(usingMobile);

            if (!usingMobile)
            {
                _colorDropdown.RemoveFromHierarchy();
                _colorDropdownParent.Add(_colorDropdown);
            }

            RefreshCurrentCategory();
        }

        public override object GetData()
        {
            return (_collection, _selectedItems, _hairColorPresets, _eyeColorPresets, _currentHairColor,
                _currentEyeColor, _selectedCategoryElement.Category, _colorDropdown.IsOpen);
        }

        public override void SetData(object data)
        {
            var cast =
                ((List<CategoryDefinition> collection, Dictionary<string, EntityDefinition> selectedItems, Color[]
                    hairColorPresets, Color[] eyeColorPresets, Color currentHairColor, Color currentEyeColor, string
                    selectedCategory, bool colorDropdownOpen))data;

            SetColorPresets(cast.hairColorPresets, cast.eyeColorPresets);
            _currentHairColor = cast.currentHairColor;
            _currentEyeColor = cast.currentEyeColor;

            SetCollection(cast.collection, cast.selectedCategory);
            SetSelectedItems(cast.selectedItems);

            _colorDropdown.Open(cast.colorDropdownOpen);
        }

        public void SetSelectedItems(Dictionary<string, EntityDefinition> selectedItems)
        {
            foreach (var category in _selectedItems.Keys.ToList())
            {
                var selectedItem = selectedItems.GetValueOrDefault(category);
                _selectedItems[category] = selectedItem;
                _categoryElements[category].SetWearable(selectedItem);
            }

            RefreshCurrentCategory();
        }

        private void OnCategoryClicked(WearableCategoryElement categoryElement)
        {
            _selectedCategoryElement?.SetSelected(false);
            _selectedCategoryElement = categoryElement;
            _selectedCategoryElement.SetSelected(true);

            RefreshCurrentCategory();

            CategoryChanged!(categoryElement.Category);
        }

        private void RefreshCurrentCategory()
        {
            var category = _selectedCategoryElement.Category;

            // Ugly but ok
            if (category == WearableCategories.Categories.EYES)
            {
                _colorDropdown.SetDisplay(true);
                _colorPopupView.SetColors(_eyeColorPresets);
                _colorPopupView.SetSelectedColor(_currentEyeColor);
                _colorPopupView.SetTitle("EYE COLOR");
                _colorDropdown.Text = UsingMobile ? null : "EYE COLOR";
            }
            else if (category == WearableCategories.Categories.HAIR)
            {
                _colorDropdown.SetDisplay(true);
                _colorPopupView.SetColors(_hairColorPresets);
                _colorPopupView.SetSelectedColor(_currentHairColor);
                _colorPopupView.SetTitle("HAIR COLOR");
                _colorDropdown.Text = UsingMobile ? null : "HAIR COLOR";
            }
            else
            {
                _colorDropdown.SetDisplay(false);
            }

            if (UsingMobile)
            {
                _colorDropdown.RemoveFromHierarchy();
                _selectedCategoryElement.Add(_colorDropdown);
            }

            _header.text = "<font-weight=600>" + _selectedCategoryElement.Title;

            var categoryDefinition = _collection.First(cd => cd.id == category);
            var selectedWearable = _selectedItems[category];

            if (categoryDefinition.Definitions.Count > 20)
            {
                Debug.LogError($"Too many items in {category}: {categoryDefinition.Definitions.Count}");
            }

            var index = 0;
            _selectedWearableElement = null;
            foreach (var ve in _itemsContainer.Children())
            {
                ve.SetVisibility(true);
                ve.SetDisplay(true);

                if (index < categoryDefinition.Definitions.Count)
                {
                    var wearable = categoryDefinition.Definitions[index];
                    var wpbe = (WearableItemElement)ve;
                    wpbe.EmptyTexture = categoryDefinition.emptyThumbnail;
                    wpbe.SetWearable(wearable);
                    var selected = wearable == selectedWearable;
                    wpbe.Selected = selected;

                    if (selected)
                    {
                        _selectedWearableElement = wpbe;
                    }
                }
                else
                {
                    if (UsingMobile)
                    {
                        ve.SetDisplay(false);
                    }
                    else
                    {
                        ve.SetVisibility(false);
                    }
                }

                index++;
            }

            if (UsingMobile && _selectedWearableElement != null)
            {
                _itemsContainer.scrollOffset = Vector2.zero;
                _itemsContainer.ScrollTo(_selectedWearableElement);
            }
        }

        public void SetColorPresets(Color[] hairColorPresets, Color[] eyeColorPresets)
        {
            _hairColorPresets = hairColorPresets;
            _eyeColorPresets = eyeColorPresets;
        }

        public void SetSelectedColors(Color hairColor, Color eyeColor)
        {
            _currentHairColor = hairColor;
            _currentEyeColor = eyeColor;

            var category = _selectedCategoryElement?.Category;
            if (category == WearableCategories.Categories.HAIR)
            {
                _colorPopupView.SetSelectedColor(hairColor);
            }
            else if (category == WearableCategories.Categories.EYES)
            {
                _colorPopupView.SetSelectedColor(eyeColor);
            }
        }

        private void OnColorSelected(Color color)
        {
            var category = _selectedCategoryElement.Category;
            if (category == WearableCategories.Categories.HAIR)
            {
                _currentHairColor = color;
            }
            else if (category == WearableCategories.Categories.EYES)
            {
                _currentEyeColor = color;
            }

            ColorSelected!(color);
        }

        private void OnWearableClicked(WearableItemElement wpbe)
        {
            if (_selectedWearableElement != null) _selectedWearableElement.Selected = false;
            wpbe.Selected = true;
            _selectedWearableElement = wpbe;

            _selectedItems[_selectedCategoryElement.Category] = wpbe.Wearable;

            _selectedCategoryElement.SetWearable(wpbe.Wearable);

            WearableSelected!(_selectedCategoryElement.Category, wpbe.Wearable);
        }
    }
}