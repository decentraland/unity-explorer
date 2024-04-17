using DCL.Audio;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ExplorePanel
{
    public class PersistentExploreOpenerView : ViewBase, IView
    {
        [field: SerializeField]
        public Button OpenExploreButton { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ButtonPressedAudio { get; private set; }
    }
}
