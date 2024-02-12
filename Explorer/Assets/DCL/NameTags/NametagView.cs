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

        public void SetUserNametag(string username, string walletId)
        {
            Username.text = username;

            WalletId.text = walletId;
            WalletId.gameObject.SetActive(!string.IsNullOrEmpty(walletId));
            VerifiedIcon.SetActive(string.IsNullOrEmpty(walletId));
        }
    }
}
