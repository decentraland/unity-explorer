using UnityEngine;

namespace DCL.Friends.UI
{
    public class BlockedSectionView : FriendPanelSectionView
    {
        [field: SerializeField] public GameObject EmptyState { get; private set; }
    }
}
