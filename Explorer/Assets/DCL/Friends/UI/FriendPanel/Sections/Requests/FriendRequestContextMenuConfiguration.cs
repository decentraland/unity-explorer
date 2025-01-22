using System;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Requests
{
    [CreateAssetMenu(fileName = "FriendRequestContextMenuSettings", menuName = "DCL/Friends/Requests/ContextMenuSettings")]
    [Serializable]
    public class FriendRequestContextMenuConfiguration : ScriptableObject
    {
        [SerializeField] private int contextMenuWidth = 250;
        [Space(10)]
        [SerializeField] private Sprite viewProfileSprite;
        [SerializeField] private string viewProfileText = "View Profile";
        [Space(10)]
        [SerializeField] private Sprite blockSprite;
        [SerializeField] private string blockText = "Block";
        [Space(10)]
        [SerializeField] private Sprite reportSprite;
        [SerializeField] private string reportText = "Report";

        public int ContextMenuWidth => contextMenuWidth;
        public Sprite ViewProfileSprite => viewProfileSprite;
        public string ViewProfileText => viewProfileText;
        public Sprite BlockSprite => blockSprite;
        public string BlockText => blockText;
        public Sprite ReportSprite => reportSprite;
        public string ReportText => reportText;
    }
}
