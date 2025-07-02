using UnityEngine;

namespace DCL.Communities.CommunitiesCard.Members
{
    [CreateAssetMenu(fileName = "CommunityMemberListContextMenuSettings", menuName = "DCL/Communities/Members/ContextMenuSettings")]
    public class CommunityMemberListContextMenuConfiguration : ScriptableObject
    {
        [field: SerializeField] public int ContextMenuWidth { get; private set; } = 250;
        [field: SerializeField] public int ElementsSpacing { get; private set; } = 5;
        [field: SerializeField] public int SeparatorHeight { get; private set; } = 20;
        [field: SerializeField] public RectOffset VerticalPadding { get; private set; }

        [field: SerializeField] public Sprite ViewProfileSprite { get; private set; }
        [field: SerializeField] public string ViewProfileText { get; private set; } = "View Profile";

        [field: SerializeField] public Sprite BlockSprite { get; private set; }
        [field: SerializeField] public string BlockText { get; private set; } = "Block";

        [field: SerializeField] public Sprite ChatSprite { get; private set; }
        [field: SerializeField] public string ChatText { get; private set; } = "Chat";

        [field: SerializeField] public Sprite CallSprite { get; private set; }
        [field: SerializeField] public string CallText { get; private set; } = "Call";

        [field: SerializeField] public Sprite RemoveModeratorSprite { get; private set; }
        [field: SerializeField] public string RemoveModeratorText { get; private set; } = "Demote Moderator";

        [field: SerializeField] public Sprite AddModeratorSprite { get; private set; }
        [field: SerializeField] public string AddModeratorText { get; private set; } = "Promote Moderator";

        [field: SerializeField] public Sprite KickUserSprite { get; private set; }
        [field: SerializeField] public string KickUserText { get; private set; } = "Remove Member";

        [field: SerializeField] public Sprite BanUserSprite { get; private set; }
        [field: SerializeField] public string BanUserText { get; private set; } = "Ban";
    }
}
