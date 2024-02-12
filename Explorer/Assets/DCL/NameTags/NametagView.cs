using TMPro;
using UnityEngine;

namespace DCL.Nametags
{
    public class NametagView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text Username { get; private set; }

        [field: SerializeField]
        public TMP_Text WalletId { get; private set; }

        [field: SerializeField]
        public GameObject VerifiedIcon { get; private set; }
    }
}
