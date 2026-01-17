using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    public class NewAccountLobbyScreenSubView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_InputField ProfileNameInputField { get; private set; } = null!;

        [field: SerializeField]
        public Button BackButton { get; private set; } = null!;

        [field: SerializeField]
        public Button FinalizeNewUserButton { get; private set; } = null!;

        [field: Space]
        [field: SerializeField]
        public Button PrevRandomButton { get; private set; } = null!;
        [field: SerializeField]
        public Button NextRandomButton { get; private set; } = null!;
        [field: SerializeField]
        public Button RandomizeButton { get; private set; } = null!;

        [field: Space]
        [field: SerializeField]
        public Toggle SubscribeToggle { get; private set; } = null!;
        [field: SerializeField]
        public Toggle AgreeLicenseToggle { get; private set; } = null!;
    }
}
