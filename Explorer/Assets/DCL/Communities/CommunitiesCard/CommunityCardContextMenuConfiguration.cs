using UnityEngine;

namespace DCL.Communities.CommunitiesCard
{
    [CreateAssetMenu(fileName = "CommunityCardContextMenuSettings", menuName = "DCL/Communities/Card/ContextMenuSettings")]
    public class CommunityCardContextMenuConfiguration : ScriptableObject
    {
        [SerializeField] private int contextMenuWidth = 250;
        [SerializeField] private int elementsSpacing = 5;
        [SerializeField] private Vector2 offsetFromTarget = Vector2.zero;
        [SerializeField] private RectOffset verticalPadding;
        [Space(10)]
        [SerializeField] private Sprite leaveCommunitySprite;
        [SerializeField] private string leaveCommunityText = "Leave Community";
        [Space(10)]
        [SerializeField] private Sprite deleteCommunitySprite;
        [SerializeField] private string deleteCommunityText = "Delete Community";
        [SerializeField] private Color deleteCommunityTextColor = Color.red;

        public int ContextMenuWidth => contextMenuWidth;
        public int ElementsSpacing => elementsSpacing;
        public Vector2 OffsetFromTarget => offsetFromTarget;
        public RectOffset VerticalPadding => verticalPadding;

        public Sprite LeaveCommunitySprite => leaveCommunitySprite;
        public string LeaveCommunityText => leaveCommunityText;

        public Sprite DeleteCommunitySprite => deleteCommunitySprite;
        public string DeleteCommunityText => deleteCommunityText;
        public Color DeleteCommunityTextColor => deleteCommunityTextColor;
    }
}
