using DCL.UI;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.Gifting.Views
{
    public class GiftingView : ViewBase, IView
    {
        [field: SerializeField] public GiftingHeaderView HeaderView { get; private set; }

        [field: SerializeField] public TabSelectorView WearablesTab { get; private set; }
        [field: SerializeField] public TabSelectorView EmotesTab { get; private set; }
        [field: SerializeField] public SearchBarView SearchBar { get; private set; }
        [field: SerializeField] public BackpackGridView WearablesGrid { get; private set; }
        [field: SerializeField] public BackpackGridView EmotesGrid { get; private set; }

        [field: SerializeField] public GameObject InfoMessageContainer { get; private set; }
        [field: SerializeField] public TMP_Text InfoMessageLabel { get; private set; }
        [field: SerializeField] public Button CancelButton { get; private set; }
        [field: SerializeField] public Button SendGiftButton { get; private set; }
        
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public Button BackgroundButton { get; private set; }

        [field: SerializeField]
        public WarningNotificationView ErrorNotification { get; private set; }
    }
}