using UnityEngine;

namespace DCL.Chat
{
    [CreateAssetMenu(fileName = "ChatEntryConfiguration", menuName = "SO/ChatEntryConfiguration")]
    public class ChatEntryConfigurationSO : ScriptableObject
    {
        [SerializeField] public Sprite otherUsersBackground;
        [SerializeField] public Sprite otherUsersVerifiedIcon;
        [SerializeField] public Color otherUsersEntryColor;

        [SerializeField] public Sprite ownUsersBackground;
        [SerializeField] public Sprite ownUserVerifiedIcon;
        [SerializeField] public Color ownUsersEntryColor;
    }
}
