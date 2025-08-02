using UnityEngine;

namespace DCL.Communities.CommunitiesCard
{
    [CreateAssetMenu(fileName = "CommunityCardContextMenuSettings", menuName = "DCL/Communities/Card/ContextMenuSettings")]
    public class CommunityCardContextMenuConfiguration : ScriptableObject
    {
        [field: SerializeField] public int ContextMenuWidth { get; private set; } = 218;
        [field: SerializeField] public int ElementsSpacing { get; private set; } = 5;
        [field: SerializeField] public Vector2 OffsetFromTarget { get; private set; }
        [field: SerializeField] public RectOffset VerticalPadding { get; private set; } = null!;


        [field: SerializeField] public Sprite ToggleNotificationsSprite { get; private set; } = null!;
        [field: SerializeField] public string ToggleNotificationsText { get; private set; } = "Notifications";

        [field: SerializeField] public Sprite LeaveCommunitySprite { get; private set; } = null!;
        [field: SerializeField] public string LeaveCommunityText { get; private set; } = "Leave Community";

        [field: SerializeField] public Sprite DeleteCommunitySprite { get; private set; } = null!;
        [field: SerializeField] public string DeleteCommunityText { get; private set; } = "Delete Community";
        [field: SerializeField] public Color DeleteCommunityTextColor { get; private set; } = Color.red;
    }
}
