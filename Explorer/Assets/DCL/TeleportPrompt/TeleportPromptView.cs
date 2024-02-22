using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.TeleportPrompt
{
    public class TeleportPromptView : ViewBase, IView
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public Button ContinueButton { get; private set; }

        [field: SerializeField]
        public Button CancelButton { get; private set; }

        [field: SerializeField]
        public TMPro.TextMeshProUGUI CoordsText { get; private set; }
    }
}
