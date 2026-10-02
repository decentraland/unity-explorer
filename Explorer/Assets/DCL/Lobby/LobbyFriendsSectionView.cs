using TMPro;
using UnityEngine;

namespace DCL.Lobby
{
    public class LobbyFriendsSectionView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text OnlineCountText { get; private set; } = null!;

        [field: SerializeField]
        public LobbyFriendsRailView Rail { get; private set; } = null!;
    }
}
