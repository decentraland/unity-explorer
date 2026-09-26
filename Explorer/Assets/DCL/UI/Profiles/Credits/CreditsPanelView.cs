using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Credits
{
    public class CreditsPanelView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text CurrentCredits { get; private set; } = null!;

        [field: SerializeField]
        public Button GetCreditsButton { get; private set; } = null!;
    }
}
