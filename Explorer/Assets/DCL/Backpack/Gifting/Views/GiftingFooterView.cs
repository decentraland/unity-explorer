using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.Gifting.Views
{
    public class GiftingFooterView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject InfoMessageContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text InfoMessageLabel { get; private set; }

        [field: SerializeField]
        public Button CancelButton { get; private set; }

        [field: SerializeField]
        public Button SendGiftButton { get; private set; }
    }
}