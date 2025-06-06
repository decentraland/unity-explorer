using System;
using UnityEngine;

namespace DCL.Communities.CommunitiesCard.Members
{
    [CreateAssetMenu(fileName = "CommunityMemberListContextMenuSettings", menuName = "DCL/Communities/Members/ContextMenuSettings")]
    [Serializable]
    public class CommunityMemberListContextMenuConfiguration : ScriptableObject
    {
        [SerializeField] private int contextMenuWidth = 250;
        [Space(10)]
        [SerializeField] private Sprite viewProfileSprite;
        [SerializeField] private string viewProfileText = "View Profile";
        [Space(10)]
        [SerializeField] private Sprite blockSprite;
        [SerializeField] private string blockText = "Block";

        [SerializeField] private Sprite chatSprite;
        [SerializeField] private string chatText = "Chat";

        [SerializeField] private Sprite callSprite;
        [SerializeField] private string callText = "Call";

        [SerializeField] private Sprite removeModeratorSprite;
        [SerializeField] private string removeModeratorText = "Remove Moderator";

        [SerializeField] private Sprite addModeratorSprite;
        [SerializeField] private string addModeratorText = "Add Moderator";

        [SerializeField] private Sprite kickUserSprite;
        [SerializeField] private string kickUserText = "Kick";

        [SerializeField] private Sprite banUserSprite;
        [SerializeField] private string banUserText = "Ban";

        public int ContextMenuWidth => contextMenuWidth;

        public Sprite ViewProfileSprite => viewProfileSprite;
        public string ViewProfileText => viewProfileText;

        public Sprite BlockSprite => blockSprite;
        public string BlockText => blockText;

        public Sprite ChatSprite => chatSprite;
        public string ChatText => chatText;

        public Sprite CallSprite => callSprite;
        public string CallText => callText;

        public Sprite RemoveModeratorSprite => removeModeratorSprite;
        public string RemoveModeratorText => removeModeratorText;

        public Sprite AddModeratorSprite => addModeratorSprite;
        public string AddModeratorText => addModeratorText;

        public Sprite KickUserSprite => kickUserSprite;
        public string KickUserText => kickUserText;

        public Sprite BanUserSprite => banUserSprite;
        public string BanUserText => banUserText;
    }
}
