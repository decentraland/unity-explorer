using UnityEngine;
using UnityEngine.UI;

namespace DCL.Emoji
{
    public class EmojiSuggestionPanelView : MonoBehaviour
    {
        [field: SerializeField]
        public Transform EmojiSuggestionContainer { get; private set; }

        [field: SerializeField]
        public RectTransform ScrollView { get; private set; }

        [field: SerializeField]
        public ScrollRect ScrollViewComponent { get; private set; }

        [field: SerializeField]
        public GameObject NoResults { get; private set; }
    }
}
