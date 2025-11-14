using DCL.RewardPanel;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.Gifting.Views
{
    public class GiftTransferSuccessView : ViewBase, IView
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public Image RecipientThumbnail { get; private set; }

        [field: SerializeField]
        public TextMeshProUGUI GiftSentText { get; private set; }

        [field: SerializeField]
        public GiftTransferBackgroundAnimation BackgroundRaysAnimation { get; private set; }
    }
}