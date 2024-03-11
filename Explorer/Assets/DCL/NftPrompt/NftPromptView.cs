using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.NftPrompt
{
    public class NftPromptView : ViewBase, IView
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public Button ViewOnOpenSeaButton { get; private set; }

        [field: SerializeField]
        public Button CancelButton { get; private set; }
    }
}
