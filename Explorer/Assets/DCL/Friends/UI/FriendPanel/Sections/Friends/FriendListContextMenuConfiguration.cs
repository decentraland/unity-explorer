using System;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    [CreateAssetMenu(fileName = "FriendListContextMenuSettings", menuName = "DCL/Friends/FriendList/ContextMenuSettings")]
    [Serializable]
    public class FriendListContextMenuConfiguration : ScriptableObject
    {
        [SerializeField] private int contextMenuWidth = 250;
        [Space(10)]
        [SerializeField] private Sprite viewProfileSprite;
        [SerializeField] private string viewProfileText = "View Profile";
        [Space(10)]
        [SerializeField] private Sprite blockSprite;
        [SerializeField] private string blockText = "Block";

        public int ContextMenuWidth => contextMenuWidth;
        public Sprite ViewProfileSprite => viewProfileSprite;
        public string ViewProfileText => viewProfileText;
        public Sprite BlockSprite => blockSprite;
        public string BlockText => blockText;
    }
}
