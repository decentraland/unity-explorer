using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.Breadcrumb
{
    public class NftSubCategoryFilterComponentView : MonoBehaviour
    {
        [field: SerializeField]
        public Button NavigateButton { get; private set; }

        [field: SerializeField]
        public Button ExitButton { get; private set; }

        [field: SerializeField]
        public TMP_Text CategoryName{ get; private set; }

        [field: SerializeField]
        public Image Icon { get; private set; }

        [field: SerializeField]
        public Image BackgroundImage { get; private set; }

        [field: SerializeField]
        public Color SelectedBackgroundColor { get; private set; }

        [field: SerializeField]
        public Color SelectedFontColor { get; private set; }

        [field: SerializeField]
        public Color UnselectedBackgroundColor { get; private set; }

        [field: SerializeField]
        public Color UnselectedFontColor { get; private set; }

        [field: SerializeField]
        public Color SelectedIconColor { get; private set; }

        [field: SerializeField]
        public Color UnselectedIconColor { get; private set; }

        //    BackgroundImage.color = model.IsSelected ? SelectedBackgroundColor : UnselectedBackgroundColor;
        //    CategoryName.color = model.IsSelected ? SelectedFontColor : UnselectedFontColor;
        //    Icon.color = model.IsSelected ? SelectedIconColor : UnselectedIconColor;
    }
}
