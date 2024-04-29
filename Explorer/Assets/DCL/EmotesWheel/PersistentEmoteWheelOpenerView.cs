using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.EmotesWheel
{
    public class PersistentEmoteWheelOpenerView : ViewBase, IView
    {
        [field: SerializeField]
        public Button OpenEmoteWheelButton { get; private set; }

        [field: SerializeField]
        public GameObject EmotesEnabledContainer { get; private set; }

        [field: SerializeField]
        public GameObject EmotesDisabledContainer { get; private set; }
    }
}
