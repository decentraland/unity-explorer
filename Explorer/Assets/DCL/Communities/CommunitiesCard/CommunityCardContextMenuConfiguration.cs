using System;
using UnityEngine;

namespace DCL.Communities.CommunitiesCard
{
    [CreateAssetMenu(fileName = "CommunityCardContextMenuSettings", menuName = "DCL/Communities/Card/ContextMenuSettings")]
    [Serializable]
    public class CommunityCardContextMenuConfiguration : ScriptableObject
    {
        [SerializeField] private int contextMenuWidth = 250;
        [SerializeField] private int elementsSpacing = 5;
        [SerializeField] private RectOffset verticalPadding;
        [Space(10)]
        [SerializeField] private Sprite leaveCommunitySprite;
        [SerializeField] private string leaveCommunityText = "Leave Community";
        [Space(10)]
        [SerializeField] private Sprite deleteCommunitySprite;
        [SerializeField] private string deleteCommunityText = "Delete Community";

        public int ContextMenuWidth => contextMenuWidth;
        public int ElementsSpacing => elementsSpacing;
        public RectOffset VerticalPadding => verticalPadding;

        public Sprite LeaveCommunitySprite => leaveCommunitySprite;
        public string LeaveCommunityText => leaveCommunityText;

        public Sprite DeleteCommunitySprite => deleteCommunitySprite;
        public string DeleteCommunityText => deleteCommunityText;
    }
}
