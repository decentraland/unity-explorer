using TMPro;
using UnityEngine;

namespace DCL.Emoji
{
    public class EmojiSectionHeaderView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text Title { get; private set; }

        public void SetTitle(string title)
        {
            Title.text = title;
        }
    }
}
