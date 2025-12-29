using DCL.Audio;
using DCL.Backpack.Gifting.Views;
using DCL.RewardPanel;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.Gifting.Notifications
{
    public class GiftReceivedPopupView : ViewBase, IView
    {
        [field: Header("Canvas Group")]
        [field: SerializeField] public CanvasGroup MainCanvasGroup { get; private set; }
        
        [field: Header("Labels")]
        [field: SerializeField] public TMP_Text SubTitleText { get; private set; }

        [field: SerializeField] public TMP_Text TitleText { get; private set; }
        [field: SerializeField] public TMP_Text ItemNameText { get; private set; }

        [field: Header("Item Container")]
        [field: SerializeField] public GiftOpenedItemView GiftItemView { get; private set; }

        [field: Header("Buttons")]
        [field: SerializeField] public Button OpenBackpackButton { get; private set; }

        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public Button BackgroundOverlayButton { get; private set; }

        [field: SerializeField]
        public GiftTransferBackgroundAnimation BackgroundRaysAnimation { get; private set; }

        [field: SerializeField]
        public AudioClipConfig Sound { get; set; }
    }
}