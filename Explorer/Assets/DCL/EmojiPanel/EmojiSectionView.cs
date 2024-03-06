using TMPro;
using UnityEngine;

namespace DCL.Emoji
{
    public class EmojiSectionView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text SectionTitle { get; private set; }

        [field: SerializeField]
        public Transform EmojiContainer { get; private set; }

        public void Configure(string sectionTitle)
        {
            SectionTitle.text = sectionTitle;
        }
    }
}
