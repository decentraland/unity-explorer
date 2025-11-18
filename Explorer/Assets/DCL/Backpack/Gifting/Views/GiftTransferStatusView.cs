using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.Gifting.Views
{
    public class GiftTransferStatusView : ViewBase, IView
    {
        [field: SerializeField]
        public Sprite WarningIcon { get; private set; }

        [Header("UI State Elements")]
        [SerializeField] private GameObject statusGroup;
        
        [Header("Header")]
        [field: SerializeField] public Button BackButton { get; private set; }

        [field: SerializeField] public Image ItemBackground { get; private set; }
        [field: SerializeField] public Image ItemCategory { get; private set; }
        [field: SerializeField] public Image ItemCategoryBackground { get; private set; }
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public TMP_Text TitleLabel { get; private set; }
        [field: SerializeField] public Image RecipientAvatar { get; private set; }
        [field: SerializeField] public TMP_Text RecipientName { get; private set; }

        [Header("Item")]
        [field: SerializeField] public Image ItemThumbnail { get; private set; }
        [field: SerializeField] public TMP_Text ItemName { get; private set; }

        [Header("Status")]
        [field: SerializeField] public TMP_Text StatusText { get; private set; }

        [field: SerializeField] public GameObject? LongRunningHint { get; private set; }

        public GameObject StatusContainer => statusGroup;
    }
}