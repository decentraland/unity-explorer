using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// View for the chat reaction button. The heart icon is set statically
    /// in the prefab and never changes at runtime.
    /// </summary>
    public class ChatReactionButtonView : MonoBehaviour
    {
        [field: SerializeField] public Button ReactionButton { get; private set; } = null!;

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);
    }
}
