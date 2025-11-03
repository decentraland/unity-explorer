using System;
using DCL.UI;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.Gifting.Views
{
    [Serializable]
    public struct GiftingTabSelectorMapping
    {
        [field: SerializeField] public TabSelectorView TabSelectorView { get; private set; }
        [field: SerializeField] public GiftingSection Section { get; private set; }
    }
    
    public class GiftingView : ViewBase, IView
    {
        [field: Header("Tabs")]
        [field: SerializeField]
        public GiftingTabSelectorMapping[] TabSelectorMappedViews { get; private set; }

        [field: Header("Content Containers")]
        [field: SerializeField]
        public RectTransform ContentContainer { get; private set; }

        [field: Header("Sections")]
        [field: SerializeField]
        public GiftingHeaderView HeaderView { get; private set; }

        [field: SerializeField]
        public GiftingFooterView FooterView { get; private set; }

        [field: Header("Content Grids")]
        [field: SerializeField]
        public GiftingGridView? WearablesGrid { get; private set; }

        [field: SerializeField]
        public GiftingGridView? EmotesGrid { get; private set; }

        [field: Header("Popup Controls")]
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public Button BackgroundButton { get; private set; }

        [field: Header("Feedback")]
        [field: SerializeField]
        public WarningNotificationView ErrorNotification { get; private set; }
    }
}