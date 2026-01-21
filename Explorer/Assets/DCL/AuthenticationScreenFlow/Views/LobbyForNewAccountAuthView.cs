using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    public class LobbyForNewAccountAuthView : MonoBehaviour
    {
        [field: SerializeField]
        public NameInputFieldView ProfileNameInputField { get; private set; } = null!;

        [field: SerializeField]
        public Button BackButton { get; private set; } = null!;

        [field: SerializeField]
        public Button FinalizeNewUserButton { get; private set; } = null!;

        [field: SerializeField]
        public Animator Animator { get; private set; } = null!;

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
        public Toggle TermsOfUse { get; private set; } = null!;
    }
}
