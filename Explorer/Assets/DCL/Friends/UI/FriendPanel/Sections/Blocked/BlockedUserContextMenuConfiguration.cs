using System;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Blocked
{
    [CreateAssetMenu(fileName = "BlockedUserContextMenuSettings", menuName = "DCL/Friends/BlockedUsers/ContextMenuSettings")]
    [Serializable]
    public class BlockedUserContextMenuConfiguration : ScriptableObject
    {
        [SerializeField] private int contextMenuWidth = 250;
        [Space(10)]
        [SerializeField] private Sprite viewProfileSprite;
        [SerializeField] private string viewProfileText = "View Profile";

        public int ContextMenuWidth => contextMenuWidth;
        public Sprite ViewProfileSprite => viewProfileSprite;
        public string ViewProfileText => viewProfileText;
    }
}
