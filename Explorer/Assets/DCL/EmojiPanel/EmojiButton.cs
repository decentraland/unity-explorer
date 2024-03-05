using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Emoji
{
    public class EmojiButton : MonoBehaviour
    {

        [field: SerializeField]
        public TMP_Text EmojiImage { get; private set; }

        [field: SerializeField]
        public Button Button { get; private set; }

    }
}
