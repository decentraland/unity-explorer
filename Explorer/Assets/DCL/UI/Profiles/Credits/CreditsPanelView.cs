using TMPro;
using UnityEngine;

namespace DCL.UI.Credits
{
    public class CreditsPanelView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text CurrentCredits { get; private set; } = null!;
    }
}
