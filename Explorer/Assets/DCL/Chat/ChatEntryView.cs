using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryView : MonoBehaviour
    {
        [field: SerializeField]
        internal TMP_Text playerName { get; private set; }

        [field: SerializeField]
        internal Image playerIcon { get; private set; }

        [field: SerializeField]
        internal TMP_Text entryText { get; private set; }

        [field: SerializeField]
        internal TMP_Text walletIdText { get; private set; }

        [field: SerializeField]
        internal Image verifiedIcon { get; private set; }
    }
}
