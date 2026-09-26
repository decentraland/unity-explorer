using DCL.UI;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.TeleportPrompt
{
    public class TeleportPromptView : ViewBase, IView
    {
        [field: SerializeField]
        public ImageView placeImage;

        [field: SerializeField]
        public Sprite defaultImage;

        [field: SerializeField]
        public GameObject placeInfoContainer;

        [field: SerializeField]
        public GameObject loadingPlaceContainer;

        [field: SerializeField]
        public GameObject loadingSpinner;

        [field: SerializeField]
        public TMP_Text location;

        [field: SerializeField]
        public TMP_Text placeName;

        [field: SerializeField]
        public TMP_Text placeCreator;

        [field: SerializeField]
        public Button continueButton;

        [field: SerializeField]
        public Button cancelButton;
    }
}
