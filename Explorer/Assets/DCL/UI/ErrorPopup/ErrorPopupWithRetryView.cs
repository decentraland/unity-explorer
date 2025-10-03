using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ErrorPopup
{
    public class ErrorPopupWithRetryView : ViewBase, IView
    {
        [field: SerializeField]
        public Button ExitButton { get; private set; }

        [field: SerializeField]
        public Button RestartButton { get; private set; }

        [field: SerializeField]
        public TMP_Text DescriptionText { get; private set; }

        [field: SerializeField]
        public TMP_Text TitleText { get; private set; }

        [field: SerializeField]
        public GameObject WarningIcon { get; private set; }

        [field: SerializeField]
        public GameObject ErrorIcon { get; private set; }

        [field: SerializeField]
        public GameObject InternetLostIcon { get; private set; }
    }
}
