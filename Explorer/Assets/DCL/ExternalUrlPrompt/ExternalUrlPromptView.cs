using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ExternalUrlPrompt
{
    public class ExternalUrlPromptView : ViewBase, IView
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public Button ContinueButton { get; private set; }

        [field: SerializeField]
        public Button CancelButton { get; private set; }

        [field: SerializeField]
        public TMPro.TextMeshProUGUI DomainText { get; private set; }

        [field: SerializeField]
        public TMPro.TextMeshProUGUI UrlText { get; private set; }

        [field: SerializeField]
        public Toggle TrustToggle { get; private set; }
    }
}
