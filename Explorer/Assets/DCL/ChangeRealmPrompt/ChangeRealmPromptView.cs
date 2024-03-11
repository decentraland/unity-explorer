using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ChangeRealmPrompt
{
    public class ChangeRealmPromptView : ViewBase, IView
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public Button ContinueButton { get; private set; }

        [field: SerializeField]
        public Button CancelButton { get; private set; }

        [field: SerializeField]
        public TMPro.TextMeshProUGUI MessageText { get; private set; }

        [field: SerializeField]
        public TMPro.TextMeshProUGUI RealmText { get; private set; }
    }
}
