using DCL.UI;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.Gifting.Views
{
    public class GiftingView : ViewBase, IView
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public Button BackgroundButton { get; private set; }

        [field: SerializeField]
        public WarningNotificationView ErrorNotification { get; private set; }
    }
}