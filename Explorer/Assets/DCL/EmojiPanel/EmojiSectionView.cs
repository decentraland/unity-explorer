using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Emoji
{
    public class EmojiSectionView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text SectionTitle { get; private set; }

        [field: SerializeField]
        public RectTransform SectionRectTransform { get; private set; }

        [field: SerializeField]
        public RectTransform EmojiContainer { get; private set; }

        public EmojiSectionName SectionName;

    }
}
