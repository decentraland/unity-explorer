using DCL.CharacterPreview;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport
{
    public class PassportView : ViewBase, IView
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public Button BackgroundButton { get; private set; }

        [field: SerializeField]
        public CharacterPreviewView CharacterPreviewView { get; private set; }

        [field: SerializeField]
        public RectTransform UserNameContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text UserNameText { get; private set; }

        [field: SerializeField]
        public GameObject VerifiedMark { get; private set; }

        [field: SerializeField]
        public Button CopyUserNameButton { get; private set; }

        [field: SerializeField]
        public RectTransform WalletAddressContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text UserWalletAddressText { get; private set; }

        [field: SerializeField]
        public Button CopyWalletAddressButton { get; private set; }
    }
}
